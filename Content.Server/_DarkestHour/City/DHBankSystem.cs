using Content.Server.GameTicking;
using Content.Shared._DarkestHour.City;
using Content.Shared.GameTicking;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.City;

/// <summary>Электронный счёт (живёт до конца раунда).</summary>
public sealed class DHBankAccount
{
    public string Number = string.Empty;
    public string Holder = string.Empty;
    public string Login = string.Empty;
    public string Password = string.Empty;
    public int Balance;
    public DHBankOrg Org = DHBankOrg.None;

    /// <summary>История операций, новые — в конце.</summary>
    public List<string> Log = new();
}

/// <summary>
/// Банк Лост-Энджелса: личные счета игроков, счета LA-V и военпола, переводы, внесение и снятие наличных.
/// Только на раунд, в базу не пишется.
/// </summary>
public sealed partial class DHBankSystem : EntitySystem
{
    [Dependency] private ServerGameTicker _ticker = default!;
    [Dependency] private IRobustRandom _random = default!;

    public const int VanguardStartBalance = 15000;
    public const int PoliceStartBalance = 8000;
    private const int MaxLog = 30;

    private const string LoginLetters = "abcdefghkmnprstuvwxyz";
    private const string PasswordLetters = "abcdefghkmnprstuvwxyz23456789";

    private readonly Dictionary<string, DHBankAccount> _accounts = new();
    private readonly Dictionary<DHBankOrg, string> _orgs = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<DHBankCardComponent, MapInitEvent>(OnCardMapInit);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _accounts.Clear();
        _orgs.Clear();
    }

    private void OnCardMapInit(Entity<DHBankCardComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Org == DHBankOrg.None || ent.Comp.Number.Length > 0)
            return;

        var account = OrgAccount(ent.Comp.Org);
        ent.Comp.Number = account.Number;
        ent.Comp.Holder = account.Holder;
        Dirty(ent);
    }

    #region Счета

    public DHBankAccount Create(string holder, int balance, DHBankOrg org = DHBankOrg.None)
    {
        string number;
        do
        {
            number = $"{_random.Next(1000, 10000)}-{_random.Next(1000, 10000)}";
        } while (_accounts.ContainsKey(number));

        string login;
        do
        {
            login = RandomString(LoginLetters, 4) + _random.Next(10, 100);
        } while (FindByLogin(login) != null);

        var account = new DHBankAccount
        {
            Number = number,
            Holder = holder,
            Login = login,
            Password = RandomString(PasswordLetters, 6),
            Balance = balance,
            Org = org,
        };

        _accounts[number] = account;
        AddLog(account, Loc.GetString("dh-bank-log-opened", ("amount", balance)));
        return account;
    }

    /// <summary>Счёт организации (создаётся при первом обращении).</summary>
    public DHBankAccount OrgAccount(DHBankOrg org)
    {
        if (_orgs.TryGetValue(org, out var number) && _accounts.TryGetValue(number, out var existing))
            return existing;

        var (holder, balance) = org switch
        {
            DHBankOrg.Vanguard => (Loc.GetString("dh-bank-org-vanguard"), VanguardStartBalance),
            DHBankOrg.Police => (Loc.GetString("dh-bank-org-police"), PoliceStartBalance),
            _ => (Loc.GetString("dh-bank-org-none"), 0),
        };

        var account = Create(holder, balance, org);
        _orgs[org] = account.Number;
        return account;
    }

    public DHBankAccount? Get(string? number)
    {
        if (number == null)
            return null;

        return _accounts.GetValueOrDefault(Normalize(number));
    }

    public DHBankAccount? FindByLogin(string login)
    {
        foreach (var account in _accounts.Values)
        {
            if (string.Equals(account.Login, login, StringComparison.OrdinalIgnoreCase))
                return account;
        }

        return null;
    }

    public DHBankAccount? TryLogin(string login, string password)
    {
        var account = FindByLogin(login.Trim());
        return account != null && account.Password == password.Trim() ? account : null;
    }

    /// <summary>Номер счёта в виде 0000-0000 (пробелы и лишние знаки убираются).</summary>
    public static string Normalize(string number)
    {
        var digits = new System.Text.StringBuilder();
        foreach (var c in number)
        {
            if (char.IsDigit(c))
                digits.Append(c);
        }

        return digits.Length == 8 ? $"{digits.ToString(0, 4)}-{digits.ToString(4, 4)}" : number.Trim();
    }

    #endregion

    #region Операции

    public void Deposit(DHBankAccount account, int amount, string note)
    {
        if (amount <= 0)
            return;

        account.Balance += amount;
        AddLog(account, Loc.GetString("dh-bank-log-in", ("amount", amount), ("note", note)));
    }

    public bool Withdraw(DHBankAccount account, int amount, string note)
    {
        if (amount <= 0 || account.Balance < amount)
            return false;

        account.Balance -= amount;
        AddLog(account, Loc.GetString("dh-bank-log-out", ("amount", amount), ("note", note)));
        return true;
    }

    /// <summary>Перевод на номер счёта. При ошибке — текст ошибки для окна.</summary>
    public bool Transfer(DHBankAccount from, string toNumber, int amount, out string error)
    {
        error = string.Empty;
        var to = Get(toNumber);
        if (to == null)
        {
            error = Loc.GetString("dh-bank-error-no-account");
            return false;
        }

        if (to == from)
        {
            error = Loc.GetString("dh-bank-error-self");
            return false;
        }

        if (amount <= 0)
        {
            error = Loc.GetString("dh-bank-error-amount");
            return false;
        }

        if (from.Balance < amount)
        {
            error = Loc.GetString("dh-bank-error-funds");
            return false;
        }

        from.Balance -= amount;
        to.Balance += amount;
        AddLog(from, Loc.GetString("dh-bank-log-transfer-out", ("amount", amount), ("to", to.Holder), ("number", to.Number)));
        AddLog(to, Loc.GetString("dh-bank-log-transfer-in", ("amount", amount), ("from", from.Holder), ("number", from.Number)));
        return true;
    }

    private void AddLog(DHBankAccount account, string text)
    {
        account.Log.Add($"[{DHCityClock.DateTimeText(_ticker.RoundDuration())}] {text}");
        if (account.Log.Count > MaxLog)
            account.Log.RemoveAt(0);
    }

    /// <summary>Последние записи истории, новые сверху.</summary>
    public List<string> RecentLog(DHBankAccount account, int count = 15)
    {
        var result = new List<string>();
        for (var i = account.Log.Count - 1; i >= 0 && result.Count < count; i--)
        {
            result.Add(account.Log[i]);
        }

        return result;
    }

    #endregion

    private string RandomString(string letters, int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = letters[_random.Next(letters.Length)];
        }

        return new string(chars);
    }
}
