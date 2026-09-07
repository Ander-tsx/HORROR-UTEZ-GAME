using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UtezHorror.Utils;

namespace UtezHorror.Tests
{
    /// <summary>
    /// Guards the project configuration itself. These fail loudly if someone renames or
    /// deletes a layer, which would otherwise show up as silently broken raycasts.
    /// </summary>
    public sealed class ProjectSetupTests
    {
        [TestCase("Player")]
        [TestCase("Enemy")]
        [TestCase("Interactable")]
        [TestCase("HidingSpot")]
        [TestCase("Environment")]
        [TestCase("Prop")]
        [TestCase("TriggerVolume")]
        [TestCase("IgnoreVision")]
        public void RequiredLayerExists(string layerName)
        {
            Assert.GreaterOrEqual(LayerMask.NameToLayer(layerName), 0,
                $"Layer '{layerName}' is missing from Project Settings > Tags and Layers.");
        }

        [Test]
        public void ColorSpaceIsLinear()
        {
            Assert.AreEqual(ColorSpace.Linear, QualitySettings.activeColorSpace,
                "Gamma space breaks light falloff; the whole lighting pass is authored for Linear.");
        }

        [Test]
        public void InteractionMaskExcludesEnvironment()
        {
            Assert.AreEqual(0, GameLayers.InteractionMask.value & (1 << GameLayers.Environment),
                "Walls must not swallow the interaction ray.");
        }

        [Test]
        public void OcclusionMaskIncludesEnvironment()
        {
            Assert.AreNotEqual(0, GameLayers.OcclusionMask.value & (1 << GameLayers.Environment),
                "Noise and line of sight must be blocked by walls.");
        }

        /// <summary>
        /// The navigation agent has to stay narrow enough for the spiral stair.
        ///
        /// Unity's default Humanoid agent is 0.5 m in radius. The stair runs are 1.2 m wide
        /// around a solid core, which leaves 0.2 m of walkable strip once that radius is eroded
        /// off both sides — and the corners sever completely. The symptom is not an error: the
        /// enemy simply never goes upstairs, and half the building is silently safe forever.
        ///
        /// The climb value matters too. It was 0.75, which is desk height, so the NavMesh
        /// happily ran across the furniture.
        /// </summary>
        [Test]
        public void NavMeshAgentFitsTheSpiralStair()
        {
            NavMeshBuildSettings agent = NavMesh.GetSettingsByID(0);

            Assert.LessOrEqual(agent.agentRadius, 0.4f,
                "Agent radius is too wide for the 1.2 m stair runs; the upper floor becomes " +
                "unreachable without any error being logged.");

            Assert.LessOrEqual(agent.agentClimb, 0.45f,
                "Agent climb is high enough to step onto desks, so the NavMesh runs over the " +
                "furniture instead of around it.");

            Assert.GreaterOrEqual(agent.agentClimb, 0.2f,
                "Agent climb is below the stair's rise per step; the stair will not bake.");
        }
    }
}
