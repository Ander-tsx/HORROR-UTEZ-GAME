using System.Collections.Generic;
using UnityEngine;
using UtezHorror.Core;

namespace UtezHorror.Player
{
    /// <summary>
    /// What the player is carrying, and what it costs them to carry it.
    ///
    /// The weight is the point. Docs/Plans/01 gives the cabinet the note "te ralentiza mientras
    /// lo cargas; no puedes correr" — that is the whole reason a bulky component is different
    /// from a small one, in a game where the professor is faster than a sprint. Taking the
    /// monitor is a decision about the rest of the run, not a pickup.
    ///
    /// Listens on the bus rather than being told directly, so <see cref="PlayerThief"/> never has
    /// to know that carrying a cabinet changes how you walk.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInventory : MonoBehaviour
    {
        private readonly List<ObjectiveItem> carried = new();

        public IReadOnlyList<ObjectiveItem> Carried => carried;

        /// <summary>Walk speed multiplier from everything being carried. 1 while empty-handed.</summary>
        public float SpeedMultiplier { get; private set; } = 1f;

        /// <summary>False once something in the bag is too awkward to run with.</summary>
        public bool CanSprint { get; private set; } = true;

        private void OnEnable() => GameSignals.ObjectiveCompleted += OnCollected;

        private void OnDisable() => GameSignals.ObjectiveCompleted -= OnCollected;

        private void OnCollected(string id)
        {
            ObjectiveSystem objectives = ObjectiveSystem.Instance;
            if (objectives == null) return;

            foreach (ObjectiveItem item in objectives.Required)
            {
                if (item.id != id) continue;
                carried.Add(item);
                break;
            }

            Recalculate();
        }

        private void Recalculate()
        {
            SpeedMultiplier = 1f;
            CanSprint = true;

            foreach (ObjectiveItem item in carried)
            {
                if (!item.encumbers) continue;

                // Multiplied rather than taking the worst one: two bulky components should be
                // worse than one, or the second is free and the player just takes everything.
                SpeedMultiplier *= item.carrySpeed;
                if (!item.allowsSprint) CanSprint = false;
            }

            // A floor, so a full bag is punishing rather than unplayable. Crawling across a
            // building that takes two minutes to cross is not tension, it is waiting.
            SpeedMultiplier = Mathf.Max(SpeedMultiplier, 0.35f);
        }
    }
}
