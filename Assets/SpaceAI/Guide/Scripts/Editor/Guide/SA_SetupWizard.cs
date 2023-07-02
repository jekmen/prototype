using SpaceAI.Ship;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpaceAI.Guide
{
    [InitializeOnLoad]
    public class SA_SetupWizard : EditorWindow
    {
        const string _openKey = "open";
        private SA_SetupWizardScriptable _prefabAssemblerData;
        private Editor _previewWindow;
        private GUIStyle bgColor;
        private GameObject ship;
        private string msg;
        private string docsFile;

        static SA_SetupWizard()
        {
            //if (PlayerPrefs.GetInt(_openKey) == 0)
            //{
            //    ShowExample();
            //}
        }

        [MenuItem("SpaceAI/Setup Wizard")]
        static void Init()
        {
            //PlayerPrefs.SetInt(_openKey, 1);
            SA_SetupWizard window = (SA_SetupWizard)GetWindow(typeof(SA_SetupWizard));
            window.Show();
            window.titleContent = new GUIContent("Setup Wizard");
            
        }

        private void OnEnable()
        {
            docsFile = Application.dataPath + "/SpaceAI/Guide/SpaceAIDocumentation.pdf";

            string[] guid = AssetDatabase.FindAssets("t:" + typeof(SA_SetupWizardScriptable).ToString(), null);

            if (guid.Length > 0 && !string.IsNullOrEmpty(guid[0]))
            {
                _prefabAssemblerData = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid[0]), typeof(SA_SetupWizardScriptable)) as SA_SetupWizardScriptable;
            }

            _previewWindow = Editor.CreateEditor(_prefabAssemblerData.model);

            bgColor = new GUIStyle();

            bgColor.normal.background = _prefabAssemblerData.bg;           

            try
            {
                EditorSceneManager.OpenScene("Assets/SpaceAI/Guide/Scenes/Guide.unity", OpenSceneMode.Single);
            }
            catch (Exception)
            {

            }
        }

        private void OnGUI()
        {
            _previewWindow.OnInteractivePreviewGUI(GUILayoutUtility.GetRect(256, 256), bgColor);

            msg = EditorApplication.isPlaying ? _prefabAssemblerData.fiveMsg : _prefabAssemblerData.firstMsg;

            GUILayout.Box(msg);

            if (!ship && GUILayout.Button("Setup base model"))
            {
                if (ship) return;

                ship = Instantiate(_prefabAssemblerData.model);

                for (int i = 0; i < ship.transform.childCount; i++)
                {
                    ship.transform.GetChild(i).GetComponent<MeshRenderer>().material = _prefabAssemblerData.materials[i];
                }

                Selection.activeGameObject = ship;

                SceneView.FrameLastActiveSceneView();

                msg = _prefabAssemblerData.secondMsg;
            }

            if (ship && !ship.GetComponent<SA_ShipController>() && GUILayout.Button("Put component"))
            {
                ship.AddComponent(_prefabAssemblerData.script.GetClass());

                ship.GetComponent<SA_ShipController>().Validate();

                msg = _prefabAssemblerData.trdMsg;
            }

            if (ship && ship.GetComponent<SA_ShipController>() && !ship.GetComponent<BoxCollider>() && GUILayout.Button("Done"))
            {
                var box = ship.AddComponent<BoxCollider>();

                box.size = new Vector3(4, 1, 5);

                msg = _prefabAssemblerData.frtMsg;
            }

            if (!EditorApplication.isPlaying && ship && ship.GetComponent<SA_ShipController>() && ship.GetComponent<BoxCollider>() && GUILayout.Button("Test flight"))
            {
                EditorApplication.EnterPlaymode();

                Instantiate(_prefabAssemblerData.asteroidField);

                msg = _prefabAssemblerData.fiveMsg;
            }

            if (GUILayout.Button("Open documentation"))
            {
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.ExitPlaymode();
                }

                Application.OpenURL("file:///" + docsFile);
            }
        }
    }
}