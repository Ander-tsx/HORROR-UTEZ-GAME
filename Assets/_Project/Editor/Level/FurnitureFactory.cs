using UnityEditor;
using UnityEngine;
using UtezHorror.Interaction;
using UtezHorror.Utils;

namespace UtezHorror.EditorTools.Level
{
    /// <summary>
    /// Blockout furniture. Every piece is boxes with real dimensions, so the rooms read at
    /// the right scale and the player collides with them properly — the point right now is
    /// space and movement, not looks. Swapping any of these for a Blender model later means
    /// replacing one method.
    /// </summary>
    public static class FurnitureFactory
    {
        private static Material Wood => LevelPrimitives.Mat("DeskWood", new Color(0.42f, 0.31f, 0.21f), 0.15f);
        private static Material Metal => LevelPrimitives.Mat("FurnitureMetal", new Color(0.32f, 0.33f, 0.35f), 0.55f, 0.7f);
        private static Material Plastic => LevelPrimitives.Mat("ChairPlastic", new Color(0.16f, 0.22f, 0.30f), 0.25f);
        private static Material Case => LevelPrimitives.Mat("PcCase", new Color(0.20f, 0.20f, 0.22f), 0.35f);
        private static Material Screen => LevelPrimitives.Mat("Screen", new Color(0.05f, 0.05f, 0.06f), 0.85f);
        private static Material Board => LevelPrimitives.Mat("Whiteboard", new Color(0.88f, 0.89f, 0.86f), 0.5f);
        private static Material Partition => LevelPrimitives.Mat("Partition", new Color(0.45f, 0.46f, 0.42f), 0.1f);

        private static int PropLayer => GameLayers.Prop;

        /// <summary>Mexican-style paddle-arm classroom chair.</summary>
        public static void Butaca(Transform parent, Vector3 floorPos, float yaw)
        {
            var root = LevelPrimitives.Group(parent, "Butaca");
            root.transform.SetLocalPositionAndRotation(floorPos, Quaternion.Euler(0f, yaw, 0f));
            Transform t = root.transform;

            LevelPrimitives.Box(t, "Seat", new Vector3(0f, 0.45f, 0f), new Vector3(0.46f, 0.05f, 0.44f), Plastic, PropLayer);
            LevelPrimitives.Box(t, "Back", new Vector3(0f, 0.70f, -0.21f), new Vector3(0.46f, 0.46f, 0.05f), Plastic, PropLayer);
            LevelPrimitives.Box(t, "Paddle", new Vector3(0.34f, 0.72f, 0.06f), new Vector3(0.34f, 0.04f, 0.30f), Wood, PropLayer);
            LevelPrimitives.Box(t, "PaddlePost", new Vector3(0.24f, 0.58f, 0.06f), new Vector3(0.04f, 0.28f, 0.04f), Metal, PropLayer, collider: false);

            foreach (var (x, z) in new[] { (-0.19f, -0.18f), (0.19f, -0.18f), (-0.19f, 0.18f), (0.19f, 0.18f) })
                LevelPrimitives.Box(t, "Leg", new Vector3(x, 0.22f, z), new Vector3(0.04f, 0.44f, 0.04f), Metal, PropLayer, collider: false);
        }

