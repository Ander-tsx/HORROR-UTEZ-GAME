using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UtezHorror.Core;

namespace UtezHorror.UI
{
    /// <summary>
    /// The shopping list, and the bar that fills while you are taking something.
    ///
    /// The list is the only thing telling the player what the run is *for*, so it is always on
    /// screen. It stays deliberately short — a name per line, a mark for what is done — because
    /// at 360p anything longer is a grey smear.
    ///
    /// The theft bar only exists while a theft is in progress. A progress bar that is always
    /// there is furniture; one that appears when you commit to something and disappears when you
    /// let go is feedback.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObjectiveHud : MonoBehaviour
    {
        [SerializeField] private Text list;
        [SerializeField] private Text clock;

        [Header("Theft")]
        [SerializeField] private GameObject theftGroup;
        [SerializeField] private Image theftFill;
        [SerializeField] private Text theftLabel;

        [SerializeField] private Color normal = new(0.82f, 0.86f, 0.78f, 0.9f);
        [SerializeField] private Color rushing = new(0.92f, 0.48f, 0.30f, 0.95f);

        private readonly StringBuilder builder = new();
        private readonly HashSet<string> done = new();

        private void OnEnable()
        {
            GameSignals.ObjectiveCompleted += OnCollected;
            GameSignals.TheftProgressChanged += OnTheftProgress;
            GameSignals.ShiftProgressChanged += OnShiftProgress;

            OnTheftProgress(-1f, null);
            Redraw();
        }

        private void OnDisable()
        {
            GameSignals.ObjectiveCompleted -= OnCollected;
            GameSignals.TheftProgressChanged -= OnTheftProgress;
            GameSignals.ShiftProgressChanged -= OnShiftProgress;
        }

        private void Start() => Redraw();

        private void OnCollected(string id)
        {
            done.Add(id);
            Redraw();
        }

        private void Redraw()
        {
            if (list == null) return;

            ObjectiveSystem objectives = ObjectiveSystem.Instance;
            if (objectives == null || objectives.Required == null)
            {
                list.text = string.Empty;
                return;
            }

            builder.Clear();
            foreach (ObjectiveItem item in objectives.Required)
            {
                // A dash and a cross rather than a tick: the built-in font has no tick glyph, and
                // a missing glyph renders as an empty box that reads as a bug.
                builder.Append(done.Contains(item.id) ? "x " : "- ");
                builder.AppendLine(item.displayName);
            }

            list.text = builder.ToString();
        }

        /// <summary>Negative progress hides the bar; that is how the thief signals "not working".</summary>
        private void OnTheftProgress(float progress01, string label)
        {
            bool active = progress01 >= 0f;

            if (theftGroup != null) theftGroup.SetActive(active);
            if (!active) return;

            if (theftFill != null)
            {
                theftFill.fillAmount = Mathf.Clamp01(progress01);
                // Colour carries the risk. The word says DEPRISA, but at this resolution the
                // colour is what the player actually registers while looking at a machine.
                theftFill.color = label != null && label.Contains("DEPRISA") ? rushing : normal;
            }

            if (theftLabel != null) theftLabel.text = label ?? string.Empty;
        }

        private void OnShiftProgress(float normalised)
        {
            if (clock == null) return;

            GameClock game = GameClock.Instance;
            if (game == null) return;

            float hour = game.DiegeticHour;
            int hours = Mathf.FloorToInt(hour);
            int minutes = Mathf.FloorToInt((hour - hours) * 60f);
            clock.text = $"{hours:00}:{minutes:00}";
        }
    }
}
