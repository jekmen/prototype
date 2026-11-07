using System.Collections.Generic;
using SpaceAI2.Demo;
using UnityEditor;
using UnityEngine;

namespace SpaceAI2.Editor
{
    /// <summary>
    /// Turns a model into a ready ship: a root with <see cref="SpaceShip"/> and its profiles, a collider fitted to the
    /// model, gun muzzles at the nose and missile rails under the hull, optionally saved as a prefab.
    /// </summary>
    internal class CreateShipWizard : EditorWindow
    {
        private enum ColliderShape
        {
            Sphere,
            Box,
            ConvexMesh
        }

        private GameObject model;
        private string shipName = "Ship";
        private Vector3 modelRotation;
        private int team;
        private ShipControl control = ShipControl.AI;
        private ShipProfile profile;
        private PilotProfile pilot;
        private WeaponProfile weapon;
        private MissileProfile missile;
        private ColliderShape colliderShape = ColliderShape.Box;
        private int muzzles = 2;
        private int hardpoints = 2;
        private bool bank = true;
        private bool savePrefab;
        private string prefabFolder = "Assets/SpaceAI2/Prefabs";

        [MenuItem("Tools/SpaceAI2/Create Ship From Model")]
        private static void Open()
        {
            var window = GetWindow<CreateShipWizard>(true, "Create Ship From Model");
            window.minSize = new Vector2(380f, 460f);
            window.LoadDefaults();
            if (Selection.activeGameObject != null) window.SetModel(Selection.activeGameObject);
        }

        private void LoadDefaults()
        {
            const string profiles = SceneBuilderUtils.Root + "/Profiles/";
            profile ??= AssetDatabase.LoadAssetAtPath<ShipProfile>(profiles + "Fighter.asset");
            pilot ??= AssetDatabase.LoadAssetAtPath<PilotProfile>(profiles + "PilotVeteran.asset");
            weapon ??= AssetDatabase.LoadAssetAtPath<WeaponProfile>(profiles + "Cannon.asset");
        }

        private void SetModel(GameObject value)
        {
            model = value;
            if (model != null) shipName = model.name;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            var picked = (GameObject)EditorGUILayout.ObjectField("Model", model, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck()) SetModel(picked);
            shipName = EditorGUILayout.TextField("Name", shipName);
            modelRotation = EditorGUILayout.Vector3Field(new GUIContent("Model rotation", "Turns the model so its nose points along +Z."), modelRotation);

            EditorGUILayout.Space();
            team = EditorGUILayout.IntField("Team", team);
            control = (ShipControl)EditorGUILayout.EnumPopup("Control", control);
            profile = (ShipProfile)EditorGUILayout.ObjectField("Ship profile", profile, typeof(ShipProfile), false);
            if (control == ShipControl.AI) pilot = (PilotProfile)EditorGUILayout.ObjectField("Pilot", pilot, typeof(PilotProfile), false);
            weapon = (WeaponProfile)EditorGUILayout.ObjectField("Gun", weapon, typeof(WeaponProfile), false);
            missile = (MissileProfile)EditorGUILayout.ObjectField("Missiles", missile, typeof(MissileProfile), false);

            EditorGUILayout.Space();
            colliderShape = (ColliderShape)EditorGUILayout.EnumPopup("Collider", colliderShape);
            using (new EditorGUI.DisabledScope(weapon == null)) muzzles = EditorGUILayout.IntSlider("Gun muzzles", muzzles, 1, 4);
            using (new EditorGUI.DisabledScope(missile == null)) hardpoints = EditorGUILayout.IntSlider("Missile rails", hardpoints, 1, 4);
            bank = EditorGUILayout.Toggle(new GUIContent("Bank model in turns", "Adds ShipBank, which leans the model, not the physics body."), bank);
            savePrefab = EditorGUILayout.Toggle("Save as prefab", savePrefab);
            if (savePrefab) prefabFolder = EditorGUILayout.TextField("Prefab folder", prefabFolder);

            if (model != null && profile != null)
            {
                float size = MeasureBounds(model, Quaternion.Euler(modelRotation)).extents.magnitude;
                if (size > 0f && (size > profile.Radius * 2f || size < profile.Radius * 0.5f))
                    EditorGUILayout.HelpBox($"The model is about {size:0.#} m in radius, the profile says {profile.Radius:0.#} m. " +
                                            "The AI keeps distance by the profile's radius; consider a profile made for this size.", MessageType.Warning);
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(model == null || profile == null))
                if (GUILayout.Button("Create", GUILayout.Height(28f))) Create();
            if (profile == null) EditorGUILayout.HelpBox("A ship profile is required.", MessageType.Info);
        }

        private void Create()
        {
            var root = new GameObject(string.IsNullOrWhiteSpace(shipName) ? "Ship" : shipName);
            Undo.RegisterCreatedObjectUndo(root, "Create ship");
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null && !model.scene.IsValid()) root.transform.position = view.pivot;
            else if (model.scene.IsValid()) root.transform.SetPositionAndRotation(model.transform.position, model.transform.rotation);

            Transform visual = AttachModel(root.transform);
            Bounds bounds = MeasureBounds(visual.gameObject, Quaternion.identity, root.transform);
            AddCollider(root, visual, bounds);

