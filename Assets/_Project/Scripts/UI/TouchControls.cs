using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace UtezHorror.UI
{
    /// <summary>
    /// Shows or hides the on-screen controls depending on what the player is actually using.
    ///
    /// A phone HUD permanently drawn over a PC build is the most common way a cross-platform game
    /// announces that nobody tested it on either. It watches which device last produced input and
    /// switches, so picking up a controller mid-game hides the thumbstick and touching the screen
    /// brings it back.
    ///
    /// The controls themselves are Unity's <c>OnScreenStick</c> and <c>OnScreenButton</c>, which
    /// feed the same action asset as a keyboard. The rest of the game never learns which one is
    /// in use — that is the whole reason the input was routed through <c>PlayerInputRouter</c>
    /// rather than read where it is needed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TouchControls : MonoBehaviour
    {
        [SerializeField] private GameObject group;

        [Tooltip("Show the touch controls in the editor regardless of the device, for layout work.")]
        [SerializeField] private bool forceVisible;

        private void OnEnable()
        {
            InputSystem.onActionChange += OnActionChange;
            Apply(ShouldShow());
        }

        private void OnDisable() => InputSystem.onActionChange -= OnActionChange;

        private void OnActionChange(object obj, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed) return;
            if (obj is not InputAction action) return;

            InputDevice device = action.activeControl?.device;
            if (device == null) return;

            // Only react to a real device change; this fires on every action, every frame.
            bool touching = device is Touchscreen;
            if (touching != group.activeSelf) Apply(touching || forceVisible);
        }

        private bool ShouldShow()
        {
            if (forceVisible) return true;
            return Touchscreen.current != null && Application.isMobilePlatform;
        }

        private void Apply(bool visible)
        {
            if (group != null) group.SetActive(visible);
        }
    }
}
