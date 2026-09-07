using UnityEngine;
using UnityEngine.AI;

namespace UtezHorror.Enemies
{
    /// <summary>
    /// Animates the professor by rotating its bones directly, with no animation clips.
    ///
    /// Why procedural rather than authored clips: the model was downloaded unrigged and the
    /// project has no animation assets. A hand-made walk cycle is better, and this is written so
    /// it can be replaced by one — the rig is imported as Humanoid precisely so a Mixamo clip
    /// retargets onto it later. Until then, a gait computed from the agent's real speed beats a
    /// creature sliding down a corridor in a T-pose.
    ///
    /// The style helps: a PS1 character had around twenty bones and animated at roughly 15 fps,
    /// so a stiff, mechanical gait is period-correct rather than a compromise. There is a
    /// deliberate frame quantisation below.
    ///
    /// **Root motion is never used.** The <see cref="NavMeshAgent"/> owns the transform; this only
    /// rotates bones underneath it. Letting an animation drive position as well is what makes an
    /// agent skate, drift off its path, or fight its own pathfinding.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAnimator : MonoBehaviour
    {
        /// <summary>A bone plus the rotation it rests at, so swings are relative to its own stance.</summary>
        private readonly struct Joint
        {
            public readonly Transform Bone;
            public readonly Quaternion Rest;

            public Joint(Transform bone)
            {
                Bone = bone;
                Rest = bone != null ? bone.localRotation : Quaternion.identity;
            }

            public bool Valid => Bone != null;
        }

        [SerializeField] private Animator animator;
        [SerializeField] private EnemyAI ai;
        [SerializeField] private NavMeshAgent agent;

        [Header("Gait")]
        [Tooltip("Strides per metre travelled. Tied to distance so the legs never skate.")]
        [SerializeField, Min(0.05f)] private float stridesPerMetre = 0.45f;

        [Tooltip("Degrees the thigh swings at full stride.")]
        [SerializeField, Range(0f, 70f)] private float legSwing = 34f;

        [Tooltip("Degrees the upper arm swings. Opposite to the leg on the same side.")]
        [SerializeField, Range(0f, 60f)] private float armSwing = 22f;

        [Tooltip("Forward lean while chasing, in degrees. A running body falls forwards.")]
        [SerializeField, Range(0f, 30f)] private float chaseLean = 14f;

        [Tooltip("Animation updates per second. 15 is the PS1's rate: period, not lag.")]
        [SerializeField, Min(1f)] private float framesPerSecond = 15f;

        private Joint hips, spine, chest, head;
        private Joint leftUpperLeg, leftLowerLeg, rightUpperLeg, rightLowerLeg;
        private Joint leftUpperArm, leftLowerArm, rightUpperArm, rightLowerArm;

        private bool ready;
        private float phase;
        private float lean;
        private float nextFrameTime;
        private Vector3 lastPosition;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (ai == null) ai = GetComponent<EnemyAI>();
            if (agent == null) agent = GetComponent<NavMeshAgent>();

            Resolve();
            lastPosition = transform.position;
        }

        private void Resolve()
        {
            hips = Find(HumanBodyBones.Hips, "Hips");
            spine = Find(HumanBodyBones.Spine, "Spine");
            chest = Find(HumanBodyBones.Chest, "Chest");
            head = Find(HumanBodyBones.Head, "Head");

            leftUpperLeg = Find(HumanBodyBones.LeftUpperLeg, "LeftUpperLeg");
            leftLowerLeg = Find(HumanBodyBones.LeftLowerLeg, "LeftLowerLeg");
            rightUpperLeg = Find(HumanBodyBones.RightUpperLeg, "RightUpperLeg");
            rightLowerLeg = Find(HumanBodyBones.RightLowerLeg, "RightLowerLeg");

            leftUpperArm = Find(HumanBodyBones.LeftUpperArm, "LeftUpperArm");
            leftLowerArm = Find(HumanBodyBones.LeftLowerArm, "LeftLowerArm");
            rightUpperArm = Find(HumanBodyBones.RightUpperArm, "RightUpperArm");
            rightLowerArm = Find(HumanBodyBones.RightLowerArm, "RightLowerArm");

            ready = hips.Valid && leftUpperLeg.Valid && rightUpperLeg.Valid;

            if (!ready)
            {
                // Loud, because the failure is otherwise invisible: the creature still renders,
                // still chases, and simply glides without moving a limb.
                Debug.LogError($"[Enemy] {name}: could not resolve the rig, so it will not animate. " +
                               "Expected Unity Humanoid bone names (Hips, LeftUpperLeg, ...) — " +
                               "check Tools/Blender/make_enemy.py and the model's import settings.", this);
                return;
            }

            // The Animator is switched off on purpose.
            //
            // A Humanoid Animator re-solves and writes the whole skeleton every frame from its
            // avatar, so hand-set bone rotations are overwritten and the creature stands in its
            // rest pose no matter what this component does. Nothing here needs an Animator: it is
            // kept on the object, disabled, so the avatar survives for the day real clips exist.
            if (animator != null) animator.enabled = false;
        }

