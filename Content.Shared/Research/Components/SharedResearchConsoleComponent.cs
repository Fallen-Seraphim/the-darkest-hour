using Content.Shared._Goobstation.Research; // DarkestHour
using Robust.Shared.Serialization;

namespace Content.Shared.Research.Components
{
    [NetSerializable, Serializable]
    public enum ResearchConsoleUiKey : byte
    {
        Key,
    }

    [Serializable, NetSerializable]
    public sealed class ConsoleUnlockTechnologyMessage : BoundUserInterfaceMessage
    {
        public string Id;

        public ConsoleUnlockTechnologyMessage(string id)
        {
            Id = id;
        }
    }

    [Serializable, NetSerializable]
    public sealed class ConsoleServerSelectionMessage : BoundUserInterfaceMessage
    {

    }

    [Serializable, NetSerializable]
    public sealed class ResearchConsoleBoundInterfaceState : BoundUserInterfaceState
    {
        public int Points;

        /// <summary>
        /// DarkestHour (из Goob Station / Monolith): все технологии и их доступность — для дерева технологий.
        /// </summary>
        public Dictionary<string, ResearchAvailability> Researches;

        /// <summary>
        /// DarkestHour: цены со скидкой реверс-инжиниринга (только технологии, цена которых отличается от базовой).
        /// </summary>
        public Dictionary<string, int> Costs;

        public ResearchConsoleBoundInterfaceState(int points, Dictionary<string, ResearchAvailability>? researches = null,
            Dictionary<string, int>? costs = null)
        {
            Points = points;
            Researches = researches ?? new();
            Costs = costs ?? new();
        }
    }
}
