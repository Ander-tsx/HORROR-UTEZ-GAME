using UnityEngine;

namespace UtezHorror.Audio
{
    /// <summary>
    /// Crushes everything the player hears down to period specification: a low sample rate and a
    /// small number of bits, optionally collapsed to mono.
    ///
    /// Sits on the <see cref="AudioListener"/>, so it catches every sound in the game without any
    /// per-clip setup, and it works with no audio assets existing yet. That is deliberate: the
    /// filter needs to exist *before* sound is produced, because it changes how sound should be
    /// authored. There is no point buying or recording 48 kHz cinematic effects when the output
    /// is 11 kHz at 8 bits — the detail is thrown away and only the mud survives. Pick sources
    /// with strong, simple shapes instead.
    ///
    /// The processing is the real 1990s chain, in order:
    ///   1. sample-and-hold, which is decimation without a reconstruction filter, so the aliasing
    ///      folds back into the audible range — that harsh, gritty edge *is* the aliasing;
    ///   2. quantisation to a few bits, which adds the crunch and a noise floor that rises with
    ///      quiet material, exactly like a real converter of the era.
    /// </summary>
    [RequireComponent(typeof(AudioListener))]
    [DisallowMultipleComponent]
    public sealed class RetroAudioFilter : MonoBehaviour
    {
        [Tooltip("Effective sample rate in Hz. The PS1's CD audio was 44.1 kHz but its sample " +
                 "playback ran far lower; 11 kHz is the classic sound-effect rate.")]
        [SerializeField, Range(2000f, 44100f)] private float sampleRate = 11025f;

        [Tooltip("Bits per sample. 8 is crunchy, 12 is subtle, 4 is destroyed.")]
        [SerializeField, Range(2, 16)] private int bitDepth = 8;

        [Tooltip("Collapse to mono. Most sound of the era was mono, and it makes the stereo " +
                 "field of a modern source stop giving the age away.")]
        [SerializeField] private bool forceMono = true;

        [Tooltip("Dry/wet. Below 1 the crush is blended with the clean signal.")]
        [SerializeField, Range(0f, 1f)] private float amount = 1f;

        [Tooltip("Turn off to hear the game clean, for comparison or for debugging a mix.")]
        [SerializeField] private bool active = true;

        // Hold state has to persist between buffers or a click appears at every buffer boundary.
        private float holdPhase;
        private float[] heldSample = new float[8];

        /// <summary>
        /// Cached because <c>AudioSettings.outputSampleRate</c> is a main-thread API and
        /// <see cref="OnAudioFilterRead"/> runs on the audio thread. Reading it there throws
        /// "GetSampleRate can only be called from the main thread" — which surfaced as an
        /// unrelated-looking exception during scene loading in the level tests.
        /// </summary>
        private float outputSampleRate = 48000f;

        public bool Active
        {
            get => active;
            set => active = value;
        }

        private void OnEnable()
        {
            outputSampleRate = Mathf.Max(1f, AudioSettings.outputSampleRate);
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        private void OnDisable() => AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;

        private void OnAudioConfigurationChanged(bool deviceWasChanged) =>
            outputSampleRate = Mathf.Max(1f, AudioSettings.outputSampleRate);

        /// <summary>
        /// Runs on the audio thread, not the main thread. Nothing here may touch the Unity API,
        /// allocate, or read anything the main thread writes without care — a stall here is an
        /// audible dropout, not a dropped frame.
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!active || amount <= 0f || channels <= 0) return;

            if (heldSample.Length < channels) heldSample = new float[channels];

            float step = Mathf.Clamp01(sampleRate / outputSampleRate);
            if (step <= 0f) return;

            float levels = Mathf.Pow(2f, bitDepth) - 1f;
            float wet = Mathf.Clamp01(amount);

            for (int i = 0; i < data.Length; i += channels)
            {
                // Sample-and-hold: only advance the held value when a whole output sample of the
                // lower rate has gone by. Everything between repeats the last value.
                holdPhase += step;
                bool newSample = holdPhase >= 1f;
                if (newSample) holdPhase -= Mathf.Floor(holdPhase);

                if (newSample)
                {
                    if (forceMono)
                    {
                        float sum = 0f;
                        for (int c = 0; c < channels; c++) sum += data[i + c];
                        float mono = sum / channels;
                        for (int c = 0; c < channels; c++) heldSample[c] = mono;
                    }
                    else
                    {
                        for (int c = 0; c < channels; c++) heldSample[c] = data[i + c];
                    }

                    for (int c = 0; c < channels; c++)
                    {
                        // Quantise across -1..1, hence the shift into 0..1 and back.
                        float normalised = Mathf.Clamp(heldSample[c], -1f, 1f) * 0.5f + 0.5f;
                        float crushed = Mathf.Round(normalised * levels) / levels;
                        heldSample[c] = crushed * 2f - 1f;
                    }
                }

                for (int c = 0; c < channels; c++)
                    data[i + c] = Mathf.Lerp(data[i + c], heldSample[c], wet);
            }
        }
    }
}
