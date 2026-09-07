using System.Collections.Generic;
using UnityEngine;

namespace UtezHorror.Core
{
    /// <summary>Something a component can be hidden in. Implemented by the interaction layer.</summary>
    public interface IStealable
    {
        /// <summary>What is in it, or null.</summary>
        ObjectiveItem Item { get; }

        /// <summary>Which room it stands in. Used to spread objectives around the building.</summary>
        string Room { get; }

        Vector3 Position { get; }

        /// <summary>Puts a component in it at the start of a run.</summary>
        void Load(ObjectiveItem item);
    }

    /// <summary>
    /// Every machine in the building that could be holding something, in one place.
    ///
    /// Exists so <see cref="ObjectiveSystem"/> can distribute components without referencing the
    /// interaction assembly. Interaction already depends on Core, so a direct reference the other
    /// way would be a cycle. Same shape as <c>NoiseSystem</c>'s listener registry, and for the
    /// same reason.
    ///
    /// Cleared on subsystem registration, or entering play mode a second time with domain reload
    /// disabled would find the previous run's machines still listed and hide components inside
    /// destroyed objects.
    /// </summary>
    public static class StealableRegistry
    {
        private static readonly List<IStealable> Registered = new();

        public static IReadOnlyList<IStealable> All => Registered;

        public static void Register(IStealable stealable)
        {
            if (stealable != null && !Registered.Contains(stealable)) Registered.Add(stealable);
        }

        public static void Unregister(IStealable stealable) => Registered.Remove(stealable);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Registered.Clear();
    }
}
