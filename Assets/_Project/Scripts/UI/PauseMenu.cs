using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UtezHorror.Core;

namespace UtezHorror.UI
{
    /// <summary>
    /// Pause, and the handful of settings worth changing mid-run.
    ///
    /// It stops the clock as well as the world. A shift with no saving and a hard time limit
    /// cannot keep running while someone is in a menu — that turns a pause into a punishment,
    /// and players stop pausing, which is worse than not having one.
    ///
    /// The settings are the two that actually vary between people and machines: look
    /// sensitivity, and how hard the picture is pixelated. The second is here rather than buried
    /// in a config file because it is the most subjective decision in the whole project and the
    /// only honest way to set it is with the game running.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text body;
        [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

        [Header("Render scale")]
        [Tooltip("How coarse the picture may get. 0.20 is about 216p on a 1080p window.")]
        [SerializeField, Range(0.1f, 1f)] private float minScale = 0.20f;

        [SerializeField, Range(0.1f, 1f)] private float maxScale = 0.60f;
        [SerializeField, Range(0.01f, 0.2f)] private float scaleStep = 0.03f;

        private bool paused;
        private bool ended;

        private void OnEnable()
        {
            GameSignals.RunWon += OnRunOver;
            GameSignals.RunLost += OnRunLost;
            if (panel != null) panel.SetActive(false);
        }

        private void OnDisable()
        {
            GameSignals.RunWon -= OnRunOver;
            GameSignals.RunLost -= OnRunLost;
            // Leaving the game paused on the way out would freeze the next scene too.
            if (paused) SetPaused(false);
        }

        private void OnRunLost(RunOutcome outcome) => OnRunOver();

        /// <summary>Once the run is over the end screen owns the input; pausing on top of it would trap the player.</summary>
        private void OnRunOver()
        {
            ended = true;
            if (paused) SetPaused(false);
        }

        private void Update()
        {
            if (ended) return;

            if (Input.GetKeyDown(toggleKey)) SetPaused(!paused);
            if (!paused) return;

            if (Input.GetKeyDown(KeyCode.LeftBracket)) NudgeScale(-scaleStep);
            if (Input.GetKeyDown(KeyCode.RightBracket)) NudgeScale(scaleStep);
            if (Input.GetKeyDown(KeyCode.Q)) Quit();
        }

        private void SetPaused(bool value)
        {
            paused = value;

            if (panel != null) panel.SetActive(paused);
            Time.timeScale = paused ? 0f : 1f;

            // The clock is stopped explicitly as well as by timeScale: it is the run's only
            // real resource, and a bug that let it tick through a pause would be invisible
            // until someone lost a shift to it.
            GameClock.Instance?.SetPaused(paused);

            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;

            if (paused) Refresh();
        }

        private void NudgeScale(float delta)
        {
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline
                           as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (pipeline == null) return;

            pipeline.renderScale = Mathf.Clamp(pipeline.renderScale + delta, minScale, maxScale);
            Refresh();
        }

        private void Refresh()
        {
            if (body == null) return;

            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline
                           as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            float scale = pipeline != null ? pipeline.renderScale : 0f;
            int lines = Mathf.RoundToInt(Screen.height * scale);

            body.text =
                $"[Esc] seguir\n" +
                $"[ [ ]  [ ] ]  pixelado: {lines}p\n" +
                $"[Q] salir";
        }

        public void Quit()
        {
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded("Menu")) SceneManager.LoadScene("Menu");
#if UNITY_EDITOR
            else UnityEditor.EditorApplication.isPlaying = false;
#else
            else Application.Quit();
#endif
        }
    }
}