        /// <summary>
        /// Lab desk with a PC. The case lies flat with the monitor sitting on top of it,
        /// which is how the machines in Cecadec are actually set up.
        /// </summary>
        public static GameObject ComputerDesk(Transform parent, Vector3 floorPos, float yaw)
        {
            var root = LevelPrimitives.Group(parent, "ComputerDesk");
            root.transform.SetLocalPositionAndRotation(floorPos, Quaternion.Euler(0f, yaw, 0f));
            Transform t = root.transform;

            const float deskH = 0.75f;
            LevelPrimitives.Box(t, "Top", new Vector3(0f, deskH, 0f), new Vector3(1.40f, 0.05f, 0.70f), Wood, PropLayer);
            LevelPrimitives.Box(t, "SideL", new Vector3(-0.66f, deskH * 0.5f, 0f), new Vector3(0.05f, deskH, 0.66f), Wood, PropLayer, collider: false);
            LevelPrimitives.Box(t, "SideR", new Vector3(0.66f, deskH * 0.5f, 0f), new Vector3(0.05f, deskH, 0.66f), Wood, PropLayer, collider: false);
            LevelPrimitives.Box(t, "Modesty", new Vector3(0f, deskH * 0.6f, -0.32f), new Vector3(1.30f, 0.40f, 0.04f), Wood, PropLayer, collider: false);

            // Case lying flat, monitor on top of it.
            const float caseTop = deskH + 0.025f;
            var pc = LevelPrimitives.Box(t, "PcCase", new Vector3(-0.22f, caseTop + 0.06f, -0.08f),
                                         new Vector3(0.44f, 0.12f, 0.42f), Case, GameLayers.Interactable);
            pc.name = "PcCase";

            float monitorBase = caseTop + 0.12f;
            LevelPrimitives.Box(t, "MonitorStand", new Vector3(-0.22f, monitorBase + 0.05f, -0.08f),
                                new Vector3(0.16f, 0.10f, 0.14f), Case, PropLayer, collider: false);
            LevelPrimitives.Box(t, "MonitorPanel", new Vector3(-0.22f, monitorBase + 0.28f, -0.10f),
                                new Vector3(0.52f, 0.34f, 0.03f), Screen, PropLayer, collider: false);
            LevelPrimitives.Box(t, "Keyboard", new Vector3(0.16f, caseTop + 0.01f, 0.12f),
                                new Vector3(0.44f, 0.02f, 0.15f), Case, PropLayer, collider: false);

            OfficeChair(t, new Vector3(0f, 0f, 0.62f), 180f);
            return root;
        }

        public static void OfficeChair(Transform parent, Vector3 floorPos, float localYaw)
        {
            var root = LevelPrimitives.Group(parent, "Chair");
            root.transform.SetLocalPositionAndRotation(floorPos, Quaternion.Euler(0f, localYaw, 0f));
            Transform t = root.transform;

            LevelPrimitives.Box(t, "Seat", new Vector3(0f, 0.45f, 0f), new Vector3(0.46f, 0.07f, 0.46f), Plastic, PropLayer);
            LevelPrimitives.Box(t, "Back", new Vector3(0f, 0.72f, -0.22f), new Vector3(0.44f, 0.48f, 0.06f), Plastic, PropLayer, collider: false);
            LevelPrimitives.Box(t, "Post", new Vector3(0f, 0.22f, 0f), new Vector3(0.07f, 0.44f, 0.07f), Metal, PropLayer, collider: false);
            LevelPrimitives.Box(t, "Base", new Vector3(0f, 0.03f, 0f), new Vector3(0.52f, 0.06f, 0.52f), Metal, PropLayer, collider: false);
        }

        public static void TeacherDesk(Transform parent, Vector3 floorPos, float yaw)
        {
            var root = LevelPrimitives.Group(parent, "TeacherDesk");
            root.transform.SetLocalPositionAndRotation(floorPos, Quaternion.Euler(0f, yaw, 0f));
            Transform t = root.transform;

            LevelPrimitives.Box(t, "Top", new Vector3(0f, 0.76f, 0f), new Vector3(1.60f, 0.06f, 0.80f), Wood, PropLayer);
            LevelPrimitives.Box(t, "Front", new Vector3(0f, 0.38f, -0.36f), new Vector3(1.56f, 0.72f, 0.05f), Wood, PropLayer, collider: false);
            LevelPrimitives.Box(t, "SideL", new Vector3(-0.78f, 0.38f, 0f), new Vector3(0.05f, 0.76f, 0.76f), Wood, PropLayer, collider: false);
            LevelPrimitives.Box(t, "SideR", new Vector3(0.78f, 0.38f, 0f), new Vector3(0.05f, 0.76f, 0.76f), Wood, PropLayer, collider: false);
            OfficeChair(t, new Vector3(0f, 0f, 0.7f), 180f);
        }

        /// <summary>Whiteboard and projector screen mounted flat on a wall.</summary>
        public static void BoardWall(Transform parent, Vector3 wallCentre, float yaw)
        {
            var root = LevelPrimitives.Group(parent, "BoardWall");
            root.transform.SetLocalPositionAndRotation(wallCentre, Quaternion.Euler(0f, yaw, 0f));
            Transform t = root.transform;

            LevelPrimitives.Box(t, "Whiteboard", new Vector3(-0.9f, 1.55f, 0f), new Vector3(3.0f, 1.25f, 0.06f), Board, PropLayer, collider: false);
            LevelPrimitives.Box(t, "ProjectorScreen", new Vector3(1.9f, 1.70f, 0f), new Vector3(1.8f, 1.20f, 0.05f), Screen, PropLayer, collider: false);
        }

