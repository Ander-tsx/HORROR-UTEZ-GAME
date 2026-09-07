using UnityEngine;

namespace UtezHorror.Core.Noise
{
    /// <summary>
    /// Designer-facing hook: drop on a prop and fire it from an animation event, a UnityEvent,
    /// or a minigame step. Keeps <see cref="NoiseSystem"/> out of gameplay prefabs' code.
    /// </summary>
    public sealed class NoiseEmitter : MonoBehaviour
    {
        [Tooltip("How far away this noise can be heard, in metres.")]
        [Min(0f)] public float radius = 8f;

        public NoiseSource source = NoiseSource.Impact;

        public void Emit() => NoiseSystem.Emit(transform.position, radius, source);

        public void EmitWithRadius(float overrideRadius) =>
            NoiseSystem.Emit(transform.position, overrideRadius, source);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
