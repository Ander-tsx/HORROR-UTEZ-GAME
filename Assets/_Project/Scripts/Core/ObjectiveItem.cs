using System;
using UnityEngine;

namespace UtezHorror.Core
{
    /// <summary>The stages of taking a component. Not every item needs every one.</summary>
    public enum TheftStep
    {
        /// <summary>Getting the case open. Screwdriver work: slow, and it clatters.</summary>
        Unscrew,

        /// <summary>Finding the part among everything else in there. Quiet, but it takes time.</summary>
        Search,

        /// <summary>Pulling it free. The loudest moment, and the one you cannot take back.</summary>
        Extract,

        /// <summary>Getting it into the bag. Short, and mostly there to make the end feel deliberate.</summary>
        Store
    }

    [Serializable]
    public struct TheftStage
    {
        public TheftStep step;

        [Tooltip("Seconds of uninterrupted work at a normal pace.")]
        [Min(0.1f)] public float seconds;

        [Tooltip("Hearing radius of this stage, in metres. Zero is silent.")]
        [Min(0f)] public float noiseRadius;
    }

    /// <summary>
    /// One component the player has to steal, and everything about how it comes out.
    ///
    /// The design decision this encodes (Docs/Plans/01): **one verb, four contexts.** Every item
    /// is taken the same way — work at it, make noise, pull it free — and what differs is where
    /// it lives, how long each stage takes, and what it costs you afterwards. Four separate
    /// minigames would be four times the work and would share nothing; this way a new component
    /// is an asset, not a system.
    ///
    /// The interesting numbers are all here rather than in the theft code, so the difference
    /// between a trivial grab and a terrifying one is data a designer can tune.
    /// </summary>
    [CreateAssetMenu(menuName = "UtezHorror/Objective Item", fileName = "ObjectiveItem")]
    public sealed class ObjectiveItem : ScriptableObject
    {
        [Header("Identity")]
        public string id = "component";
        public string displayName = "Componente";

        [TextArea(2, 3)]
        [Tooltip("Where this one is found. Shown in the objective list.")]
        public string whereFound;

        [Header("Taking it")]
        [Tooltip("The stages, in order. An item with one short stage is the tutorial; " +
                 "an item with four is the full ritual.")]
        public TheftStage[] stages =
        {
            new() { step = TheftStep.Unscrew, seconds = 3.5f, noiseRadius = 5f },
            new() { step = TheftStep.Search,  seconds = 2.5f, noiseRadius = 2f },
            new() { step = TheftStep.Extract, seconds = 2.0f, noiseRadius = 8f },
            new() { step = TheftStep.Store,   seconds = 1.0f, noiseRadius = 1f }
        };

        [Tooltip("How much faster the work goes while rushing.")]
        [Min(1f)] public float rushSpeed = 2.1f;

        [Tooltip("How much further the noise carries while rushing. This is the whole bargain: " +
                 "haste buys time and spends safety.")]
        [Min(1f)] public float rushNoise = 2.6f;

        [Header("Carrying it")]
        [Tooltip("Bulky enough to slow you down once it is in your hands.")]
        public bool encumbers;

        [Tooltip("Walk speed multiplier while carrying it. Only used when it encumbers.")]
        [Range(0.2f, 1f)] public float carrySpeed = 0.55f;

        [Tooltip("Whether you can still run with it. A monitor under one arm says no.")]
        public bool allowsSprint = true;

        public float TotalSeconds
        {
            get
            {
                float total = 0f;
                foreach (TheftStage stage in stages) total += stage.seconds;
                return total;
            }
        }
    }
}
