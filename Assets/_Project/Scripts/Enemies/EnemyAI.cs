using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UtezHorror.Core;

namespace UtezHorror.Enemies
{
    public enum EnemyState
    {
        /// <summary>Walking a route, not aware of anything.</summary>
        Patrol,

        /// <summary>Heard something and is going to look. Does not know the player exists.</summary>
        Investigate,

        /// <summary>Can see the player and is closing.</summary>
        Chase,

        /// <summary>Lost sight. Sweeping the area around the last known position.</summary>
        Search
    }

    /// <summary>
    /// The professor. Patrols, investigates noise, chases on sight, searches when it loses you.
    ///
    /// This is the piece the whole design was waiting for. <c>NoiseSystem</c> and
    /// <see cref="EnemyHearing"/> have existed since the first architecture pass and nothing
    /// listened to them, which meant the game's central idea — that hurrying *causes* danger —
    /// was implemented and inert. Investigate is where that idea finally has consequences: a
    /// sprint, a slammed door or a rushed theft draws something towards you.
    ///
    /// Everything that distinguishes one professor from another lives in
    /// <see cref="EnemyConfig"/>, so a second one is an asset, not a subclass.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [DisallowMultipleComponent]
    public sealed class EnemyAI : MonoBehaviour
    {
        [SerializeField] private EnemyConfig config;
        [SerializeField] private EnemyHearing hearing;
        [SerializeField] private EnemyVision vision;

        [Tooltip("Points walked in order while patrolling. Generated from the level layout.")]
        [SerializeField] private List<Vector3> patrolPoints = new();

        [Tooltip("Metres from a destination that counts as having arrived.")]
        [SerializeField, Min(0.2f)] private float arriveDistance = 1.2f;

        [Tooltip("Seconds to pause at each patrol point. Constant motion reads as a machine.")]
        [SerializeField, Min(0f)] private float patrolPause = 2.5f;

        [Tooltip("How far around the last known position it sweeps while searching.")]
        [SerializeField, Min(1f)] private float searchRadius = 6f;

        [Tooltip("Seconds without moving before it assumes it is stuck and recovers.")]
        [SerializeField, Min(1f)] private float stuckTimeout = 3f;

        private NavMeshAgent agent;
        private Transform player;

        private EnemyState state = EnemyState.Patrol;
        private int patrolIndex;
        private float stateTimer;
        private float waitUntil;

        private Vector3 lastPosition;
        private float stuckTime;

        private bool blackout;
        private float chaseGrace;
        private Vector3 lastSeenPoint;
        private Vector3 lastSeenDirection = Vector3.forward;

        public EnemyState State => state;

        public void Configure(EnemyConfig value, IEnumerable<Vector3> route)
        {
            config = value;
            patrolPoints = new List<Vector3>(route);
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (hearing == null) hearing = GetComponent<EnemyHearing>();
            if (vision == null) vision = GetComponent<EnemyVision>();
        }

        private void OnEnable()
        {
            lastPosition = transform.position;
            GameSignals.DirectedEventFired += OnDirectedEvent;
            GameSignals.PowerChanged += OnPowerChanged;
            EnterPatrol();
        }

        /// <summary>
        /// A directed hunt does not tell the enemy where the player is — it tells it to go and
        /// look near them. The difference matters: an enemy that knows is unfair, an enemy that
        /// is *coming to your side of the building* is frightening and still escapable.
        /// </summary>
        private void OnDirectedEvent(DirectedEvent evt)
        {
            if (evt != DirectedEvent.Hunt) return;
            if (player == null) player = ResolvePlayer();
            if (player == null) return;

            Vector2 scatter = Random.insideUnitCircle * 9f;
            EnterSearch(player.position + new Vector3(scatter.x, 0f, scatter.y));
        }

        /// <summary>With the mains down it moves faster and gives up later. The dark is its ground.</summary>
        private void OnPowerChanged(bool on) => blackout = !on;

        private void OnDisable()
        {
            GameSignals.DirectedEventFired -= OnDirectedEvent;
            GameSignals.PowerChanged -= OnPowerChanged;

            // Leaving a path queued on a disabled agent is how you get pathfinding errors on the
            // frame it comes back.
            if (agent != null && agent.isOnNavMesh) agent.ResetPath();
        }

        private void Update()
        {
            if (config == null) return;

            // Everything below assumes the agent is actually on the mesh. It can fall off after
            // a level rebuild, a teleport, or spawning slightly inside geometry, and every
            // NavMeshAgent call in that state logs an error — thousands of them, once a frame.
            if (!agent.isOnNavMesh)
            {
                RecoverOntoNavMesh();
                return;
            }

            DetectStuck();

            switch (state)
            {
                case EnemyState.Patrol: TickPatrol(); break;
                case EnemyState.Investigate: TickInvestigate(); break;
                case EnemyState.Chase: TickChase(); break;
                case EnemyState.Search: TickSearch(); break;
            }
        }

        // ---------------------------------------------------------------- states

        private void TickPatrol()
        {
            if (SeesPlayer()) { EnterChase(); return; }
            if (HeardSomething()) { EnterInvestigate(); return; }

            if (patrolPoints.Count == 0) return;
            if (Time.time < waitUntil) return;

            if (!agent.hasPath || Arrived())
            {
                // Pausing at each stop reads as a person checking a room. Walking a loop without
                // stopping reads as a patrol robot, and the player learns the timing instantly.
                waitUntil = Time.time + patrolPause;
                patrolIndex = (patrolIndex + 1) % patrolPoints.Count;
                TryGoTo(patrolPoints[patrolIndex]);
            }
        }

