using System;
using UnityEngine;
using UtezHorror.Core;
using UtezHorror.Core.Noise;

namespace UtezHorror.Interaction
{
    /// <summary>
    /// A machine that might have something worth taking.
    ///
    /// Every PC in the building carries one of these. Most hold nothing: the level places
    /// candidates everywhere and <c>ObjectiveSystem</c> decides at the start of the run which few
    /// are actually loaded, so the same building plays differently and the player has to search
    /// rather than memorise.
    ///
    /// It holds the state of a theft in progress but reads no input — <c>PlayerThief</c> drives
    /// it, the same way <see cref="PlayerInteractor"/> is driven by the input router. That is
    /// what lets a touch control and a keyboard share this without the interaction layer knowing
    /// either exists.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TheftTarget : MonoBehaviour, IInteractable, IStealable
    {
        [SerializeField] private ObjectiveItem item;

        [Tooltip("Which room this is in. ObjectiveSystem uses it to avoid stacking objectives.")]
        [SerializeField] private string room = "";

        [Tooltip("Seconds of not working before the progress on the current stage drains away.")]
        [SerializeField, Min(0f)] private float patienceSeconds = 2.5f;

        [Tooltip("How fast abandoned progress drains, as a multiple of real time.")]
        [SerializeField, Min(0f)] private float drainRate = 0.6f;

        private int stage;
        private float progress;      // seconds into the current stage
        private float idleSeconds;
        private float noiseCooldown;
        private bool taken;

        /// <summary>Raised when the last stage finishes, carrying what was taken.</summary>
        public event Action<ObjectiveItem> Completed;

        public ObjectiveItem Item => item;
        public string Room => room;
        public Vector3 Position => transform.position;
        public bool HasItem => item != null && !taken;
        public bool InProgress => stage > 0 || progress > 0f;

        /// <summary>Which stage is being worked on. Meaningless once taken.</summary>
        public TheftStep CurrentStep =>
            item != null && stage < item.stages.Length ? item.stages[stage].step : TheftStep.Store;

        /// <summary>0..1 across the whole job, not just the current stage. The HUD shows this.</summary>
        public float Progress01
        {
            get
            {
                if (item == null || item.stages.Length == 0) return 0f;

                float done = 0f;
                for (int i = 0; i < stage && i < item.stages.Length; i++) done += item.stages[i].seconds;
                return Mathf.Clamp01((done + progress) / Mathf.Max(item.TotalSeconds, 0.01f));
            }
        }

        public string Prompt => HasItem
            ? (InProgress ? $"Seguir: {item.displayName}" : $"Robar {item.displayName}")
            : "Nada aquí";

        private void OnEnable() => StealableRegistry.Register(this);

        private void OnDisable() => StealableRegistry.Unregister(this);

        /// <summary>Assigned by ObjectiveSystem at the start of the run.</summary>
        public void Load(ObjectiveItem value)
        {
            item = value;
            stage = 0;
            progress = 0f;
            taken = false;
        }

        public void SetRoom(string value) => room = value;

        public bool CanInteract(GameObject interactor) => isActiveAndEnabled;

        /// <summary>
        /// A press alone does nothing. Taking a component is work you hold down, not a button you
        /// tap — that is the difference between the central mechanic feeling elaborate and
        /// feeling like every other prompt in the building.
        /// </summary>
        public void Interact(GameObject interactor) { }

        private void Update()
        {
            if (!HasItem || !InProgress) return;

            idleSeconds += Time.deltaTime;
            if (idleSeconds < patienceSeconds) return;

            // Walking away loses the stage you were on, but not the stages you finished. Losing
            // everything would make interruption unbearable; losing nothing would make hiding
            // free, and hiding has to cost something.
            progress = Mathf.Max(0f, progress - Time.deltaTime * drainRate);
        }

        /// <summary>
        /// Advances the work. Called every frame while the player holds the interact control.
        /// </summary>
        /// <param name="rushing">Sprint held: faster, and much louder.</param>
        /// <returns>True while there is still work left.</returns>
        public bool Advance(float deltaSeconds, bool rushing)
        {
            if (!HasItem) return false;

            idleSeconds = 0f;
            TheftStage current = item.stages[stage];

            progress += deltaSeconds * (rushing ? item.rushSpeed : 1f);
            EmitNoise(current, rushing, deltaSeconds);

            if (progress < current.seconds) return true;

            progress -= current.seconds;
            stage++;

            if (stage < item.stages.Length) return true;

            taken = true;
            GameSignals.RaiseObjectiveCompleted(item.id);
            Completed?.Invoke(item);
            return false;
        }

        /// <summary>
        /// Noise in pulses rather than continuously.
        ///
        /// A continuous emitter would either flood the hearing system every frame or have to be
        /// throttled inside it. Pulses also read better: a screwdriver slipping, a bracket
        /// giving — the enemy gets a series of chances to hear you, and rushing means more of
        /// them, louder.
        /// </summary>
        private void EmitNoise(TheftStage current, bool rushing, float deltaSeconds)
        {
            if (current.noiseRadius <= 0f) return;

            noiseCooldown -= deltaSeconds;
            if (noiseCooldown > 0f) return;

            noiseCooldown = rushing ? 0.35f : 0.8f;

            float radius = current.noiseRadius * (rushing ? item.rushNoise : 1f);
            NoiseSystem.Emit(transform.position, radius, NoiseSource.Minigame);
        }
    }
}
