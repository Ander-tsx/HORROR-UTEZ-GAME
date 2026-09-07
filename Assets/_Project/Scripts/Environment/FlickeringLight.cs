using UnityEngine;
using UtezHorror.Core;

namespace UtezHorror.Environment
{
    /// <summary>
    /// A failing fluorescent fixture.
    ///
    /// These are not here to light the building — the torch does that. They exist to make the
    /// dark feel occupied: a tube that stutters twice and dies at the far end of a corridor
    /// reads as a place that is failing, and it costs the player nothing but nerve. Most
    /// fixtures in the level have no Light at all; the few that do spend most of their time out.
    /// </summary>
    [RequireComponent(typeof(Light))]
    [DisallowMultipleComponent]
    public sealed class FlickeringLight : MonoBehaviour
    {
        private enum State { Stable, Flickering, Out }

        [Header("Durations (seconds, random in range)")]
        [SerializeField] private Vector2 stableDuration = new(0.4f, 2f);
        [SerializeField] private Vector2 flickerDuration = new(0.2f, 1.4f);
        [SerializeField] private Vector2 outDuration = new(10f, 45f);

        [Tooltip("Chance that a flicker burst collapses into a full outage instead of recovering.")]
        [SerializeField, Range(0f, 1f)] private float outChance = 0.85f;

        [Tooltip("Chance that a fixture is already out when the scene starts.")]
        [SerializeField, Range(0f, 1f)] private float startDeadChance = 0.7f;

        [Header("Flicker look")]
        [SerializeField, Range(0f, 1f)] private float flickerFloor = 0.02f;
        [SerializeField, Min(1f)] private float flickerSpeed = 24f;

        [Tooltip("Emissive tube renderer, switched off with the light so a dead fixture stops glowing.")]
        [SerializeField] private Renderer glow;

        private Light fixture;
        private float baseIntensity;
        private float noiseOffset;
        private State state;
        private float stateEndsAt;
        private bool mainsOn = true;

        private void Awake()
        {
            fixture = GetComponent<Light>();
            baseIntensity = fixture.intensity;
            noiseOffset = Random.Range(0f, 500f);   // decorrelates fixtures from each other
        }

        private void OnEnable()
        {
            GameSignals.PowerChanged += OnPowerChanged;

            // Random phase so a corridor never blinks in unison.
            if (Random.value < startDeadChance) EnterState(State.Out);
            else EnterState(State.Stable, Random.value);
        }

        private void OnDisable() => GameSignals.PowerChanged -= OnPowerChanged;

        private void OnPowerChanged(bool on)
        {
            mainsOn = on;
            if (!on) SetLit(false);
            else EnterState(State.Out);   // mains back does not mean this tube recovers at once
        }

        private void Update()
        {
            if (!mainsOn) return;

            if (Time.time >= stateEndsAt) Advance();
            if (state != State.Flickering) return;

            float n = Mathf.PerlinNoise(Time.time * flickerSpeed, noiseOffset);
            SetLit(n > 0.4f);
            fixture.intensity = baseIntensity * Mathf.Lerp(flickerFloor, 1f, n);
        }

        private void Advance()
        {
            State next = state switch
            {
                State.Stable => State.Flickering,
                State.Flickering => Random.value < outChance ? State.Out : State.Stable,
                _ => State.Flickering   // a dead tube stutters before it comes back, if it does
            };
            EnterState(next);
        }

        private void EnterState(State next, float elapsedFraction = 0f)
        {
            state = next;

            switch (next)
            {
                case State.Stable:
                    SetLit(true);
                    fixture.intensity = baseIntensity;
                    stateEndsAt = Time.time + RandomIn(stableDuration) * (1f - elapsedFraction);
                    break;

                case State.Flickering:
                    SetLit(true);
                    stateEndsAt = Time.time + RandomIn(flickerDuration);
                    break;

                case State.Out:
                    SetLit(false);
                    stateEndsAt = Time.time + RandomIn(outDuration);
                    break;
            }
        }

        private void SetLit(bool lit)
        {
            fixture.enabled = lit;
            if (glow != null) glow.enabled = lit;
        }

        private static float RandomIn(Vector2 range) => Random.Range(range.x, range.y);
    }
}
