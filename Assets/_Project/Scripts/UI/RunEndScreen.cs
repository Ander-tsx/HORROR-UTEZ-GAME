using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UtezHorror.Core;

namespace UtezHorror.UI
{
    /// <summary>
    /// The screen that closes a run, won or lost, with what actually happened on it.
    ///
    /// The summary is not decoration. A run is twenty-five minutes with no saving, so the only
    /// thing that carries between attempts is what the player learned — how far they got, how
    /// long they lasted, how many times the building nearly had them. Ending on a bare "you
    /// lost" throws that away and makes every attempt feel like the first one.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunEndScreen : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text headline;
        [SerializeField] private Text summary;
        [SerializeField] private Text hint;

        [SerializeField] private Color winTint = new(0.80f, 0.86f, 0.74f, 0.95f);
        [SerializeField] private Color loseTint = new(0.86f, 0.62f, 0.52f, 0.95f);

        [SerializeField] private KeyCode restartKey = KeyCode.R;
        [SerializeField] private KeyCode menuKey = KeyCode.Escape;

        private bool ended;

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void OnEnable()
        {
            GameSignals.RunWon += OnWon;
            GameSignals.RunLost += OnLost;
        }

        private void OnDisable()
        {
            GameSignals.RunWon -= OnWon;
            GameSignals.RunLost -= OnLost;
        }

        private void Update()
        {
            if (!ended) return;

            // Read straight from Input rather than through the router: by this point the player
            // object may be mid-death sequence with its components switched off.
            if (Input.GetKeyDown(restartKey)) Restart();
            else if (Input.GetKeyDown(menuKey)) ToMenu();
        }

        private void OnWon() => Show("LO LOGRASTE", winTint);

        private void OnLost(RunOutcome outcome) => Show(outcome switch
        {
            RunOutcome.TimeExpired => "SE ACABÓ EL TURNO",
            RunOutcome.Caught => "TE ATRAPARON",
            _ => "FIN DEL TURNO"
        }, loseTint);

        private void Show(string title, Color tint)
        {
            if (ended) return;
            ended = true;

            if (panel != null) panel.SetActive(true);
            if (headline != null)
            {
                headline.text = title;
                headline.color = tint;
            }

            if (summary != null) summary.text = Summarise();
            if (hint != null) hint.text = $"[{restartKey}] otra vez     [{menuKey}] menú";

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static string Summarise()
        {
            ObjectiveSystem objectives = ObjectiveSystem.Instance;
            GameClock clock = GameClock.Instance;

            int taken = objectives != null ? objectives.Collected.Count : 0;
            int total = objectives?.Required?.Count ?? 0;

            string time = "—";
            if (clock != null && clock.Config != null)
            {
                float lasted = clock.Config.shiftDurationSeconds - clock.RemainingSeconds;
                time = $"{Mathf.FloorToInt(lasted / 60f):00}:{Mathf.FloorToInt(lasted % 60f):00}";
            }

            return $"Componentes: {taken}/{total}\nAguantaste: {time}";
        }

        public void Restart()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ToMenu()
        {
            Time.timeScale = 1f;
            // Falls back to restarting when there is no menu scene in the build, so the button is
            // never a dead end during development.
            if (Application.CanStreamedLevelBeLoaded("Menu")) SceneManager.LoadScene("Menu");
            else Restart();
        }
    }
}
