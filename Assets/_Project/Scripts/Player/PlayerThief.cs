using UnityEngine;
using UtezHorror.Core;
using UtezHorror.Interaction;

namespace UtezHorror.Player
{
    /// <summary>
    /// Drives a theft while the player holds the interact control.
    ///
    /// Held, not tapped. That single choice is what makes the central mechanic feel like work
    /// rather than like every other prompt in the building: you have to stand there, exposed,
    /// facing a machine with your back to the corridor, for as long as it takes.
    ///
    /// **Sprint while working means rushing** — roughly twice as fast and nearly three times as
    /// loud. It reuses a control the player already has, and it puts the game's central bargain
    /// under one thumb: haste buys time and spends safety. The enemy has heard noise since the
    /// first architecture pass; this is what finally gives the player a reason to make it
    /// deliberately.
    ///
    /// The input lives here rather than in <see cref="TheftTarget"/> for the same reason
    /// <see cref="PlayerInteractor"/> reads none: the interaction layer must stay independent of
    /// the control scheme, or touch and keyboard cannot share it.
    /// </summary>
    [RequireComponent(typeof(PlayerInputRouter))]
    [RequireComponent(typeof(PlayerInteractor))]
    [DisallowMultipleComponent]
    public sealed class PlayerThief : MonoBehaviour
    {
        [SerializeField] private PlayerInputRouter input;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private PlayerInventory inventory;

        private TheftTarget active;

        /// <summary>The theft in progress, or null.</summary>
        public TheftTarget Active => active;

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputRouter>();
            if (interactor == null) interactor = GetComponent<PlayerInteractor>();
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
        }

        private void Update()
        {
            // The generator is the same verb against a different object: hold, make noise, wait.
            // Handled here rather than in a component of its own so there is one place that
            // decides what "working on something" means.
            if (interactor.Current is GeneratorRepair generator)
            {
                if (input.InteractHeld) generator.Advance(Time.deltaTime);
                else if (generator.Progress01 <= 0f) GameSignals.RaiseTheftProgressChanged(-1f, null);
                return;
            }

            if (interactor.Current is AssemblyBench bench)
            {
                if (input.InteractHeld) bench.Advance(Time.deltaTime);
                else if (bench.Progress01 <= 0f) GameSignals.RaiseTheftProgressChanged(-1f, null);
                return;
            }

            TheftTarget target = interactor.Current as TheftTarget;

            // Looking away or letting go stops the work. The target keeps what was finished and
            // slowly loses the stage in progress, so stepping back to hide costs something
            // without throwing the whole job away.
            if (target == null || !input.InteractHeld || !target.HasItem)
            {
                if (active != null)
                {
                    active = null;
                    GameSignals.RaiseTheftProgressChanged(-1f, null);
                }
                return;
            }

            active = target;

            bool rushing = input.SprintHeld;
            bool working = target.Advance(Time.deltaTime, rushing);

            GameSignals.RaiseTheftProgressChanged(target.Progress01, Label(target, rushing));

            if (working) return;

            // Finished. The inventory hears about it through the bus like everything else, so
            // this does not need to know what carrying a monitor does to your walking speed.
            active = null;
            GameSignals.RaiseTheftProgressChanged(-1f, null);
        }

        private static string Label(TheftTarget target, bool rushing)
        {
            string verb = target.CurrentStep switch
            {
                TheftStep.Unscrew => "Desatornillando",
                TheftStep.Search => "Buscando",
                TheftStep.Extract => "Extrayendo",
                _ => "Guardando"
            };

            // Naming the risk while it is being taken, rather than after.
            return rushing ? $"{verb} — DEPRISA" : verb;
        }
    }
}
