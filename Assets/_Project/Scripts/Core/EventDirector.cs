using System.Collections.Generic;
using UnityEngine;

namespace UtezHorror.Core
{
    /// <summary>What the director can do to the player.</summary>
    public enum DirectedEvent
    {
        /// <summary>The mains go out. The heaviest thing in the deck.</summary>
        Blackout,

        /// <summary>The professor is told roughly where you are. It does not see you; it comes to look.</summary>
        Hunt,

        /// <summary>Lights stutter across the building. Costs nothing and buys a lot.</summary>
        Flicker
    }

    /// <summary>
    /// Decides when something happens, and refuses to be random about it.
    ///
    /// Docs/Plans/01 is explicit that <c>Random.Range</c> gives one of two bad runs: eight
    /// minutes where nothing happens, or three events on top of each other. Both are failures of
    /// the same thing — nothing is tracking how much pressure the player is already under.
    ///
    /// So there is a **tension budget**. It refills over time and every event spends from it, so
    /// a heavy event buys a long quiet afterwards and a cheap one does not. On top of that:
    ///
    /// - a **minimum gap**, so two things never land together;
    /// - a **maximum gap**, so silence never becomes the whole game;
    /// - a **per-event cooldown**, so the same trick is never played twice in a row;
    /// - **nothing fires during a theft.** Interrupting the one moment the player has committed
    ///   to reads as a bug, not as tension, and it is the single rule most worth keeping.
    ///
    /// Frequency rises with the shift: nothing during Arrival, one at a time in Fading, regular
    /// in Dark, back to back in Curfew.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EventDirector : MonoBehaviour
    {
        [Header("Rhythm")]
        [Tooltip("Never less than this between two events, in seconds.")]
        [SerializeField, Min(1f)] private float minimumGap = 35f;

        [Tooltip("Never more than this without something happening, once past Arrival.")]
        [SerializeField, Min(5f)] private float maximumGap = 150f;

        [Header("Tension budget")]
        [Tooltip("Budget refilled per second. Events cost from it, so a heavy one buys quiet.")]
        [SerializeField, Min(0.01f)] private float refillPerSecond = 1f;

        [SerializeField, Min(1f)] private float budgetCeiling = 120f;

        [Header("Costs")]
        [SerializeField, Min(1f)] private float blackoutCost = 100f;
        [SerializeField, Min(1f)] private float huntCost = 55f;
        [SerializeField, Min(1f)] private float flickerCost = 20f;

        [Header("Cooldowns")]
        [SerializeField, Min(0f)] private float blackoutCooldown = 180f;
        [SerializeField, Min(0f)] private float huntCooldown = 70f;
        [SerializeField, Min(0f)] private float flickerCooldown = 45f;

        private readonly Dictionary<DirectedEvent, float> nextAllowed = new();
        private float budget;
        private float lastEventTime;
        private bool theftInProgress;

        private void OnEnable()
        {
            GameSignals.TheftProgressChanged += OnTheftProgress;
            budget = budgetCeiling * 0.5f;
            lastEventTime = Time.time;
        }

        private void OnDisable() => GameSignals.TheftProgressChanged -= OnTheftProgress;

        /// <summary>A negative progress means the player let go; anything else means they are working.</summary>
        private void OnTheftProgress(float progress01, string label) => theftInProgress = progress01 >= 0f;

        private void Update()
        {
            GameClock clock = GameClock.Instance;
            if (clock == null || !clock.IsRunning) return;

            budget = Mathf.Min(budgetCeiling, budget + refillPerSecond * Time.deltaTime * Pace(clock.Phase));

            if (clock.Phase == ShiftPhase.Arrival) return;

            float since = Time.time - lastEventTime;
            if (since < minimumGap) return;

            // Committed players are left alone, but not forever: past the maximum gap the rule
            // is dropped, or a player who never stops working is never threatened at all.
            if (theftInProgress && since < maximumGap) return;

            bool overdue = since >= maximumGap;
            DirectedEvent? choice = Choose(clock.Phase, overdue);
            if (choice == null) return;

            Fire(choice.Value);
        }

        /// <summary>How fast the budget refills, by phase. This is the difficulty curve.</summary>
        private static float Pace(ShiftPhase phase) => phase switch
        {
            ShiftPhase.Arrival => 0f,
            ShiftPhase.Fading => 0.7f,
            ShiftPhase.Dark => 1.2f,
            _ => 1.8f
        };

        private DirectedEvent? Choose(ShiftPhase phase, bool overdue)
        {
            // Ordered heaviest first: when there is budget for a blackout, a blackout is the most
            // interesting thing that can happen.
            var deck = new List<(DirectedEvent evt, float cost)>
            {
                (DirectedEvent.Blackout, blackoutCost),
                (DirectedEvent.Hunt, huntCost),
                (DirectedEvent.Flicker, flickerCost)
            };

            // The mains going out this early would spend the run's best card before the player
            // understands the building.
            if (phase == ShiftPhase.Fading) deck.RemoveAll(d => d.evt == DirectedEvent.Blackout);

            foreach ((DirectedEvent evt, float cost) in deck)
            {
                if (Time.time < Allowed(evt)) continue;
                if (!overdue && budget < cost) continue;
                if (evt == DirectedEvent.Blackout && PowerSystem.Instance is { CanCutPower: false }) continue;

                return evt;
            }

            // Overdue with nothing affordable: a flicker always beats another minute of nothing.
            return overdue && Time.time >= Allowed(DirectedEvent.Flicker) ? DirectedEvent.Flicker : null;
        }

        private float Allowed(DirectedEvent evt) => nextAllowed.GetValueOrDefault(evt, 0f);

        private void Fire(DirectedEvent evt)
        {
            float cost = evt switch
            {
                DirectedEvent.Blackout => blackoutCost,
                DirectedEvent.Hunt => huntCost,
                _ => flickerCost
            };

            float cooldown = evt switch
            {
                DirectedEvent.Blackout => blackoutCooldown,
                DirectedEvent.Hunt => huntCooldown,
                _ => flickerCooldown
            };

            budget = Mathf.Max(0f, budget - cost);
            lastEventTime = Time.time;
            nextAllowed[evt] = Time.time + cooldown;

            if (evt == DirectedEvent.Blackout) PowerSystem.Instance?.CutPower();

            GameSignals.RaiseDirectedEvent(evt);
            Debug.Log($"[Director] {evt}. Budget left {budget:0}.");
        }
    }
}
