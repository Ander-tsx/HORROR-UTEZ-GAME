using System;
using UnityEngine;

namespace UtezHorror.Core
{
    /// <summary>
    /// Process-wide gameplay signals. Systems that must not know about each other
    /// (clock, power, HUD, audio, enemy director) talk through here.
    ///
    /// Static state does not survive a domain reload cleanly when "Enter Play Mode Options"
    /// has reload disabled, so every handler list is cleared on subsystem registration.
    /// </summary>
    public static class GameSignals
    {
        public static event Action<ShiftPhase> PhaseChanged;
        public static event Action<float> ShiftProgressChanged;   // normalised 0..1
        public static event Action<bool> PowerChanged;            // true = mains restored
        public static event Action<float> FlashlightBatteryChanged; // normalised 0..1
        public static event Action<string> ObjectiveCompleted;    // objective id

        /// <summary>Every component is in the bag. The run stops being a hunt and becomes an escape.</summary>
        public static event Action AllObjectivesCollected;

        /// <summary>Theft in progress: 0..1 and the verb to show, or a negative to hide the bar.</summary>
        public static event Action<float, string> TheftProgressChanged;

        /// <summary>The director decided something should happen. See <see cref="EventDirector"/>.</summary>
        public static event Action<DirectedEvent> DirectedEventFired;

        /// <summary>
        /// A battery was picked up, carrying the fraction of a full charge it restores.
        ///
        /// Goes through the bus rather than the pickup calling PlayerFlashlight directly,
        /// because the pickup lives in the Interaction assembly and the torch in Player, and
        /// Player already depends on Interaction. A direct call would be a dependency cycle.
        /// </summary>
        public static event Action<float> BatteryPickedUp;

        /// <summary>
        /// What the player is currently aiming at, as the verb to show, or null for nothing.
        /// Lets the HUD show prompts without the UI assembly knowing the interaction system.
        /// </summary>
        public static event Action<string> InteractionPromptChanged;

        /// <summary>
        /// An enemy reached the player, carrying where it happened.
        ///
        /// What being caught actually costs is still an open design question (see
        /// Docs/Plans/01: total defeat, or a price plus three strikes). The enemy only reports
        /// the event; whatever consumes this decides the consequence, so changing the rule never
        /// means touching AI code.
        /// </summary>
        public static event Action<Vector3> EnemyCaughtPlayer;

        /// <summary>
        /// How exposed the player currently is, 0 (hidden) to 1 (obvious).
        ///
        /// On the bus rather than read off a component so the enemy never has to reference the
        /// player assembly, and so the HUD can show the same number the AI is acting on. A meter
        /// that disagrees with the thing hunting you is worse than no meter.
        /// </summary>
        public static event Action<float> PlayerVisibilityChanged;

        /// <summary>
        /// The player is being killed, carrying where the killer is. Raised at the *start* of
        /// the death sequence so the blood and the shake land on the impact, not after it.
        /// </summary>
        public static event Action<Vector3> PlayerDying;

        /// <summary>The run is over. Carries why.</summary>
        public static event Action<RunOutcome> RunLost;

        /// <summary>The machine is built and the player got out with it.</summary>
        public static event Action RunWon;
        public static event Action<RunOutcome> RunEnded;

        public static void RaisePhaseChanged(ShiftPhase phase) => PhaseChanged?.Invoke(phase);
        public static void RaiseShiftProgressChanged(float t) => ShiftProgressChanged?.Invoke(t);
        public static void RaisePowerChanged(bool on) => PowerChanged?.Invoke(on);
        public static void RaiseFlashlightBatteryChanged(float t) => FlashlightBatteryChanged?.Invoke(t);
        public static void RaiseObjectiveCompleted(string id) => ObjectiveCompleted?.Invoke(id);
        public static void RaiseAllObjectivesCollected() => AllObjectivesCollected?.Invoke();
        public static void RaiseDirectedEvent(DirectedEvent evt) => DirectedEventFired?.Invoke(evt);
        public static void RaiseTheftProgressChanged(float progress01, string label) =>
            TheftProgressChanged?.Invoke(progress01, label);
        public static void RaiseBatteryPickedUp(float amount01) => BatteryPickedUp?.Invoke(amount01);
        public static void RaiseInteractionPromptChanged(string prompt) => InteractionPromptChanged?.Invoke(prompt);
        public static void RaiseEnemyCaughtPlayer(Vector3 where) => EnemyCaughtPlayer?.Invoke(where);
        public static void RaisePlayerVisibilityChanged(float exposure01) => PlayerVisibilityChanged?.Invoke(exposure01);
        public static void RaisePlayerDying(Vector3 killer) => PlayerDying?.Invoke(killer);
        public static void RaiseRunLost(RunOutcome outcome) => RunLost?.Invoke(outcome);
        public static void RaiseRunWon() => RunWon?.Invoke();
        public static void RaiseRunEnded(RunOutcome outcome) => RunEnded?.Invoke(outcome);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            PhaseChanged = null;
            ShiftProgressChanged = null;
            PowerChanged = null;
            FlashlightBatteryChanged = null;
            ObjectiveCompleted = null;
            AllObjectivesCollected = null;
            DirectedEventFired = null;
            TheftProgressChanged = null;
            BatteryPickedUp = null;
            InteractionPromptChanged = null;
            EnemyCaughtPlayer = null;
            PlayerVisibilityChanged = null;
            PlayerDying = null;
            RunLost = null;
            RunWon = null;
            RunEnded = null;
        }
    }

    public enum RunOutcome
    {
        Survived,
        TimeExpired,
        Caught
    }
}
