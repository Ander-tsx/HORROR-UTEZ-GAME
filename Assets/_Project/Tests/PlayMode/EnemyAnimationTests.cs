using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtezHorror.Enemies;

namespace UtezHorror.Tests
{
    /// <summary>
    /// Proves the professor's legs actually move while it walks.
    ///
    /// Written after two failed fixes. "It is not animated" has half a dozen possible causes —
    /// an unresolved rig, an Animator overwriting the pose, a gait that never leaves zero, a
    /// bone axis that twists instead of bending — and every guess costs a rebuild and a round
    /// trip. None of them throw, and the creature keeps chasing perfectly well while frozen, so
    /// nothing in the console says anything is wrong.
    ///
    /// This measures the one thing that matters: does a bone rotation change between two moments
    /// while the agent is moving.
    /// </summary>
    public sealed class EnemyAnimationTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Cecadec.unity";

        /// <summary>
        /// Puts the runtime back to an empty scene.
        ///
        /// This test loads the real level, which brings a live GameClock with it. GameClock keeps
        /// a static Instance and destroys duplicates, so the next test to build its own clock got
        /// silently thrown away and failed for reasons that had nothing to do with it. A test
        /// that loads a scene has to put it back.
        /// </summary>
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Scene empty = SceneManager.CreateScene("AfterEnemyAnimationTest");
            SceneManager.SetActiveScene(empty);

            Scene level = SceneManager.GetSceneByName("Cecadec");
            if (level.IsValid() && level.isLoaded)
                yield return SceneManager.UnloadSceneAsync(level);
            else
                yield return null;
        }

        [UnityTest]
        public IEnumerator TheEnemyMovesItsLegsWhileWalking()
        {
            Assume.That(System.IO.File.Exists(ScenePath), "Cecadec scene has not been built yet.");

            SceneManager.LoadScene("Cecadec", LoadSceneMode.Single);
            yield return null;
            yield return null;

            EnemyAI enemy = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None).FirstOrDefault();
            Assert.IsNotNull(enemy, "No enemy in the scene.");

            var agent = enemy.GetComponent<NavMeshAgent>();
            Assert.IsNotNull(agent, "Enemy has no NavMeshAgent.");

            Transform thigh = FindDeep(enemy.transform, "LeftUpperLeg");
            Assert.IsNotNull(thigh, "No 'LeftUpperLeg' bone under the enemy.");

            // Let it start walking. It patrols from the first frame, but the agent needs a moment
            // to acquire a path and get up to speed.
            float settle = 0f;
            while (settle < 2f && agent.velocity.magnitude < 0.3f)
            {
                settle += Time.deltaTime;
                yield return null;
            }

            Assert.Greater(agent.velocity.magnitude, 0.3f,
                "The enemy never started moving, so this test cannot say anything about its legs.");

            // Bone rotation is not the thing the player sees. If the bind poses or the weights
            // did not survive the export, the skeleton can swing perfectly while the mesh stays
            // rigid — which looks exactly like "it is not animated".
            var skinned = enemy.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(skinned, "Enemy has no SkinnedMeshRenderer.");
            var baked = new Mesh();
            skinned.BakeMesh(baked, useScale: true);
            Vector3[] restVertices = baked.vertices;

            Quaternion before = thigh.localRotation;
            float travelled = 0f;
            Vector3 last = enemy.transform.position;
            float maxAngle = 0f;

            // Sample across at least a metre of walking: the gait is driven by distance covered,
            // so a fixed number of frames would prove nothing at low speed.
            float maxVertexShift = 0f;

            while (travelled < 1.5f)
            {
                yield return null;
                travelled += Vector3.Distance(enemy.transform.position, last);
                last = enemy.transform.position;
                maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(before, thigh.localRotation));

                // Sampled every frame and kept as a maximum, not compared between two arbitrary
                // moments. The gait is a sine: two lone samples can easily land at the same
                // phase and report almost no movement on a perfectly good animation. That
                // mistake produced a false failure once already.
                skinned.BakeMesh(baked, useScale: true);
                Vector3[] current = baked.vertices;
                for (int i = 0; i < restVertices.Length && i < current.Length; i++)
                    maxVertexShift = Mathf.Max(maxVertexShift,
                                               Vector3.Distance(restVertices[i], current[i]));
            }

            var animator = enemy.GetComponentInChildren<Animator>();
            string context = $"agent speed={agent.velocity.magnitude:0.00}, " +
                             $"animator enabled={(animator == null ? "n/a" : animator.enabled.ToString())}, " +
                             $"walked={travelled:0.00} m, largest thigh swing={maxAngle:0.0}°, " +
                             $"largest vertex movement={maxVertexShift:0.000} m";

            Debug.Log($"[AnimTest] {context}");

            Assert.Greater(maxAngle, 5f,
                $"The thigh never rotated more than {maxAngle:0.0}° over {travelled:0.0} m of " +
                $"walking, so the creature is gliding. {context}");

            // The one the player can actually see, and the one that caught the real bug. The
            // broken version — every vertex bound to the hips — still managed 0.055 m, so a
            // threshold near zero proves nothing. A real stride on a two-metre creature moves a
            // foot by more than half a metre.
            Assert.Greater(maxVertexShift, 0.30f,
                $"The bones move but the mesh does not: no vertex shifted more than " +
                $"{maxVertexShift:0.000} m. The skin weights or bind poses did not survive the " +
                $"import. {context}");
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
