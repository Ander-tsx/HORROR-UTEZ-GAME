using UnityEngine;
using UtezHorror.Core.Noise;

namespace UtezHorror.Enemies
{
    /// <summary>
    /// Turns heard noise into a point of interest. Deliberately dumb: it stores what it
    /// heard and how loud, and lets the state machine decide whether that is worth walking
    /// over to. Keeping the decision out of here means tuning happens in
    /// <see cref="EnemyConfig"/>, not in perception code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHearing : MonoBehaviour, INoiseListener
    {
        [SerializeField] private EnemyConfig config;

        [Tooltip("Noises quieter than this at the point of hearing are ignored.")]
        [SerializeField, Range(0f, 1f)] private float loudnessThreshold = 0.15f;

        public Vector3 HearingPosition => transform.position;

        public float HearingMultiplier => config != null ? config.hearingMultiplier : 1f;

        /// <summary>True while there is an unhandled noise worth investigating.</summary>
        public bool HasPointOfInterest { get; private set; }

        public Vector3 PointOfInterest { get; private set; }

        /// <summary>Loudness of the noise that produced the current point of interest.</summary>
        public float PointOfInterestLoudness { get; private set; }

        private void OnEnable() => NoiseSystem.Register(this);

        private void OnDisable()
        {
            NoiseSystem.Unregister(this);
            ClearPointOfInterest();
        }

        public void OnNoiseHeard(in NoiseEvent noise)
        {
            if (noise.Loudness < loudnessThreshold) return;

            // A louder noise always wins; a quieter one never overwrites a fresher lead.
            if (HasPointOfInterest && noise.Loudness < PointOfInterestLoudness) return;

            HasPointOfInterest = true;
            PointOfInterest = noise.Position;
            PointOfInterestLoudness = noise.Loudness;
        }

        public void ClearPointOfInterest()
        {
            HasPointOfInterest = false;
            PointOfInterestLoudness = 0f;
        }
    }
}
