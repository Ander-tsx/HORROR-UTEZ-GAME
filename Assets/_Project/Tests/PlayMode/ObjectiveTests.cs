using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtezHorror.Core;
using UtezHorror.Interaction;

namespace UtezHorror.Tests
{
    /// <summary>
    /// Cover for the run's goal: that there is one, that it is reachable, and that it is not in
    /// the same place twice.
    ///
    /// Distribution happens at runtime, so none of it can be checked by looking at the scene.
    /// And the failure mode is the worst kind — a run that is quietly unwinnable, or one where
    /// two components sit on adjacent desks and the building stops mattering. Neither throws.
    /// </summary>
    public sealed class ObjectiveTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Cecadec.unity";

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Scene empty = SceneManager.CreateScene("AfterObjectiveTest");
            SceneManager.SetActiveScene(empty);

            Scene level = SceneManager.GetSceneByName("Cecadec");
            if (level.IsValid() && level.isLoaded) yield return SceneManager.UnloadSceneAsync(level);
            else yield return null;
        }

        private static IEnumerator LoadLevel()
        {
            Assume.That(System.IO.File.Exists(ScenePath), "Cecadec scene has not been built yet.");
            SceneManager.LoadScene("Cecadec", LoadSceneMode.Single);
            yield return null;
            yield return null;   // Start runs on the second frame, and Start is what distributes
        }

        [UnityTest]
        public IEnumerator EveryComponentIsHiddenSomewhere()
        {
            yield return LoadLevel();

            ObjectiveSystem objectives = ObjectiveSystem.Instance;
            Assert.IsNotNull(objectives, "No ObjectiveSystem in the level; the run has no goal.");
            Assert.Greater(objectives.Required.Count, 0, "No components configured.");

            List<TheftTarget> loaded = Object
                .FindObjectsByType<TheftTarget>(FindObjectsSortMode.None)
                .Where(t => t.Item != null)
                .ToList();

            Assert.AreEqual(objectives.Required.Count, loaded.Count,
                "Some components were never placed, which makes the run unwinnable.");

            foreach (ObjectiveItem item in objectives.Required)
                Assert.IsTrue(loaded.Any(t => t.Item == item),
                              $"'{item.displayName}' is not anywhere in the building.");
        }

        [UnityTest]
        public IEnumerator ComponentsAreSpreadAroundTheBuilding()
        {
            yield return LoadLevel();

            List<TheftTarget> loaded = Object
                .FindObjectsByType<TheftTarget>(FindObjectsSortMode.None)
                .Where(t => t.Item != null)
                .ToList();

            Assume.That(loaded.Count, Is.GreaterThan(1));

            // One per room, so clearing a room never hands over a second component for free.
            List<string> rooms = loaded.Select(t => $"{t.Room}_{Mathf.RoundToInt(t.transform.position.y / 3.5f)}").ToList();
            Assert.AreEqual(rooms.Count, rooms.Distinct().Count(),
                            "Two components landed in the same room: " + string.Join(", ", rooms));

            // And never all downstairs, or the stair stops being part of the run.
            int floors = loaded.Select(t => Mathf.RoundToInt(t.transform.position.y / 3.5f))
                               .Distinct().Count();
            Assert.Greater(floors, 1, "Every component is on one floor; half the building is idle.");
        }

        [UnityTest]
        public IEnumerator TakingAComponentRequiresHoldingOnLongEnough()
        {
            yield return LoadLevel();

            TheftTarget target = Object.FindObjectsByType<TheftTarget>(FindObjectsSortMode.None)
                                       .FirstOrDefault(t => t.Item != null);
            Assert.IsNotNull(target, "Nothing was placed to steal.");

            ObjectiveItem item = target.Item;

            // A single frame of work must not finish it: the whole mechanic is that it takes
            // time you have to spend standing exposed.
            Assert.IsTrue(target.Advance(0.05f, rushing: false), "One frame completed the theft.");
            Assert.Less(target.Progress01, 0.5f, "One frame got more than halfway.");

            // Rushing has to actually be faster, or the risk buys nothing.
            float steady = target.Progress01;
            target.Advance(0.5f, rushing: false);
            float afterSteady = target.Progress01 - steady;

            target.Advance(0.5f, rushing: true);
            float afterRush = target.Progress01 - steady - afterSteady;

            Assert.Greater(afterRush, afterSteady * 1.2f,
                           "Rushing is not meaningfully faster, so its extra noise is a pure loss.");

            // And it does finish.
            float guard = 0f;
            while (target.Advance(0.1f, rushing: false) && guard < item.TotalSeconds * 3f) guard += 0.1f;

            Assert.IsFalse(target.HasItem, "The theft never completed.");
        }
    }
}