            var ship = root.AddComponent<SpaceShip>();
            ship.Team = team;
            ship.Profile = profile;
            ship.Control = control;
            ship.Pilot = control == ShipControl.AI ? pilot : null;
            ship.Weapon = weapon;
            ship.Missile = missile;
            ship.ApplySetup();
            SceneBuilderUtils.PaintTeam(ship);
            if (bank) root.AddComponent<ShipBank>().Visual = visual;

            if (weapon != null) root.GetComponent<ShipGun>().Muzzles = CreatePoints(root.transform, "Muzzle", muzzles, bounds, PointKind.Muzzle);
            if (missile != null) root.GetComponent<MissileLauncher>().Hardpoints = CreatePoints(root.transform, "Hardpoint", hardpoints, bounds, PointKind.Hardpoint);
            EnsureSceneSystems();

            if (savePrefab)
            {
                SceneBuilderUtils.EnsureFolder(prefabFolder);
                string path = AssetDatabase.GenerateUniqueAssetPath($"{prefabFolder}/{root.name}.prefab");
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.UserAction);
            }
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
        }

        private Transform AttachModel(Transform root)
        {
            GameObject instance;
            if (model.scene.IsValid())
            {
                instance = model;
                Undo.SetTransformParent(instance.transform, root, "Create ship");
            }
            else
            {
                instance = PrefabUtility.IsPartOfPrefabAsset(model)
                    ? (GameObject)PrefabUtility.InstantiatePrefab(model, root)
                    : Instantiate(model, root);
            }
            instance.name = "Model";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(modelRotation);
            // The ship's colliders and bodies live on the root.
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>()) Undo.DestroyObjectImmediate(collider);
            foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>()) Undo.DestroyObjectImmediate(body);
            return instance.transform;
        }

        private void AddCollider(GameObject root, Transform visual, Bounds bounds)
        {
            switch (colliderShape)
            {
                case ColliderShape.Sphere:
                    var sphere = root.AddComponent<SphereCollider>();
                    sphere.center = bounds.center;
                    sphere.radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
                    break;
                case ColliderShape.Box:
                    var box = root.AddComponent<BoxCollider>();
                    box.center = bounds.center;
                    box.size = bounds.size;
                    break;
                case ColliderShape.ConvexMesh:
                    foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>())
                    {
                        if (filter.sharedMesh == null) continue;
                        var mesh = filter.gameObject.AddComponent<MeshCollider>();
                        mesh.sharedMesh = filter.sharedMesh;
                        mesh.convex = true;
                    }
                    break;
            }
        }

        private enum PointKind
        {
            Muzzle,
            Hardpoint
        }

        // Muzzles sit at the nose, spread across the wings; rails sit under the hull at mid-length.
        private static Transform[] CreatePoints(Transform root, string label, int count, Bounds bounds, PointKind kind)
        {
            var parent = new GameObject(label + "s").transform;
            parent.SetParent(root, false);
            var points = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                float across = count == 1 ? 0f : Mathf.Lerp(-1f, 1f, i / (count - 1f));
                Vector3 local = kind == PointKind.Muzzle
                    ? new Vector3(bounds.center.x + across * bounds.extents.x * 0.5f, bounds.center.y, bounds.max.z)
                    : new Vector3(bounds.center.x + across * bounds.extents.x * 0.4f, bounds.min.y, bounds.center.z);
                points[i] = new GameObject($"{label} {i + 1}").transform;
                points[i].SetParent(parent, false);
                points[i].localPosition = local;
            }
            return points;
        }

        /// <summary>Renderer bounds in the space of <paramref name="space"/> (or of the model turned by <paramref name="rotation"/>).</summary>
        private static Bounds MeasureBounds(GameObject target, Quaternion rotation, Transform space = null)
        {
            var bounds = new Bounds();
            bool any = false;
            var corners = new List<Vector3>(8);
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
            {
                Bounds local;
                Transform from = renderer.transform;
                if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null) local = filter.sharedMesh.bounds;
                else if (renderer is SkinnedMeshRenderer skinned) local = skinned.localBounds;
                else continue;

                corners.Clear();
                for (int c = 0; c < 8; c++)
                    corners.Add(local.center + Vector3.Scale(local.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                foreach (Vector3 corner in corners)
                {
                    Vector3 world = from.TransformPoint(corner);
                    Vector3 point = space != null ? space.InverseTransformPoint(world) : rotation * target.transform.InverseTransformPoint(world);
                    if (!any) bounds = new Bounds(point, Vector3.zero);
                    else bounds.Encapsulate(point);
                    any = true;
                }
            }
            return bounds;
        }

        private static void EnsureSceneSystems()
        {
            if (FindObjectOfType<SpaceAIWorld>() == null)
                Undo.RegisterCreatedObjectUndo(new GameObject(nameof(SpaceAIWorld), typeof(SpaceAIWorld)), "Create ship");
            if (FindObjectOfType<ProjectileSystem>() == null)
            {
                var projectiles = new GameObject(nameof(ProjectileSystem)).AddComponent<ProjectileSystem>();
                projectiles.TracerMaterial = GunneryTestSceneBuilder.LoadOrCreateTracerMaterial();
                Undo.RegisterCreatedObjectUndo(projectiles.gameObject, "Create ship");
            }
            if (FindObjectOfType<MissileSystem>() == null)
            {
                var missiles = new GameObject(nameof(MissileSystem)).AddComponent<MissileSystem>();
                missiles.TrailMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                Undo.RegisterCreatedObjectUndo(missiles.gameObject, "Create ship");
            }
        }
    }
}
