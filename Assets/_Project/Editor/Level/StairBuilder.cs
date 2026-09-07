using System.Collections.Generic;
using UnityEngine;
using UtezHorror.Utils;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>
    /// Square spiral stair: four straight runs and four landings wrapping a solid core, inside
    /// a shaft. The core removes any chance of falling down the well, which a blockout with no
    /// railings would otherwise have.
    ///
    /// <see cref="WalkPoints"/> is the single description of where a person's feet go; the
    /// builder places treads on it and the tests walk it, so the two cannot drift apart.
    /// </summary>
    public static class StairBuilder
    {
        public const int StepsPerRun = 5;
        public const float TreadThickness = 0.28f;

        /// <summary>One walkable surface on the stair: the top face of a tread or a landing.</summary>
        public readonly struct Tread
        {
            public readonly Vector3 Position;   // centre of the top face, in world space
            public readonly Vector3 Size;
            public readonly bool IsLanding;

            /// <summary>Direction of travel, so a kit step can be turned to face up the run.</summary>
            public readonly float Yaw;

            public Tread(Vector3 position, Vector3 size, bool isLanding, float yaw)
            {
                Position = position;
                Size = size;
                IsLanding = isLanding;
                Yaw = yaw;
            }
        }

        /// <summary>
        /// Every walkable surface from the bottom landing to the top one, in climbing order.
        /// </summary>
        public static IEnumerable<Tread> WalkPoints(Vector2 centre, float outerSize, float runWidth,
                                                    float totalRise, float baseY)
        {
            float d = (outerSize - runWidth) * 0.5f;      // landing centre offset from the middle
            float runLength = outerSize - 2f * runWidth;  // straight portion between landings
            float risePerRun = totalRise / 4f;
            float risePerStep = risePerRun / StepsPerRun;
            float tread = runLength / StepsPerRun;

            var landings = new[]
            {
                new Vector2(-d, -d), new Vector2(d, -d), new Vector2(d, d), new Vector2(-d, d)
            };

            for (int run = 0; run < 4; run++)
            {
                Vector2 from = landings[run];
                Vector2 to = landings[(run + 1) % 4];
                float startHeight = run * risePerRun;

                Vector2 dir = (to - from).normalized;
                bool alongX = Mathf.Abs(dir.x) > Mathf.Abs(dir.y);
                float yaw = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;

                yield return new Tread(
                    new Vector3(centre.x + from.x, baseY + startHeight, centre.y + from.y),
                    new Vector3(runWidth, TreadThickness, runWidth), isLanding: true, yaw);

                for (int step = 0; step < StepsPerRun; step++)
                {
                    float centreAlong = runWidth * 0.5f + tread * (step + 0.5f);
                    Vector2 pos = from + dir * centreAlong;
                    float topY = startHeight + risePerStep * (step + 1);

                    var size = alongX
                        ? new Vector3(tread, TreadThickness, runWidth)
                        : new Vector3(runWidth, TreadThickness, tread);

                    yield return new Tread(
                        new Vector3(centre.x + pos.x, baseY + topY, centre.y + pos.y), size,
                        isLanding: false, yaw);
                }
            }

            // Top landing sits directly above the first one, one floor up.
            yield return new Tread(
                new Vector3(centre.x + landings[0].x, baseY + totalRise, centre.y + landings[0].y),
                new Vector3(runWidth, TreadThickness, runWidth), isLanding: true, yaw: 0f);
        }

        public static void SquareSpiral(Transform parent, Vector2 centre, float outerSize,
                                        float runWidth, float totalRise, float baseY)
        {
            var root = LevelPrimitives.Group(parent, "Stairwell");

            // Solid core: a spiral around a hollow well would need railings the blockout lacks.
            KitPlacer.Prop(root.transform, "Stair_Core",
                           new Vector3(centre.x, baseY, centre.y), 0f, GameLayers.Environment,
                           name: "Core");

            // Kit treads pivot on their top face, which is exactly what WalkPoints reports.
            int index = 0;
            foreach (Tread tread in WalkPoints(centre, outerSize, runWidth, totalRise, baseY))
            {
                KitPlacer.Prop(root.transform,
                               tread.IsLanding ? "Stair_Landing" : "Stair_Step",
                               tread.Position, tread.Yaw, GameLayers.Environment,
                               name: tread.IsLanding ? $"Landing{index}" : $"Step{index}");
                index++;
            }
        }
    }
}
