using UnityEngine;
using UtezHorror.Core;

namespace UtezHorror.Enemies
{
    /// <summary>
    /// One asset per antagonist. Everything that distinguishes them — how fast, how alert,
    /// when in the shift they are allowed to show up, and what has to be true for them to
    /// spawn — is data, so adding a new one never means editing AI code.
    /// </summary>
    [CreateAssetMenu(menuName = "UtezHorror/Enemy Config", fileName = "EnemyConfig")]
    public sealed class EnemyConfig : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Unnamed";

        [TextArea(2, 4)]
        [Tooltip("Design intent: what this one does to the player that no other does.")]
        public string designNotes;

        [Header("Movement")]
        [Min(0f)] public float patrolSpeed = 1.6f;
        [Tooltip("Chase speed. Set this slightly ABOVE the player's sprint (5.6 m/s) so running " +
                 "in a straight line eventually loses. Running has to buy distance, not safety.")]
        [Min(0f)] public float chaseSpeed = 6f;

        [Header("Perception")]
        [Tooltip("How far this enemy can see the player, in metres.")]
        [Min(0f)] public float sightRange = 14f;

        [Tooltip("Full width of the vision cone, in degrees.")]
        [Range(10f, 360f)] public float sightAngle = 100f;

        [Tooltip("Scales every incoming noise radius. 1 = normal, 2 = hears twice as far.")]
        [Min(0f)] public float hearingMultiplier = 1f;

        [Tooltip("Distance at which a chasing enemy actually catches the player.")]
        [Min(0.1f)] public float catchRadius = 1.2f;

        [Tooltip("Seconds spent searching the last known position before giving up.")]
        [Min(0f)] public float searchDuration = 12f;

        [Tooltip("Seconds it keeps chasing after losing sight, before dropping to a search. " +
                 "Zero makes it comically easy to shake off by stepping behind anything.")]
        [Min(0f)] public float chaseGraceSeconds = 3.5f;

        [Header("Appearance rules")]
        [Tooltip("Earliest phase of the shift in which this enemy may appear at all.")]
        public ShiftPhase earliestPhase = ShiftPhase.Fading;

        [Tooltip("Only appears while the mains power is out.")]
        public bool requiresBlackout;

        [Tooltip("Minimum seconds between two appearances of this enemy.")]
        [Min(0f)] public float cooldownSeconds = 120f;

        [Tooltip("Relative likelihood of being picked when several enemies are eligible.")]
        [Min(0f)] public float selectionWeight = 1f;

        public bool IsEligible(ShiftPhase phase, bool powerOn) =>
            phase >= earliestPhase && (!requiresBlackout || !powerOn);
    }
}
