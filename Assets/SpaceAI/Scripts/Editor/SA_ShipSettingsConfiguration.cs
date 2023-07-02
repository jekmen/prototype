using SpaceAI.Ship;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Reflection;
using SpaceAI.DataManagment;

namespace SpaceAI.DataManagment
{
    [CustomEditor(typeof(SA_ShipConfigurationManager))]
    [CanEditMultipleObjects]
    public class SA_ShipSettingsConfiguration : Editor
    {
        SA_ShipConfigurationManager script;
        SerializedProperty property;
        SerializedObject configObject;

        public const string PATH_PREFIX = "Configurations";
        public string DATA_PATH;
        public string bntText = "";

        void OnEnable()
        {
            script = (SA_ShipConfigurationManager)target;
            configObject = new SerializedObject(script);
            DATA_PATH = Path.Combine(Application.streamingAssetsPath + "/" + PATH_PREFIX, script.name + ".xml");
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Flight Configuration");
            EditorGUILayout.Space();
            configObject.Update();

            foreach (var item in script.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var property = configObject.FindProperty(item.Name);
                EditorGUILayout.PropertyField(property, true);
            }

            bool validate = script.ShipSystems.shipSystems != null && script.ShipSystems.shipSystemsScripts != null
                            && script.ShipSystems.shipSystems.Count != script.ShipSystems.shipSystemsScripts.Count;

            if (validate)
            {
                script.ShipSystems.OnValidate();
            }

            if (File.Exists(DATA_PATH))
            {
                bntText = "Override";
            }
            else
            {
                bntText = "Save";
            }

            #region Buttons
            if (GUILayout.Button("Set Default Settings"))
            {
                GenereteData(configObject, property);
            }

            if (GUILayout.Button(bntText))
            {
                SaveData(configObject, property);
            }

            if (File.Exists(DATA_PATH))
            {
                if (GUILayout.Button("Load"))
                {
                    script = script.Load(script.name);
                    configObject = new SerializedObject(script);
                    foreach (var item in script.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        var property = configObject.FindProperty(item.Name);
                        EditorGUILayout.PropertyField(property, true);
                    }
                }
            }

            configObject.ApplyModifiedProperties();
            #endregion
        }

        private void SaveData(SerializedObject serializedObject, SerializedProperty property)
        {
            if (Directory.Exists(Application.streamingAssetsPath + "/" + PATH_PREFIX))
            {
                script.Save(script.name);
            }
            else
            {
                Directory.CreateDirectory(Application.streamingAssetsPath + "/" + PATH_PREFIX);
                script.Save(script.name);
            }

            AssetDatabase.Refresh();
        }

        private void GenereteData(SerializedObject serializedObject, SerializedProperty property)
        {
            script.SetAsDefault();
            serializedObject = new SerializedObject(script);
        }
    }

    [CustomEditor(typeof(SA_BaseShip), true)]
    public class BaseShipLoader : Editor
    {
        SA_BaseShip sA_BaseShip;
        int iButtonWidth = 220;

        private void OnEnable()
        {
            sA_BaseShip = (SA_BaseShip)target;
        }

        public override void OnInspectorGUI()
        {
            SetUp();
        }

        void SetUp()
        {
            if (!Directory.Exists(Path.Combine(Application.streamingAssetsPath, "Configurations")) || Directory.GetFiles(Path.Combine(Application.streamingAssetsPath, "Configurations")).Length == 0)
            {
                GUILayout.BeginVertical();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                sA_BaseShip.configFileName = "";
                EditorGUILayout.HelpBox("First you need to create a configuration file. SpaceAI->Create->FlightConfig", MessageType.Info);

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                return;
            }

            if (string.IsNullOrEmpty(sA_BaseShip.configFileName))
            {
                GUILayout.Space(15);

                foreach (var item in Directory.GetFiles(Path.Combine(Application.streamingAssetsPath, "Configurations")))
                {
                    GUILayout.BeginVertical();
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();

                    if (!item.Contains(".meta") && GUILayout.Button("Load: " + Path.GetFileNameWithoutExtension(item), GUILayout.Width(iButtonWidth)))
                    {
                        sA_BaseShip.currFile = item;
                        sA_BaseShip.configFileName = Path.GetFileNameWithoutExtension(item);
                        EditorUtility.SetDirty(sA_BaseShip);
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                }
            }
            else
            {
                if (!File.Exists(sA_BaseShip.currFile))
                {
                    sA_BaseShip.configFileName = "";
                }

                GUILayout.Space(15);

                EditorGUILayout.HelpBox("Current file: " + sA_BaseShip.configFileName, MessageType.Info);

                GUILayout.BeginVertical();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (!Application.isPlaying && GUILayout.Button("Clear", GUILayout.Width(iButtonWidth)))
                {
                    sA_BaseShip.configFileName = "";
                    EditorUtility.SetDirty(sA_BaseShip);
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();

                GUILayout.Space(5);

                GUILayout.BeginVertical();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (!Application.isPlaying && GUILayout.Button("Open: " + sA_BaseShip.configFileName, GUILayout.Width(iButtonWidth)))
                {
                    string path = "Assets/SpaceAI/Data/FlightConfigs/";
                    SA_ShipConfigurationManager scriptObject = (SA_ShipConfigurationManager)AssetDatabase.LoadAssetAtPath(path + sA_BaseShip.configFileName + ".asset", typeof(SA_ShipConfigurationManager));
                    Selection.activeObject = scriptObject;
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            GUILayout.Space(5);

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (!Application.isPlaying && GUILayout.Button("Validate", GUILayout.Width(iButtonWidth)))
            {
                sA_BaseShip.Validate();
                EditorUtility.SetDirty(sA_BaseShip);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
    }
}