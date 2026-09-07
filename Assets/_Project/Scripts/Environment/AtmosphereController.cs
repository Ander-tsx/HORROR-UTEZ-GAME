using UnityEngine;
using UtezHorror.Core;
using UtezHorror.Utils;

namespace UtezHorror.Environment
{
    /// <summary>
    /// Drives fog and ambient light from where the player is and how far the shift has run.
    ///
    /// Two atmospheres, not one. Indoors is near-black with almost no fog: the torch is the only
    /// light and fog would only wash out its beam. Outdoors is thick, visible, grey-green fog —
    /// which is simultaneously the Silent Hill signature, the draw-distance cap that lets a wood
    /// exist on a phone, and the reason something can step out of nowhere. Crossing a doorway is
    /// meant to read as changing worlds.
    ///
    /// Both ends darken as the shift advances, so the clock is something the player can see
    /// rather than only read off the HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AtmosphereController : MonoBehaviour
    {
        [Header("Atmospheres — defaults come from AtmospherePresets")]
        [SerializeField] private Atmosphere interiorEarly = AtmospherePresets.InteriorEarly;
        [SerializeField] private Atmosphere interiorLate = AtmospherePresets.InteriorLate;
        [SerializeField] private Atmosphere exteriorEarly = AtmospherePresets.ExteriorEarly;
        [SerializeField] private Atmosphere exteriorLate = AtmospherePresets.ExteriorLate;

        [Header("Detection")]
        [Tooltip("Metres to look upward for a ceiling before calling the player indoors.")]
        [SerializeField, Min(1f)] private float ceilingProbeHeight = 12f;

        [Tooltip("Seconds for one atmosphere to become the other. Too fast reads as a bug.")]
        [SerializeField, Min(0.01f)] private float blendSeconds = 1.4f;

        [SerializeField] private Transform target;

        [Tooltip("Camera whose background is kept matching the fog. Found automatically if empty.")]
        [SerializeField] private Camera view;

        private float indoors01;
        private bool initialised;

        /// <summary>0 outside, 1 inside. Exposed for audio and, later, enemy behaviour.</summary>
        public float Indoors01 => indoors01;

        private void Awake()
        {
            if (view == null) view = Camera.main;
            if (target == null && view != null) target = view.transform;
        }

        private void Update()
        {
            if (target == null) return;

            float wanted = IsUnderCover(target.position) ? 1f : 0f;

            // Snapping on the first frame stops the run opening with a visible fade from
            // whatever the scene was saved with.
            if (!initialised)
            {
                indoors01 = wanted;
                initialised = true;
            }
            else
            {
                indoors01 = Mathf.MoveTowards(indoors01, wanted, Time.deltaTime / blendSeconds);
            }

            Apply(indoors01, ShiftProgress());
        }

        /// <summary>
        /// Indoors means "something solid overhead".
        ///
        /// A raycast is used instead of trigger volumes at the doors on purpose: triggers have to
        /// be placed by hand on every opening, and one missed doorway leaves the player permanently
        /// in the wrong atmosphere. A ceiling is a fact about the geometry, so it cannot be
        /// forgotten when a new room is added.
        /// </summary>
        private bool IsUnderCover(Vector3 position)
        {
            return Physics.Raycast(position, Vector3.up, ceilingProbeHeight,
                                   GameLayers.EnvironmentMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>0 at the start of the shift, 1 at the end. Falls back to 0 with no clock.</summary>
        private static float ShiftProgress()
        {
            GameClock clock = GameClock.Instance;
            return clock != null ? clock.Normalised : 0f;
        }

        private void Apply(float inside, float progress)
        {
            Atmosphere interior = Atmosphere.Lerp(interiorEarly, interiorLate, progress);
            Atmosphere exterior = Atmosphere.Lerp(exteriorEarly, exteriorLate, progress);
            Atmosphere current = Atmosphere.Lerp(exterior, interior, inside);
            AtmospherePresets.Apply(current);

            // Without this the fog fades into the camera's clear colour and the sky ends in a
            // hard horizontal line — the outdoors reads as a painted backdrop rather than as
            // distance. There is no skybox by design; the fog *is* the sky.
            if (view != null) view.backgroundColor = current.fogColor;
        }
    }
}