        /// <summary>One mini office in Servicios Escolares: desk, chair and partition panels.</summary>
        public static void Cubicle(Transform parent, Vector3 floorPos, float yaw)
        {
            var root = LevelPrimitives.Group(parent, "Cubicle");
            root.transform.SetLocalPositionAndRotation(floorPos, Quaternion.Euler(0f, yaw, 0f));
            Transform t = root.transform;

            const float panelH = 1.55f;
            LevelPrimitives.Box(t, "PanelBack", new Vector3(0f, panelH * 0.5f, -1.20f), new Vector3(2.40f, panelH, 0.06f), Partition, PropLayer);
            LevelPrimitives.Box(t, "PanelLeft", new Vector3(-1.20f, panelH * 0.5f, -0.40f), new Vector3(0.06f, panelH, 1.60f), Partition, PropLayer);

            LevelPrimitives.Box(t, "DeskTop", new Vector3(0f, 0.75f, -0.85f), new Vector3(1.80f, 0.05f, 0.65f), Wood, PropLayer);
            LevelPrimitives.Box(t, "DeskSideL", new Vector3(-0.86f, 0.37f, -0.85f), new Vector3(0.05f, 0.75f, 0.62f), Wood, PropLayer, collider: false);
            LevelPrimitives.Box(t, "DeskSideR", new Vector3(0.86f, 0.37f, -0.85f), new Vector3(0.05f, 0.75f, 0.62f), Wood, PropLayer, collider: false);

            LevelPrimitives.Box(t, "Monitor", new Vector3(0.10f, 1.02f, -1.00f), new Vector3(0.48f, 0.32f, 0.04f), Screen, PropLayer, collider: false);
            LevelPrimitives.Box(t, "Cabinet", new Vector3(1.05f, 0.35f, -0.95f), new Vector3(0.45f, 0.70f, 0.45f), Metal, PropLayer);
            OfficeChair(t, new Vector3(0f, 0f, -0.10f), 0f);
        }

        /// <summary>Outdoor generator the player has to reach when the mains drop.</summary>
        public static void Generator(Transform parent, Vector3 floorPos, float yaw)
        {
            var root = LevelPrimitives.Group(parent, "Generator");
            root.transform.SetLocalPositionAndRotation(floorPos, Quaternion.Euler(0f, yaw, 0f));
            Transform t = root.transform;

            LevelPrimitives.Box(t, "Pad", new Vector3(0f, 0.06f, 0f), new Vector3(4.4f, 0.12f, 3.2f),
                                LevelPrimitives.Mat("Concrete", new Color(0.30f, 0.30f, 0.29f)), GameLayers.Environment);
            LevelPrimitives.BoxOnFloor(t, "Housing", new Vector3(0f, 0.12f, 0f), new Vector3(3.0f, 1.7f, 1.6f), Metal, GameLayers.Interactable);
            LevelPrimitives.BoxOnFloor(t, "Exhaust", new Vector3(1.2f, 0.12f, -0.6f), new Vector3(0.25f, 2.6f, 0.25f), Metal, PropLayer, collider: false);
            LevelPrimitives.BoxOnFloor(t, "Panel", new Vector3(0f, 0.92f, 0.82f), new Vector3(0.9f, 0.6f, 0.08f), Case, PropLayer, collider: false);

            // Cage so the player has to walk around to the front panel.
            LevelPrimitives.BoxOnFloor(t, "RailBack", new Vector3(0f, 0.12f, -1.5f), new Vector3(4.4f, 1.1f, 0.08f), Metal, GameLayers.Environment);
            LevelPrimitives.BoxOnFloor(t, "RailL", new Vector3(-2.16f, 0.12f, 0f), new Vector3(0.08f, 1.1f, 3.2f), Metal, GameLayers.Environment);
            LevelPrimitives.BoxOnFloor(t, "RailR", new Vector3(2.16f, 0.12f, 0f), new Vector3(0.08f, 1.1f, 3.2f), Metal, GameLayers.Environment);
        }
    }
}
