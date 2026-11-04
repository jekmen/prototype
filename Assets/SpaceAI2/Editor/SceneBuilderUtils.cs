using SpaceAI2.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpaceAI2.Editor
{
    internal static class SceneBuilderUtils
    {
        public const string Root = "Assets/SpaceAI2";
        private const string ProfilePath = Root + "/Profiles/Fighter.asset";
        private const string ModelPath = "Assets/SpaceAI/Content/Models/Other/SM_Ship_Fighter_05.fbx";

        public static bool NewScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            return true;
        }

        public static void SaveScene(string name)
        {
            EnsureFolder(Root + "/Scenes");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), $"{Root}/Scenes/{name}.unity");
        }

        public static ShipProfile LoadOrCreateProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<ShipProfile>(ProfilePath);
            if (profile != null) return profile;

            EnsureFolder(Root + "/Profiles");
            profile = ScriptableObject.CreateInstance<ShipProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssets();
            return profile;
        }

        public static GameObject CreateShip(string name, ShipProfile profile, Vector3 position, Quaternion rotation)
        {
            var ship = new GameObject(name);
            ship.transform.SetPositionAndRotation(position, rotation);
            ship.AddComponent<Rigidbody>();
            ship.AddComponent<SphereCollider>().radius = profile.Radius;
            ship.AddComponent<ShipMotor>().Profile = profile;

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

        public static void AddTrail(GameObject ship, Color color)
        {
            var trail = ship.AddComponent<TrailRenderer>();
            trail.time = 1.5f;
            trail.startWidth = 0.8f;
            trail.endWidth = 0f;
            trail.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }

        public static Camera SetupCamera(Transform target, out ChaseCamera chase, out FlightHud hud)
        {
            Camera camera = Camera.main;
            camera.farClipPlane = 8000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
            camera.transform.SetPositionAndRotation(target.TransformPoint(0f, 4f, -16f), target.rotation);
            chase = camera.gameObject.AddComponent<ChaseCamera>();
            chase.Target = target;
            hud = camera.gameObject.AddComponent<FlightHud>();
            return camera;
        }

        public static void ScatterRocks(Transform parent, System.Random random, int count, float minDistance, float maxDistance, float minScale, float maxScale)
        {
            for (int i = 0; i < count; i++)
            {
                var rock = GameObject.CreatePrimitive(i % 3 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere);
                rock.name = "Rock";
                rock.transform.SetParent(parent);
                Vector3 position;
                do position = RandomVector(random) * maxDistance;
                while (position.magnitude < minDistance || position.magnitude > maxDistance);
                rock.transform.position = position;
                rock.transform.rotation = Quaternion.Euler(RandomVector(random) * 180f);
                rock.transform.localScale = Vector3.one * Mathf.Lerp(minScale, maxScale, Mathf.Pow((float)random.NextDouble(), 2f));
            }
        }

        public static Vector3 RandomVector(System.Random random) => new Vector3(
            (float)random.NextDouble() * 2f - 1f,
            (float)random.NextDouble() * 2f - 1f,
            (float)random.NextDouble() * 2f - 1f);

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
