using SpaceAI2.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpaceAI2.Editor
{
    public static class FlightTestSceneBuilder
    {
        private const string Root = "Assets/SpaceAI2";
        private const string ScenePath = Root + "/Scenes/FlightTest.unity";
        private const string ProfilePath = Root + "/Profiles/Fighter.asset";
        private const string ModelPath = "Assets/SpaceAI/Content/Models/Other/SM_Ship_Fighter_05.fbx";

        [MenuItem("Tools/SpaceAI2/Build Flight Test Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            ShipProfile profile = LoadOrCreateProfile();

            GameObject ship = CreateShip(profile, out ShipMotor motor, out PlayerPilot pilot);
            SetupCamera(ship.transform, motor, pilot);
            CreateObstacles();

            EnsureFolder(Root + "/Scenes");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Selection.activeObject = ship;
        }

        private static ShipProfile LoadOrCreateProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<ShipProfile>(ProfilePath);
            if (profile != null) return profile;

            EnsureFolder(Root + "/Profiles");
            profile = ScriptableObject.CreateInstance<ShipProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static GameObject CreateShip(ShipProfile profile, out ShipMotor motor, out PlayerPilot pilot)
        {
            var ship = new GameObject("PlayerShip");
            ship.AddComponent<Rigidbody>();
            ship.AddComponent<SphereCollider>().radius = profile.Radius;
            motor = ship.AddComponent<ShipMotor>();
            motor.Profile = profile;
            pilot = ship.AddComponent<PlayerPilot>();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, ship.transform);
                visual.name = "Model";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                ship.AddComponent<ShipBank>().Visual = visual.transform;
            }
            return ship;
        }

        private static void SetupCamera(Transform ship, ShipMotor motor, PlayerPilot pilot)
        {
            Camera camera = Camera.main;
            camera.farClipPlane = 6000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
            camera.transform.SetPositionAndRotation(ship.TransformPoint(0f, 4f, -16f), ship.rotation);
            camera.gameObject.AddComponent<ChaseCamera>().Target = ship;
            camera.gameObject.AddComponent<FlightHud>().Bind(motor, pilot);
        }

        private static void CreateObstacles()
        {
            var root = new GameObject("Obstacles").transform;
            var random = new System.Random(1);

            for (int i = 0; i < 300; i++)
            {
                var rock = GameObject.CreatePrimitive(i % 3 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere);
                rock.name = "Rock";
                rock.transform.SetParent(root);
                Vector3 position;
                do position = RandomVector(random) * 2500f;
                while (position.magnitude < 150f);
                rock.transform.position = position;
                rock.transform.rotation = Quaternion.Euler(RandomVector(random) * 180f);
                rock.transform.localScale = Vector3.one * Mathf.Lerp(8f, 120f, Mathf.Pow((float)random.NextDouble(), 2f));
            }

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

        private static Vector3 RandomVector(System.Random random) => new Vector3(
            (float)random.NextDouble() * 2f - 1f,
            (float)random.NextDouble() * 2f - 1f,
            (float)random.NextDouble() * 2f - 1f);

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
