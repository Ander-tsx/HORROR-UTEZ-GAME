using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UtezHorror.Core
{
    /// <summary>
    /// Decides where the components are this run, and tracks how many are left.
    ///
    /// The level places an <see cref="IStealable"/> on every machine in the building; almost all
    /// of them are empty. Choosing the loaded ones here, at runtime, is what stops the game
    /// becoming a memorised route after two attempts — the building is the same, the run is not.
    ///
    /// Two placement rules, and both exist to protect the walk rather than the puzzle:
    /// **never two in the same room**, so clearing one room never hands you a second component
    /// for free; and **never all on one floor**, so the stair is always part of the run. Without
    /// the second rule a lucky draw makes half the building irrelevant.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObjectiveSystem : MonoBehaviour
    {
        [Tooltip("What has to be collected this run, in the order shown in the HUD.")]
        [SerializeField] private ObjectiveItem[] required;

        [Tooltip("Fixed seed makes a run reproducible for testing. Zero picks a new one each run.")]
        [SerializeField] private int seed;

        private readonly List<ObjectiveItem> outstanding = new();
        private readonly List<ObjectiveItem> collected = new();

        public static ObjectiveSystem Instance { get; private set; }

        public IReadOnlyList<ObjectiveItem> Required => required;
        public IReadOnlyList<ObjectiveItem> Collected => collected;
        public int Remaining => outstanding.Count;
        public bool Complete => outstanding.Count == 0 && required is { Length: > 0 };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable() => GameSignals.ObjectiveCompleted += OnCollected;

        private void OnDisable() => GameSignals.ObjectiveCompleted -= OnCollected;

        private void Start() => Distribute();

        private void Distribute()
        {
            outstanding.Clear();
            collected.Clear();

            if (required == null || required.Length == 0)
            {
                Debug.LogWarning("[Objectives] No components configured; the run has no goal.");
                return;
            }

            IReadOnlyList<IStealable> candidates = StealableRegistry.All;
            if (candidates.Count < required.Length)
            {
                Debug.LogError($"[Objectives] Only {candidates.Count} machines in the level for " +
                               $"{required.Length} components. Some will never be findable.");
                return;
            }

            var rng = new System.Random(seed != 0 ? seed : Random.Range(1, int.MaxValue));
            List<IStealable> pool = candidates.OrderBy(_ => rng.Next()).ToList();

            var usedRooms = new HashSet<string>();
            var usedFloors = new HashSet<int>();

            foreach (ObjectiveItem item in required)
            {
                // Relaxed one rule at a time. Dropping both at once — which the first version
                // did — turns a slightly awkward layout into two components on adjacent desks.
                IStealable chosen = Pick(pool, usedRooms, usedFloors, newRoom: true, newFloor: true)
                                    ?? Pick(pool, usedRooms, usedFloors, newRoom: true, newFloor: false)
                                    ?? Pick(pool, usedRooms, usedFloors, newRoom: false, newFloor: false);

                if (chosen == null)
                {
                    Debug.LogError($"[Objectives] Nowhere left to put '{item.displayName}'.");
                    continue;
                }

                chosen.Load(item);
                pool.Remove(chosen);
                usedRooms.Add(RoomId(chosen));
                usedFloors.Add(FloorOf(chosen.Position.y));
                outstanding.Add(item);
            }

            Debug.Log($"[Objectives] {outstanding.Count} components hidden across " +
                      $"{usedRooms.Count} rooms and {usedFloors.Count} floors.");
        }

        /// <summary>
        /// First machine in the shuffled pool that satisfies the rules still being enforced.
        ///
        /// The floor rule only bites until something has been placed on a second floor; after
        /// that it has done its job and would only fight the room rule.
        /// </summary>
        private static IStealable Pick(List<IStealable> pool, HashSet<string> usedRooms,
                                       HashSet<int> usedFloors, bool newRoom, bool newFloor)
        {
            foreach (IStealable candidate in pool)
            {
                if (newRoom && usedRooms.Contains(RoomId(candidate))) continue;

                if (newFloor && usedFloors.Count == 1 &&
                    usedFloors.Contains(FloorOf(candidate.Position.y))) continue;

                return candidate;
            }
            return null;
        }

        private static string RoomId(IStealable s) => $"{s.Room}_{FloorOf(s.Position.y)}";

        private static int FloorOf(float y) => Mathf.RoundToInt(y / 3.5f);

        private void OnCollected(string id)
        {
            ObjectiveItem item = outstanding.FirstOrDefault(o => o.id == id);
            if (item == null) return;

            outstanding.Remove(item);
            collected.Add(item);

            Debug.Log($"[Objectives] '{item.displayName}' taken. {outstanding.Count} to go.");

            if (outstanding.Count == 0) GameSignals.RaiseAllObjectivesCollected();
        }
    }
}