        private void TickInvestigate()
        {
            if (SeesPlayer()) { EnterChase(); return; }

            // A louder noise while already investigating retargets: the enemy follows the trail.
            if (HeardSomething())
            {
                TryGoTo(hearing.PointOfInterest);
                hearing.ClearPointOfInterest();
            }

            stateTimer -= Time.deltaTime;
            if (Arrived() || stateTimer <= 0f) EnterSearch(transform.position);
        }

        private void TickChase()
        {
            if (player == null) { EnterPatrol(); return; }

            if (SeesPlayer())
            {
                chaseGrace = config.chaseGraceSeconds;

                // Remembered every frame it can see you, so when it loses you it knows both
                // where you were and which way you were going.
                Vector3 previous = lastSeenPoint;
                lastSeenPoint = player.position;
                Vector3 travel = lastSeenPoint - previous;
                if (travel.sqrMagnitude > 0.04f) lastSeenDirection = travel.normalized;

                TryGoTo(lastSeenPoint);

                if (Vector3.Distance(transform.position, player.position) <= config.catchRadius)
                    Catch();

                return;
            }

            // Losing sight does not end a chase. Dropping straight to a search made it trivial
            // to shake off by stepping behind a single desk; it keeps coming for a few seconds,
            // which is the difference between a threat and an obstacle.
            chaseGrace -= Time.deltaTime;
            if (chaseGrace > 0f)
            {
                TryGoTo(lastSeenPoint);
                return;
            }

            // Then it goes to where you *were*, and guesses forward along the way you were
            // heading. Never to where you actually are — an enemy that knows that is not scary,
            // it is unfair.
            EnterSearch(lastSeenPoint + lastSeenDirection * 4f);
        }

        private void TickSearch()
        {
            if (SeesPlayer()) { EnterChase(); return; }
            if (HeardSomething()) { EnterInvestigate(); return; }

            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f) { EnterPatrol(); return; }

            // Sweeps around the last known position rather than around itself, and biased along
            // the direction the player was last moving. Searching in a circle where it happens
            // to be standing is what made it look like it had forgotten why it came.
            if (Arrived() || !agent.hasPath)
                TryGoTo(RandomPointNear(searchAnchor + lastSeenDirection * 2f, searchRadius));
        }

        // ---------------------------------------------------------------- transitions

        private void EnterPatrol()
        {
            state = EnemyState.Patrol;
            SetSpeed(config != null ? config.patrolSpeed : 1.6f);
            waitUntil = 0f;
            if (patrolPoints.Count > 0) TryGoTo(patrolPoints[patrolIndex]);
        }

        private void EnterInvestigate()
        {
            state = EnemyState.Investigate;
            SetSpeed(Mathf.Lerp(config.patrolSpeed, config.chaseSpeed, 0.45f));
            stateTimer = config.searchDuration;

            TryGoTo(hearing.PointOfInterest);
            hearing.ClearPointOfInterest();
        }

        private void EnterChase()
        {
            state = EnemyState.Chase;
            SetSpeed(config.chaseSpeed);
            chaseGrace = config.chaseGraceSeconds;

            if (player == null) player = ResolvePlayer();
            if (player != null)
            {
                lastSeenPoint = player.position;
                TryGoTo(lastSeenPoint);
            }
        }

        private Vector3 searchAnchor;

        private void EnterSearch(Vector3 around)
        {
            state = EnemyState.Search;
            searchAnchor = around;
            SetSpeed(Mathf.Lerp(config.patrolSpeed, config.chaseSpeed, 0.3f));
            stateTimer = config.searchDuration;
            TryGoTo(around);
        }

        private void Catch()
        {
            GameSignals.RaiseEnemyCaughtPlayer(transform.position);
            EnterSearch(transform.position);
        }

        // ---------------------------------------------------------------- perception

        private bool SeesPlayer()
        {
            if (vision == null || !vision.HasLineOfSight) return false;
            if (player == null) player = ResolvePlayer();
            return player != null;
        }

        private bool HeardSomething() => hearing != null && hearing.HasPointOfInterest;

        private static Transform ResolvePlayer()
        {
            Camera main = Camera.main;
            if (main == null) return null;
            return main.transform.parent != null ? main.transform.parent : main.transform;
        }

        // ---------------------------------------------------------------- movement

        private void SetSpeed(float speed)
        {
            // A blackout is the professor's advantage, not just the player's problem.
            if (agent.isOnNavMesh) agent.speed = speed * (blackout ? 1.15f : 1f);
        }

        /// <summary>
        /// Moves towards a world point, snapping it onto the mesh first.
        ///
        /// A destination even slightly off the mesh — inside a wall, on a desk, on the far side
        /// of a doorway that did not bake — produces a partial path the agent walks until it
        /// jams against geometry. Sampling first turns that into a nearby reachable point.
        /// </summary>
        private bool TryGoTo(Vector3 destination)
        {
            if (!agent.isOnNavMesh) return false;

            if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                return false;

            agent.isStopped = false;
            return agent.SetDestination(hit.position);
        }

        private bool Arrived() =>
            !agent.pathPending && agent.remainingDistance <= arriveDistance;

        private static Vector3 RandomPointNear(Vector3 centre, float radius)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            return centre + new Vector3(offset.x, 0f, offset.y);
        }

        private void DetectStuck()
        {
            if (Vector3.Distance(transform.position, lastPosition) > 0.05f)
            {
                stuckTime = 0f;
                lastPosition = transform.position;
                return;
            }

            // Standing still on purpose is not being stuck.
            if (state == EnemyState.Patrol && Time.time < waitUntil) return;

            stuckTime += Time.deltaTime;
            if (stuckTime < stuckTimeout) return;

            stuckTime = 0f;
            agent.ResetPath();
            EnterPatrol();
        }

        private void RecoverOntoNavMesh()
        {
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 12f, NavMesh.AllAreas))
                return;

            agent.Warp(hit.position);
        }
    }
}
