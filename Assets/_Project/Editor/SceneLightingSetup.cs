using UnityEngine;
using UnityEngine.Rendering;
using UtezHorror.Environment;

namespace UtezHorror.EditorTools
{
    /// <summary>
    /// Applies the project's baseline atmosphere to a scene. Kept here rather than tweaked
    /// by hand in the Lighting window so a regenerated scene never silently reverts to
    /// Unity's defaults — skybox ambient in particular floods an enclosed building with
    /// free fill light and destroys the darkness the whole design rests on.
    /// </summary>
    public static class SceneLightingSetup
    {
        public static void ApplyInterior()
        {
            // The saved scene gets the exterior atmosphere, because that is where the player
            // spawns. At runtime AtmosphereController takes over and blends between the four
            // presets; this only decides what someone sees when they open the scene, and a
            // scene stamped with the interior values opens as a black rectangle.
            AtmospherePresets.Apply(AtmospherePresets.ExteriorEarly);

            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.defaultReflectionResolution = 64;
            RenderSettings.skybox = null;
        }
    }
}
