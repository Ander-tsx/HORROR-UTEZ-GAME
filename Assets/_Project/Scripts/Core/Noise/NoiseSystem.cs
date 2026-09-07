using System.Collections.Generic;
using UnityEngine;
using UtezHorror.Utils;

namespace UtezHorror.Core.Noise
{
    /// <summary>
    /// Connects loud player actions to whoever can hear them. This is what makes rushing a
    /// theft dangerous instead of merely slow: the minigames and the enemies share no other
    /// coupling.
    ///
    /// Deliberately not physics-trigger based — a noise is a one-shot query over a handful
    /// of listeners, which is far cheaper than keeping overlap spheres alive.
    /// </summary>
    public static class NoiseSystem
    {
        /// <summary>Fraction of the radius that survives passing through one wall.</summary>
        public const float WallAttenuation = 0.45f;

        private static readonly List<INoiseListener> Listeners = new(16);
        private static readonly List<INoiseListener> DispatchBuffer = new(16);

        public static int ListenerCount => Listeners.Count;

        public static void Register(INoiseListener listener)
        {
            if (listener != null && !Listeners.Contains(listener))
                Listeners.Add(listener);
        }

        public static void Unregister(INoiseListener listener) => Listeners.Remove(listener);

        /// <summary>
        /// Emit a noise of <paramref name="radius"/> metres. Occluded listeners hear a
        /// shortened version rather than nothing, so walls muffle instead of soundproofing.
        /// </summary>
        public static void Emit(Vector3 position, float radius, NoiseSource source)
        {
            if (radius <= 0f || Listeners.Count == 0) return;

            // Copy first: a listener may unregister itself from inside its own callback.
            DispatchBuffer.Clear();
            DispatchBuffer.AddRange(Listeners);

            for (int i = 0; i < DispatchBuffer.Count; i++)
            {
                INoiseListener listener = DispatchBuffer[i];
                if (listener == null) continue;

                Vector3 to = listener.HearingPosition - position;
                float distance = to.magnitude;
                float reach = radius * Mathf.Max(0f, listener.HearingMultiplier);

                // Cheap reject before spending a raycast.
                if (distance > reach) continue;

                if (distance > 0.01f &&
                    Physics.Raycast(position, to / distance, distance,
                                    GameLayers.OcclusionMask, QueryTriggerInteraction.Ignore))
                {
                    reach *= WallAttenuation;
                    if (distance > reach) continue;
                }

                float loudness = reach <= 0f ? 0f : Mathf.Clamp01(1f - distance / reach);
                listener.OnNoiseHeard(new NoiseEvent(position, reach, source, loudness));
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Listeners.Clear();
            DispatchBuffer.Clear();
        }
    }
}
