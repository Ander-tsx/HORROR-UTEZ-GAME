using UnityEngine;
using UtezHorror.Core;

namespace UtezHorror.Player
{
    /// <summary>
    /// How easy the player is to spot right now, as a single 0..1 number.
    ///
    /// This is what turns crouching and the torch from flavour into decisions. Every one of these
    /// factors is a trade the player makes knowingly:
    ///
    /// - **Crouching** halves how far you can be seen, and costs more than half your speed. In a
    ///   game on a clock, moving slowly is the most expensive thing you can do.
    /// - **Sprinting** makes you both louder and more visible. Running is never the safe option,
    ///   it is the option you take when you have already lost the quiet one.
    /// - **The torch** is the big one. It nearly doubles your visible range, so the tool that
    ///   lets you see is also the thing that gives you away. That tension is the whole point of
    ///   building the game around one light source.
    /// - **Standing still** helps a little. Motion is what the eye actually catches.
    ///
    /// Published on <see cref="GameSignals"/> so the enemy acts on exactly the number the HUD
    /// shows. A meter that disagrees with the thing hunting you is worse than no meter.
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    [DisallowMultipleComponent]
    public sealed class PlayerVisibility : MonoBehaviour
    {
        [Header("Stance")]
        [SerializeField, Range(0f, 1f)] private float crouched = 0.28f;
        [SerializeField, Range(0f, 1f)] private float standing = 0.55f;
        [SerializeField, Range(0f, 1f)] private float sprinting = 0.75f;

        [Header("Modifiers")]
        [Tooltip("Added while the torch is on. The tool that lets you see is what gives you away.")]
        [SerializeField, Range(0f, 1f)] private float torchBonus = 0.40f;

        [Tooltip("Subtracted while completely still.")]
        [SerializeField, Range(0f, 0.5f)] private float stillnessBonus = 0.12f;

        [Tooltip("Seconds for a change to take effect. Instant flips make the meter unreadable.")]
        [SerializeField, Min(0.01f)] private float blendSeconds = 0.35f;

        private FirstPersonController movement;
        private PlayerFlashlight torch;
        private float exposure;

        /// <summary>0 hidden, 1 obvious. What the enemy scales its sight range by.</summary>
        public float Exposure01 => exposure;

        private void Awake()
        {
            movement = GetComponent<FirstPersonController>();
            torch = GetComponent<PlayerFlashlight>();
            exposure = Target();
        }

        private void OnEnable() => GameSignals.RaisePlayerVisibilityChanged(exposure);

        private void Update()
        {
            float target = Target();
            exposure = Mathf.MoveTowards(exposure, target, Time.deltaTime / blendSeconds);
            GameSignals.RaisePlayerVisibilityChanged(exposure);
        }

        private float Target()
        {
            float value = movement.Stance switch
            {
                Stance.Crouching => crouched,
                Stance.Sprinting => sprinting,
                _ => standing
            };

            // Blended on the crouch fraction rather than the boolean, so going down is a slide
            // rather than a step and the meter matches what the body is doing.
            value = Mathf.Lerp(value, crouched, movement.Crouch01 * 0.5f);

            if (torch != null && torch.IsOn) value += torchBonus;
            if (!movement.IsMoving) value -= stillnessBonus;

            return Mathf.Clamp01(value);
        }
    }
}
