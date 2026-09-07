using UnityEditor;
using UnityEngine;
using UtezHorror.Core;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>
    /// The four components the run is for, as assets.
    ///
    /// This is Docs/Plans/01's table turned into data: one verb, four contexts. Every one is
    /// taken the same way, and what differs is how long it takes, how loud it is, and what it
    /// costs to carry afterwards. Read down the numbers and the intended arc is visible —
    /// the cables teach the mechanic, the cabinet is the one you think twice about.
    ///
    /// Created rather than committed by hand so the whole set can be rebuilt from one menu item,
    /// and so these comments live next to the values they explain. Existing assets are left
    /// alone: once someone has tuned one in the inspector, regenerating must not undo it.
    /// </summary>
    public static class ObjectiveCatalog
    {
        private const string Folder = "Assets/_Project/ScriptableObjects/ObjectiveItems";

        public static ObjectiveItem[] EnsureAll() => new[]
        {
            // Easy, quiet, quick. It exists to teach the hold-to-work verb somewhere safe.
            Ensure("Cables", "Cables", "Detrás de un rack, en el suelo", encumbers: false,
                   stages: new[]
                   {
                       Stage(TheftStep.Search, 2.0f, 1.5f),
                       Stage(TheftStep.Extract, 1.5f, 3f),
                       Stage(TheftStep.Store, 0.8f, 1f)
                   }),

            // The full ritual, and the reason the mechanic exists: open it, find it, pull it.
            Ensure("DiscoDuro", "Disco duro", "Dentro de una PC", encumbers: false,
                   stages: new[]
                   {
                       Stage(TheftStep.Unscrew, 4.5f, 6f),
                       Stage(TheftStep.Search, 3.0f, 2f),
                       Stage(TheftStep.Extract, 2.5f, 7f),
                       Stage(TheftStep.Store, 1.0f, 1.5f)
                   }),

            // Bolted down. The noise is loud and unavoidable, so the decision is *when*, not
            // whether — you take it once you know where the professor is, or you regret it.
            Ensure("Pantalla", "Pantalla", "Anclada al escritorio", encumbers: true,
                   carrySpeed: 0.75f, allowsSprint: true,
                   stages: new[]
                   {
                       Stage(TheftStep.Unscrew, 5.5f, 11f),
                       Stage(TheftStep.Extract, 3.0f, 13f),
                       Stage(TheftStep.Store, 1.2f, 2f)
                   }),

            // Quick to free and expensive to keep: you cannot run while carrying it, and the
            // professor is faster than a sprint. Everything after taking it is a different game.
            Ensure("Gabinete", "Gabinete", "Bajo el escritorio, voluminoso", encumbers: true,
                   carrySpeed: 0.55f, allowsSprint: false,
                   stages: new[]
                   {
                       Stage(TheftStep.Unscrew, 2.5f, 5f),
                       Stage(TheftStep.Extract, 2.5f, 9f),
                       Stage(TheftStep.Store, 1.5f, 3f)
                   })
        };

        private static TheftStage Stage(TheftStep step, float seconds, float noiseRadius) =>
            new() { step = step, seconds = seconds, noiseRadius = noiseRadius };

        private static ObjectiveItem Ensure(string id, string displayName, string whereFound,
                                            bool encumbers, TheftStage[] stages,
                                            float carrySpeed = 1f, bool allowsSprint = true)
        {
            LevelPrimitives.EnsureFolder(Folder);
            string path = $"{Folder}/{id}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<ObjectiveItem>(path);
            if (existing != null) return existing;

            var item = ScriptableObject.CreateInstance<ObjectiveItem>();
            item.id = id;
            item.displayName = displayName;
            item.whereFound = whereFound;
            item.stages = stages;
            item.encumbers = encumbers;
            item.carrySpeed = carrySpeed;
            item.allowsSprint = allowsSprint;

            AssetDatabase.CreateAsset(item, path);
            return item;
        }
    }
}
