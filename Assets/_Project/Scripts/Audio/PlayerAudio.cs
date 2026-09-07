using UnityEngine;
using UtezHorror.Core;
using UtezHorror.Player;

namespace UtezHorror.Audio
{
    /// <summary>
    /// Everything the player hears themselves do: footsteps, the torch switch, picking things up.
    ///
    /// There was a synthesised room tone under all of it and it has been removed. Filtered white
    /// noise with a slow swell does not read as a room, it reads as a broken speaker — the ear
    /// identifies noise as noise almost immediately, and no amount of shaping fixes that. Real
    /// ambience is recorded or designed, not generated, so this stays silent until there is an
    /// actual asset for it. Synthesis works for short transients like footsteps and fails for
    /// anything the player hears continuously.
    ///
    /// Sits on the player rather than being scattered across the systems that cause the sounds,
    /// so those systems stay silent in the literal sense — <see cref="FirstPersonController"/>
    /// raises a step event and knows nothing about audio, and the battery pickup does not carry
    /// an AudioSource of its own.
    ///
    /// Clips are built at Awake by <see cref="ProceduralSfx"/>, so the project still ships with
    /// no audio files. Everything passes through the retro filter on the listener afterwards.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAudio : MonoBehaviour
    {
        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.5f;

        [Tooltip("Random pitch spread per step. Identical repeats are what make footsteps grating.")]
        [SerializeField, Range(0f, 0.4f)] private float pitchJitter = 0.12f;

        private AudioSource steps;
        private AudioSource oneShots;

        private AudioClip[] footsteps;
        private AudioClip crouchStep;
        private AudioClip click;
        private AudioClip pickup;

        private FirstPersonController movement;
        private PlayerFlashlight torch;

        private void Awake()
        {
            movement = GetComponent<FirstPersonController>();
            torch = GetComponent<PlayerFlashlight>();

            steps = CreateSource("Steps", loop: false, volume: footstepVolume);
            oneShots = CreateSource("OneShots", loop: false, volume: 0.6f);

            // Three variants, cycled at random: the ear picks up an exactly repeating sample
            // within about four steps, and once it does the walk sounds fake.
            footsteps = new[]
            {
                ProceduralSfx.Footstep("Step_A", 1.00f, 0.55f),
                ProceduralSfx.Footstep("Step_B", 0.92f, 0.62f),
                ProceduralSfx.Footstep("Step_C", 1.09f, 0.48f)
            };
            crouchStep = ProceduralSfx.Footstep("Step_Crouch", 0.85f, 0.30f, seconds: 0.11f);
            click = ProceduralSfx.Click("TorchClick");
            pickup = ProceduralSfx.Pickup("Pickup");
        }

        private void OnEnable()
        {
            if (movement != null) movement.Stepped += OnStepped;
            GameSignals.BatteryPickedUp += OnBatteryPickedUp;
        }

        private void OnDisable()
        {
            if (movement != null) movement.Stepped -= OnStepped;
            GameSignals.BatteryPickedUp -= OnBatteryPickedUp;
        }

        private void Update()
        {
            // Polled rather than event-driven because the torch has no toggle event, and adding
            // one for a click would put audio concerns into the torch.
            if (torch == null) return;
            if (torch.IsOn == wasTorchOn) return;

            wasTorchOn = torch.IsOn;
            oneShots.PlayOneShot(click, 0.5f);
        }

        private bool wasTorchOn;

        private void OnStepped(Stance stance)
        {
            AudioClip clip = stance == Stance.Crouching
                ? crouchStep
                : footsteps[Random.Range(0, footsteps.Length)];

            steps.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);

            // A sprint lands harder than a walk, and a crouch barely lands at all. The mix
            // carries the same information the noise radius does, so what the player hears
            // matches what the enemy hears.
            float volume = stance switch
            {
                Stance.Sprinting => 1f,
                Stance.Crouching => 0.30f,
                _ => 0.65f
            };

            steps.PlayOneShot(clip, volume);
        }

        private void OnBatteryPickedUp(float amount) => oneShots.PlayOneShot(pickup, 0.7f);

        private AudioSource CreateSource(string name, bool loop, float volume)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = volume;
            // 2D: these are sounds the player makes, so they should not pan or attenuate with
            // the listener's own movement.
            source.spatialBlend = 0f;
            return source;
        }
    }
}
