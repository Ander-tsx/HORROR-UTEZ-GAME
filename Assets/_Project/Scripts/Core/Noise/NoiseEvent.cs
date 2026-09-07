using UnityEngine;

namespace UtezHorror.Core.Noise
{
    public enum NoiseSource
    {
        Footstep,
        Sprint,
        Interaction,
        Minigame,
        Impact,
        Scripted
    }

    /// <summary>
    /// One audible occurrence in the world. <see cref="Radius"/> is expressed in metres:
    /// a listener standing further away than that simply does not hear it.
    /// </summary>
    public readonly struct NoiseEvent
    {
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly NoiseSource Source;

        /// <summary>How loud it landed for the specific listener, 0..1. 1 = right on top of it.</summary>
        public readonly float Loudness;

        public NoiseEvent(Vector3 position, float radius, NoiseSource source, float loudness)
        {
            Position = position;
            Radius = radius;
            Source = source;
            Loudness = loudness;
        }
    }

    public interface INoiseListener
    {
        Vector3 HearingPosition { get; }

        /// <summary>Scales every incoming noise radius. 1 = normal hearing, 2 = hears twice as far.</summary>
        float HearingMultiplier { get; }

        void OnNoiseHeard(in NoiseEvent noise);
    }
}
