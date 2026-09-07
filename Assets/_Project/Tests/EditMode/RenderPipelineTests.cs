using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UtezHorror.EditorTools;

namespace UtezHorror.Tests
{
    /// <summary>
    /// Guards a regression that already happened once: Unity recreated a default URP asset
    /// and silently repointed Graphics Settings at it, which left every tuned setting
    /// (additional light shadows, Forward+, the mobile tier) inert while the project still
    /// looked fine in the editor.
    /// </summary>
    public sealed class RenderPipelineTests
    {
        [Test]
        public void DefaultPipelineIsTheProjectAsset()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.defaultRenderPipeline;
            Assert.IsNotNull(pipeline, "No render pipeline assigned in Graphics Settings.");
            Assert.AreEqual("URP_PC", pipeline.name,
                "Graphics Settings must point at Assets/Settings/URP_PC.asset.");
        }

        [Test]
        public void EveryQualityLevelHasAPipelineAsset()
        {
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                Assert.IsNotNull(QualitySettings.GetRenderPipelineAssetAt(i),
                    $"Quality level '{names[i]}' has no URP asset, so it falls back to the default tier.");
            }
        }

        [Test]
        public void MobileTierIsSeparateFromPcTier()
        {
            string[] names = QualitySettings.names;
            int mobile = System.Array.IndexOf(names, "Mobile");
            int pcHigh = System.Array.IndexOf(names, "PC High");

            Assert.GreaterOrEqual(mobile, 0, "Expected a 'Mobile' quality level.");
            Assert.GreaterOrEqual(pcHigh, 0, "Expected a 'PC High' quality level.");
            Assert.AreNotSame(QualitySettings.GetRenderPipelineAssetAt(mobile),
                              QualitySettings.GetRenderPipelineAssetAt(pcHigh),
                              "Mobile and PC must not share one URP asset.");
        }

        /// <summary>
        /// The PS1 look is carried by settings that read as mistakes: no anti-aliasing and a
        /// third-resolution render target. They are the first thing someone turns back up when
        /// the game "looks low quality", so they are guarded rather than trusted.
        /// Re-apply with Tools > UtezHorror > Apply PS1 Render Settings.
        /// </summary>
        [TestCase("Assets/Settings/URP_PC.asset")]
        [TestCase("Assets/Settings/URP_Mobile.asset")]
        public void TierKeepsThePs1RenderSettings(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
            Assert.IsNotNull(asset, $"No URP asset at {path}.");
            var so = new SerializedObject(asset);

            Assert.AreEqual(RetroPipelineSetup.RenderScale,
                            so.FindProperty("m_RenderScale").floatValue, 0.001f,
                            $"{asset.name}: render scale drifted; the game stops being 360p.");

            Assert.AreEqual(RetroPipelineSetup.UpscalingPoint,
                            so.FindProperty("m_UpscalingFilter").intValue,
                            $"{asset.name}: upscaling must be Nearest-Neighbor, or the low-res " +
                            "render is blurred back into mush instead of showing hard pixels.");

            Assert.AreEqual(1, so.FindProperty("m_MSAA").intValue,
                            $"{asset.name}: MSAA smooths the exact pixel staircase the style is built on.");

            Assert.IsFalse(so.FindProperty("m_SoftShadowsSupported").boolValue,
                           $"{asset.name}: soft shadows are not period-correct, and cost more.");
        }
    }
}