        private Joint Find(HumanBodyBones id, string fallbackName)
        {
            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            {
                Transform mapped = animator.GetBoneTransform(id);
                if (mapped != null) return new Joint(mapped);
            }
            return new Joint(FindDeep(transform, fallbackName));
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

        /// <summary>
        /// LateUpdate, so the pose is written after the agent has moved the transform.
        /// </summary>
        private void LateUpdate()
        {
            if (!ready) return;

            // Distance actually covered, not the agent's desired velocity: an agent pressed
            // against a wall still reports speed, and its legs would run on the spot.
            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            lastPosition = transform.position;

            phase += delta.magnitude * stridesPerMetre * Mathf.PI * 2f;

            bool chasing = ai != null && ai.State == EnemyState.Chase;
            lean = Mathf.MoveTowards(lean, chasing ? chaseLean : 0f, Time.deltaTime * 40f);

            // Quantised on purpose: sampling at 15 fps under a 60 fps game is what the era looked
            // like, and it costs nothing.
            if (Time.time < nextFrameTime) return;
            nextFrameTime = Time.time + 1f / framesPerSecond;

            Pose();
        }

        private void Pose()
        {
            // Fades the gait out when standing still, so a stopped enemy settles into its rest
            // stance instead of freezing mid-step.
            float speed = agent != null && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
            float gait = Mathf.Clamp01(speed / 1.5f);

            float swing = Mathf.Sin(phase) * gait;
            float sway = Mathf.Cos(phase) * gait;

            // Knees only bend one way, hence clamping to positive.
            float leftKnee = Mathf.Max(0f, -swing) * legSwing * 1.2f;
            float rightKnee = Mathf.Max(0f, swing) * legSwing * 1.2f;

            // Every bone is reset before anything is applied, so a frame never accumulates on
            // the previous one, and so parents are at rest before their children are swung.
            Rest(hips); Rest(spine); Rest(chest); Rest(head);
            Rest(leftUpperLeg); Rest(leftLowerLeg); Rest(rightUpperLeg); Rest(rightLowerLeg);
            Rest(leftUpperArm); Rest(leftLowerArm); Rest(rightUpperArm); Rest(rightLowerArm);

            // Parents first: a child swung before its parent would be carried somewhere else by
            // the parent's rotation on the same frame.
            Swing(hips, sway * 3f * gait);
            Swing(spine, lean * 0.5f);
            Swing(chest, lean * 0.5f);
            Swing(head, -lean * 0.6f);   // stays level against the lean, eyes on you

            Swing(leftUpperLeg, swing * legSwing);
            Swing(leftLowerLeg, leftKnee);
            Swing(rightUpperLeg, -swing * legSwing);
            Swing(rightLowerLeg, rightKnee);

            // Arms swing against the legs. Without the counter-swing a walk reads as a shamble.
            Swing(leftUpperArm, -swing * armSwing);
            Swing(leftLowerArm, -Mathf.Abs(swing) * armSwing * 0.5f);
            Swing(rightUpperArm, swing * armSwing);
            Swing(rightLowerArm, -Mathf.Abs(swing) * armSwing * 0.5f);
        }

        private static void Rest(Joint joint)
        {
            if (joint.Valid) joint.Bone.localRotation = joint.Rest;
        }

        /// <summary>
        /// Swings a bone about the character's own right axis, in world space.
        ///
        /// Not about the bone's local X. Bone axes come out of Blender with whatever roll the
        /// exporter gave them, so a local-axis rotation twists one limb and bends another, and
        /// which is which changes with every model. Rotating about the body's right axis means a
        /// leg swings forwards on any rig, from any exporter, with no per-model tuning.
        /// </summary>
        private void Swing(Joint joint, float degrees)
        {
            if (!joint.Valid || Mathf.Approximately(degrees, 0f)) return;
            joint.Bone.rotation = Quaternion.AngleAxis(degrees, transform.right) * joint.Bone.rotation;
        }
    }
}
