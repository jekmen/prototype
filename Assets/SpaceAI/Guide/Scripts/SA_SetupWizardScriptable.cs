using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpaceAI.Guide
{
    [CreateAssetMenu(fileName = "SetupWizard", menuName = "SpaceAI/SetupWizard")]
    public class SA_SetupWizardScriptable : ScriptableObject
    {
        public Texture2D bg;
        public GameObject model;
        public GameObject asteroidField;
        public MonoScript script;
        public Material[] materials;
        public string firstMsg;
        public string secondMsg;
        public string trdMsg;
        public string frtMsg;
        public string fiveMsg;
    }
}