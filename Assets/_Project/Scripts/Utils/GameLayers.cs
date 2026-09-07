using UnityEngine;

namespace UtezHorror.Utils
{
    /// <summary>
    /// Resolved from the names in TagManager rather than hardcoded indices, so renaming a
    /// layer in the editor fails loudly here instead of silently pointing at the wrong one.
    /// </summary>
    public static class GameLayers
    {
        public static readonly int Player = Resolve("Player");
        public static readonly int Enemy = Resolve("Enemy");
        public static readonly int Interactable = Resolve("Interactable");
        public static readonly int HidingSpot = Resolve("HidingSpot");
        public static readonly int Environment = Resolve("Environment");
        public static readonly int Prop = Resolve("Prop");
        public static readonly int TriggerVolume = Resolve("TriggerVolume");
        public static readonly int IgnoreVision = Resolve("IgnoreVision");

        /// <summary>Everything an interaction ray is allowed to hit.</summary>
        public static readonly LayerMask InteractionMask =
            (1 << Interactable) | (1 << HidingSpot) | (1 << Prop);

        /// <summary>
        /// Everything that blocks line of sight and muffles sound. Interactables are in here
        /// because a closed door has to muffle a noise the way a wall does.
        /// </summary>
        public static readonly LayerMask OcclusionMask =
            (1 << Environment) | (1 << Default) | (1 << Prop) | (1 << Interactable);

        /// <summary>
        /// Solid architecture only. Used to answer "is there a ceiling above me", which is how
        /// <c>AtmosphereController</c> decides whether the player is inside the building.
        /// </summary>
        public static readonly LayerMask EnvironmentMask = (1 << Environment) | (1 << Default);

        private const int Default = 0;

        private static int Resolve(string name)
        {
            int layer = LayerMask.NameToLayer(name);
            if (layer < 0)
                Debug.LogError($"[GameLayers] Layer \"{name}\" is missing from Project Settings > Tags and Layers.");
            return layer;
        }
    }
}
