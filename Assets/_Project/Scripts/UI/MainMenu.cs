using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UtezHorror.UI
{
    /// <summary>
    /// The title screen.
    ///
    /// Deliberately almost empty: a title, one line telling you what the shift is, and a key to
    /// start. A menu is the first thing a player sees, and a busy one promises a busy game — this
    /// one should feel like standing outside a building at dusk with a decision to make.
    ///
    /// It reads raw <c>Input</c> rather than the action asset because the input router belongs to
    /// a player object that does not exist in this scene, and because a menu that only responds
    /// to a rebound key is a menu nobody can get past.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenu : MonoBehaviour
    {
        [SerializeField] private string gameScene = "Cecadec";
        [SerializeField] private Text hint;
        [SerializeField] private KeyCode startKey = KeyCode.Return;
        [SerializeField] private KeyCode quitKey = KeyCode.Escape;

        private void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (hint != null) hint.text = $"[{startKey}] entrar     [{quitKey}] salir";
        }

        private void Update()
        {
            // Any of these, not just Return: on a phone there is no keyboard, and a tap on the
            // screen has to be able to start the game.
            if (Input.GetKeyDown(startKey) || Input.GetKeyDown(KeyCode.Space) ||
                Input.GetMouseButtonDown(0) || Input.touchCount > 0)
            {
                Play();
            }
            else if (Input.GetKeyDown(quitKey))
            {
                Quit();
            }
        }

        public void Play()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SceneManager.LoadScene(gameScene);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
