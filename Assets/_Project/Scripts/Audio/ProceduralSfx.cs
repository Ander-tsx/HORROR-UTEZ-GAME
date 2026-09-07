using UnityEngine;

namespace UtezHorror.Audio
{
    /// <summary>
    /// Sound effects synthesised in code, with no audio files at all.
    ///
    /// Why bother instead of downloading clips: the game runs everything through
    /// <see cref="RetroAudioFilter"/> at 11 kHz and 8 bits, so any detail finer than that is
    /// thrown away before it reaches the player. Recorded effects would be paying for fidelity
    /// that gets destroyed on the way out. Short bursts of shaped noise survive that treatment
    /// intact — which is exactly why the era's sound was made this way.
    ///
    /// Every clip here is noise shaped by an envelope and a one-pole filter. That is enough for
    /// short transients — footsteps, doors, impacts — and not enough for anything the player
    /// hears continuously. A synthesised room tone was tried and cut: sustained filtered noise
    /// reads as a broken speaker rather than as a room, because the ear identifies noise as
    /// noise within a second or two no matter how it is shaped. Ambience, voice and music need
    /// real assets.
    /// </summary>
    public static class ProceduralSfx
    {
        /// <summary>Matches the crusher's target rate: generating finer detail would be wasted.</summary>
        public const int SampleRate = 11025;

        private static readonly System.Random Rng = new(20260906);

        /// <summary>A footstep: a short, dull thud with a little grit on the front.</summary>
        public static AudioClip Footstep(string name, float pitch, float brightness, float seconds = 0.16f)
        {
            int count = Mathf.RoundToInt(SampleRate * seconds);
            var data = new float[count];

            // One-pole low pass. `brightness` is how much of the raw noise survives it, which is
            // the difference between a boot on tile and a shoe on dust.
            float previous = 0f;
            float cutoff = Mathf.Clamp01(brightness);

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;

                // Sharp attack, exponential decay: anything slower reads as a whoosh, not a step.
                float envelope = Mathf.Exp(-t * 22f) * Mathf.Min(1f, t * 60f);

                float noise = (float)(Rng.NextDouble() * 2.0 - 1.0);
                previous = Mathf.Lerp(previous, noise, cutoff);

                // A low sine under the noise gives the step a body. Without it a footstep is a
                // hiss, and the player hears static rather than weight.
                float body = Mathf.Sin(t * Mathf.PI * 2f * (55f * pitch)) * 0.6f;

                data[i] = (previous * 0.7f + body * 0.5f) * envelope * 0.55f;
            }

            return FromSamples(name, data);
        }

        /// <summary>Metal on metal: a battery being lifted off a desk.</summary>
        public static AudioClip Pickup(string name)
        {
            const float seconds = 0.22f;
            int count = Mathf.RoundToInt(SampleRate * seconds);
            var data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float envelope = Mathf.Exp(-t * 14f);

                // Two detuned partials read as a small metal object; one alone reads as a beep.
                float tone = Mathf.Sin(t * Mathf.PI * 2f * 880f) * 0.5f
                           + Mathf.Sin(t * Mathf.PI * 2f * 1310f) * 0.3f;
                float grit = (float)(Rng.NextDouble() * 2.0 - 1.0) * 0.25f * Mathf.Exp(-t * 40f);

                data[i] = (tone + grit) * envelope * 0.4f;
            }

            return FromSamples(name, data);
        }

        /// <summary>The torch switch. Tiny, dry, and immediate feedback that the key worked.</summary>
        public static AudioClip Click(string name)
        {
            const float seconds = 0.06f;
            int count = Mathf.RoundToInt(SampleRate * seconds);
            var data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float envelope = Mathf.Exp(-t * 60f);
                data[i] = (float)(Rng.NextDouble() * 2.0 - 1.0) * envelope * 0.5f;
            }

            return FromSamples(name, data);
        }

        private static AudioClip FromSamples(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, stream: false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
