using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UtezHorror.Enemies;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>
    /// Prints what the enemy in the built scene actually is: its avatar, its bones, and where it
    /// is facing.
    ///
    /// Exists because "the enemy is not animated" and "the enemy runs at me backwards" are both
    /// silent failures with several possible causes — a rejected avatar, bones renamed by the
    /// importer, an Animator overwriting the pose, a model whose forward axis does not match the
    /// agent's. Guessing between them costs a rebuild each time; this answers it in one run, and
    /// will answer it again the next time the model is swapped.
    /// </summary>
    public static class EnemyDiagnostics
    {
        [MenuItem("Tools/UtezHorror/Diagnose Enemy")]
        public static void Diagnose()
        {
            EditorSceneManager.OpenScene(CecadecLevelBuilder.ScenePath, OpenSceneMode.Single);

            var ai = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include,
                                                        FindObjectsSortMode.None).FirstOrDefault();
            if (ai == null)
            {
                Debug.LogError("[Diag] No enemy in the scene.");
                return;
            }

            var report = new StringBuilder("[Diag] enemy report\n");
            report.AppendLine($"  root '{ai.name}' rotation={ai.transform.eulerAngles}");

            var animator = ai.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                report.AppendLine("  NO Animator component");
            }
            else
            {
                report.AppendLine($"  Animator on '{animator.name}' enabled={animator.enabled} " +
                                  $"controller={(animator.runtimeAnimatorController == null ? "none" : "set")}");
                report.AppendLine($"  avatar={(animator.avatar == null ? "NULL" : animator.avatar.name)} " +
                                  $"isHuman={(animator.avatar != null && animator.avatar.isHuman)} " +
                                  $"isValid={(animator.avatar != null && animator.avatar.isValid)}");

                if (animator.avatar != null && animator.avatar.isHuman)
                {
                    foreach (HumanBodyBones id in new[]
                             {
                                 HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Head,
                                 HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
                                 HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm
                             })
                    {
                        Transform bone = animator.GetBoneTransform(id);
                        report.AppendLine($"    {id} -> {(bone == null ? "NULL" : bone.name)}");
                    }
                }
            }

            var skinned = ai.GetComponentInChildren<SkinnedMeshRenderer>(true);
            report.AppendLine(skinned == null
                ? "  NO SkinnedMeshRenderer"
                : $"  SkinnedMeshRenderer '{skinned.name}' bones={skinned.bones.Length} " +
                  $"root={skinned.rootBone?.name ?? "null"}");

            if (skinned != null && skinned.sharedMesh != null)
            {
                // Where the skin weights actually landed. Bones can swing perfectly while the
                // mesh stays rigid, and the only way to tell them apart is to look at which bone
                // each vertex is bound to.
                BoneWeight[] weights = skinned.sharedMesh.boneWeights;
                var counts = new System.Collections.Generic.Dictionary<int, int>();
                foreach (BoneWeight w in weights)
                {
                    counts.TryGetValue(w.boneIndex0, out int c);
                    counts[w.boneIndex0] = c + 1;
                }

                report.AppendLine($"  dominant bone per vertex, of {weights.Length} vertices:");
                foreach (var pair in counts.OrderByDescending(p => p.Value).Take(8))
                {
                    string bone = pair.Key < skinned.bones.Length && skinned.bones[pair.Key] != null
                        ? skinned.bones[pair.Key].name
                        : $"#{pair.Key}";
                    report.AppendLine($"    {bone,-16} {pair.Value,6} ({pair.Value * 100f / weights.Length:0.0}%)");
                }
            }

            report.AppendLine("  transform names under the enemy:");
            foreach (Transform t in ai.GetComponentsInChildren<Transform>(true).Take(30))
                report.AppendLine($"    {Path(t, ai.transform)}");

            var anim = ai.GetComponent<EnemyAnimator>();
            report.AppendLine($"  EnemyAnimator: {(anim == null ? "MISSING" : "present")}");

            // Which way the mesh faces relative to the agent's forward. The agent always walks
            // along +Z, so anything else here is the model coming in backwards.
            if (skinned != null)
                report.AppendLine($"  body local rotation={skinned.transform.root.eulerAngles}, " +
                                  $"model child rotation={FindBodyRoot(ai.transform)?.localEulerAngles}");

            Debug.Log(report.ToString());
        }

        private static Transform FindBodyRoot(Transform enemy)
        {
            foreach (Transform child in enemy)
                if (child.name == "Body") return child;
            return null;
        }

        private static string Path(Transform t, Transform root)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (Transform c = t; c != null && c != root; c = c.parent) parts.Insert(0, c.name);
            return string.Join("/", parts);
        }
    }
}
