using Content.Shared._DarkestHour.City;
using Content.Shared._DarkestHour.Uprising;

namespace Content.Server._DarkestHour.Uprising;

/// <summary>
/// Задания LA-V (решение автора 2026-11-12): продать N штук корпоративного товара. Засчитываются чеки чековой
/// машинки, где продавец — LA-V (счёт корпорации или личный счёт сотрудника LA-V), а покупатель — не из LA-V.
/// </summary>
public sealed partial class DHTaskSystem
{
    private bool StartSales(ActiveTask task)
    {
        if (task.Proto.Goods.Count == 0)
            return false;

        var goods = task.Proto.Goods[_random.Next(task.Proto.Goods.Count)];
        task.GoodsProto = goods;
        task.Remaining = _random.Next(task.Proto.CountMin, task.Proto.CountMax + 1);
        var name = _proto.TryIndex(goods, out var proto) ? proto.Name : goods.Id;
        task.Text = Loc.GetString("dh-task-sales-text", ("goods", name), ("count", task.Remaining));
        return true;
    }

    private bool IsVanguardAccount(string number)
    {
        if (number == _bank.OrgAccount(DHBankOrg.Vanguard).Number)
            return true;

        return _bank.OwnerOf(number) is { } owner &&
               TryComp<DHUprisingMemberComponent>(owner, out var member) && member.Side == DHUprisingSide.Vanguard;
    }

    private void OnCheque(DHChequeIssuedEvent ev)
    {
        if (!_sides.TryGetValue(DHTaskSide.Vanguard, out var state) || state.Active is not { } task ||
            task.Proto.Kind != DHTaskKind.Sales || ev.GoodsProto != task.GoodsProto ||
            !IsVanguardAccount(ev.SellerNumber) || IsVanguardAccount(ev.BuyerNumber))
        {
            return;
        }

        task.Remaining--;
        if (task.Remaining <= 0)
            Finish(DHTaskSide.Vanguard, task, true, Loc.GetString("dh-task-log-done", ("name", Loc.GetString(task.Proto.Name))));
        else
            UpdateConsoles(DHTaskSide.Vanguard);
    }
}
