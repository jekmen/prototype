using UnityEditor;
using UnityEngine;
using static SpaceAI2.Editor.SceneBuilderUtils;

namespace SpaceAI2.Editor
{
    public static class FlightTestSceneBuilder
    {
        [MenuItem("Tools/SpaceAI2/Build Flight Test Scene")]
        public static void Build()
        {
            if (!NewScene()) return;
            ShipProfile profile = LoadOrCreateProfile();

            GameObject ship = CreateShip("PlayerShip", profile, Vector3.zero, Quaternion.identity);
            var pilot = ship.AddComponent<PlayerPilot>();
            SetupCamera(ship.transform, out _, out var hud);
            hud.Bind(ship.GetComponent<ShipMotor>(), pilot);
            CreateObstacles();

            SaveScene("FlightTest");
            Selection.activeObject = ship;
        }

        private static void CreateObstacles()
        {
            var root = new GameObject("Obstacles").transform;
            var random = new System.Random(1);
            ScatterRocks(root, random, 300, 150f, 2500f, 8f, 120f);

            for (int i = 0; i < 8; i++)
            {
                var gate = new GameObject("Gate").transform;
                gate.SetParent(root);
                gate.position = new Vector3(0f, 0f, 300f + i * 250f) + RandomVector(random) * 80f;
                for (int j = 0; j < 4; j++)
                {
                    var bar = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                    bar.name = "Bar";
                    bar.SetParent(gate, false);
                    bool vertical = j < 2;
                    float side = j % 2 == 0 ? -1f : 1f;
                    bar.localPosition = vertical ? new Vector3(side * 30f, 0f, 0f) : new Vector3(0f, side * 30f, 0f);
                    bar.localScale = vertical ? new Vector3(4f, 64f, 4f) : new Vector3(64f, 4f, 4f);
                }
            }
        }
    }
}
