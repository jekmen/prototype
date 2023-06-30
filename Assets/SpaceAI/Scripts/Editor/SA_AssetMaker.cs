using System.IO;
using UnityEditor;
using UnityEngine;

namespace SpaceAI.DataManagment
{
    public class MakeScriptableObject
    {
        [MenuItem("SpaceAI/Create/FlightConfig")]
        public static void CreateAssetFlightConfig()
        {
            string path = "SpaceAI/Data/FlightConfigs/";

            SA_ShipConfigurationManager asset = ScriptableObject.CreateInstance<SA_ShipConfigurationManager>();

            if (!Directory.Exists(Path.Combine(Application.dataPath, path)))
            {
                Directory.CreateDirectory(Path.Combine(Application.dataPath, path));
                AssetDatabase.CreateAsset(asset, "Assets/" + path + "FlightConfiguration.asset");
            }
            else
            {
                AssetDatabase.CreateAsset(asset, "Assets/" + path + "FlightConfiguration.asset");
            }

            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
            AssetDatabase.Refresh();
        }

        [MenuItem("SpaceAI/Create/ItemsStaf")]
        public static void CreateMyAsset()
        {
            string path = "SpaceAI/Resources/Items/";

            SA_ItemsStaf asset = ScriptableObject.CreateInstance<SA_ItemsStaf>();

            if (!Directory.Exists(Path.Combine(Application.dataPath, path)))
            {
                Directory.CreateDirectory(Path.Combine(Application.dataPath, path));
                AssetDatabase.CreateAsset(asset, "Assets/" + path + "ItemsStaf.asset");
            }
            else
            {
                AssetDatabase.CreateAsset(asset, "Assets/" + path + "ItemsStaf.asset");
            }

            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
            AssetDatabase.Refresh();
        }
    }
}