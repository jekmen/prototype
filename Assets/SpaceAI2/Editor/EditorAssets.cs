using UnityEditor;
using UnityEngine;

namespace SpaceAI2.Editor
{
    /// <summary>Asset paths and small helpers shared by the editor tools.</summary>
    internal static class EditorAssets
    {
        public const string Root = "Assets/SpaceAI2";
        public const string Profiles = Root + "/Profiles/";
        public const string SystemsPrefab = Root + "/Prefabs/SpaceAI Systems.prefab";
        private const string ModelMaterialPath = Root + "/Content/Materials/SciFiSpace.mat";
        private const string EnemyMaterialPath = Root + "/Content/Materials/SciFiSpace_Red.mat";

        /// <summary>Gives the fighter model the red livery on any team but the first.</summary>
        public static void PaintTeam(SpaceShip ship)
        {
            var standard = AssetDatabase.LoadAssetAtPath<Material>(ModelMaterialPath);
            var enemy = AssetDatabase.LoadAssetAtPath<Material>(EnemyMaterialPath);
            if (standard == null || enemy == null) return;
            Material from = ship.Team == 0 ? enemy : standard;
            Material to = ship.Team == 0 ? standard : enemy;
            foreach (Renderer renderer in ship.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != from) continue;
                    materials[i] = to;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
