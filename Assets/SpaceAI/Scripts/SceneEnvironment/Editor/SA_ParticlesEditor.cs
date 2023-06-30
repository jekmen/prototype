﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SpaceAI.SceneEnvironment
{
    [CustomEditor(typeof(SA_SpaceFlightParticles))]
    public class SA_ParticlesEditor : Editor
    {
        // Range Values Configuration
        private int _displayMinParticleCount = 1;
        private int _displayMaxParticleCount = 3000;
        private int _displayMinRange = 1;
        private int _displayMaxRange = 1000;

        // Warning Threshholds
        private int _warningHighParticleCount = 1000;

        // Serialized Object
        SerializedObject myTarget;

        // Serialized Properties
        SerializedProperty maxParticles;
        SerializedProperty range;
        SerializedProperty distanceSpawn;
        SerializedProperty fadeParticles;
        SerializedProperty distanceFade;
        SerializedProperty minParticleSize;
        SerializedProperty maxParticleSize;
        SerializedProperty scaleMultiplier;
        SerializedProperty minParticleDriftSpeed;
        SerializedProperty maxParticleDriftSpeed;
        SerializedProperty driftSpeedMultiplier;

        // Temporary variables since properties can't be modified directly when using Ref and/or Out paremeters
        private float _minSize;
        private float _maxSize;
        private float _minDriftSpeed;
        private float _maxDriftSpeed;

        void OnEnable()
        {
            // Reference the serialized object (instance of SpaceParticles.cs)
            myTarget = new SerializedObject(target);

            // Find and reference the properties of the target object		
            maxParticles = myTarget.FindProperty("maxParticles");
            range = myTarget.FindProperty("range");
            distanceSpawn = myTarget.FindProperty("distanceSpawn");
            fadeParticles = myTarget.FindProperty("fadeParticles");
            distanceFade = myTarget.FindProperty("distanceFade");
            minParticleSize = myTarget.FindProperty("minParticleSize");
            maxParticleSize = myTarget.FindProperty("maxParticleSize");
            scaleMultiplier = myTarget.FindProperty("sizeMultiplier");
            minParticleDriftSpeed = myTarget.FindProperty("minParticleDriftSpeed");
            maxParticleDriftSpeed = myTarget.FindProperty("maxParticleDriftSpeed");
            driftSpeedMultiplier = myTarget.FindProperty("driftSpeedMultiplier");

        }

        // Override the OnInspectorGUI and present these EditorGUI gadgets instead of the default ones
        //public void OnInspectorGUIa() {
        public override void OnInspectorGUI()
        {
            // Update the serialized object
            myTarget.Update();

            // Present inspector GUI gadgets/objects and modify AsteroidField.cs instances with configured values
            maxParticles.intValue = EditorGUILayout.IntSlider("Number of Particles", maxParticles.intValue, _displayMinParticleCount, _displayMaxParticleCount);
            if (maxParticles.intValue > _warningHighParticleCount)
            {
                EditorGUILayout.LabelField("Warning! Many particles may impact performance! Consider smaller range and fewer aprticles instead.", EditorStyles.wordWrappedMiniLabel);
            }
            range.floatValue = EditorGUILayout.Slider("Range", range.floatValue, _displayMinRange, _displayMaxRange);
            if (range.floatValue > Camera.main.farClipPlane)
            {
                EditorGUILayout.LabelField("Warning! Main camera clipping plane is closer than particles range.", EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.LabelField("Range is distance from the center to the edge of the particle system. If the transform of SpaceParticles moves, particles " +
                "that become out of range will respawn to a new location at spawn distance of range.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Separator();
            distanceSpawn.floatValue = EditorGUILayout.Slider("Spawn at % of Range", distanceSpawn.floatValue, 0.0f, 1.0f);
            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("Particle Size (Min/Max Range)", EditorStyles.boldLabel);
            _minSize = minParticleSize.floatValue;
            _maxSize = maxParticleSize.floatValue;
            GUIContent _scaleContent = new GUIContent(string.Format("Min:{0:F2}, Max:{1:F2}", _minSize, _maxSize));
            EditorGUILayout.MinMaxSlider(_scaleContent, ref _minSize, ref _maxSize, 0.01f, 1.0f);
            minParticleSize.floatValue = _minSize;
            maxParticleSize.floatValue = _maxSize;
            scaleMultiplier.floatValue = EditorGUILayout.FloatField("Size Multiplier", scaleMultiplier.floatValue);
            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("Particle Drift Speed (Min/Max Range)", EditorStyles.boldLabel);
            _minDriftSpeed = minParticleDriftSpeed.floatValue;
            _maxDriftSpeed = maxParticleDriftSpeed.floatValue;
            GUIContent _driftContent = new GUIContent(string.Format("Min:{0:F1}, Max:{1:F1}", _minDriftSpeed, _maxDriftSpeed));
            EditorGUILayout.MinMaxSlider(_driftContent, ref _minDriftSpeed, ref _maxDriftSpeed, 0.0f, 1.0f);
            minParticleDriftSpeed.floatValue = _minDriftSpeed;
            maxParticleDriftSpeed.floatValue = _maxDriftSpeed;
            driftSpeedMultiplier.floatValue = EditorGUILayout.FloatField("Drift Speed Multiplier", driftSpeedMultiplier.floatValue);
            EditorGUILayout.Separator();

            // Visual Settings
            EditorGUILayout.LabelField("Visual Settings", EditorStyles.boldLabel);
            fadeParticles.boolValue = EditorGUILayout.Toggle("Fade Particles", fadeParticles.boolValue);
            if (fadeParticles.boolValue)
            {
                distanceFade.floatValue = EditorGUILayout.Slider("Fade from % of Spawn", distanceFade.floatValue, 0.0f, 1.0f);
                EditorGUILayout.LabelField("Alpha is 1.0 at distanceFade*distanceSpawn*range and " +
                "gradually fades out to 0.0 at distanceSpawn*range.", EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.Separator();

            // Apply the modified properties
            myTarget.ApplyModifiedProperties();
        }


        // Function to ovveride and display custom object array in inspector
        void ArrayGUI(SerializedObject obj, string name)
        {
            int size = obj.FindProperty(name + ".Array.size").intValue;
            int newSize = EditorGUILayout.IntField("Size", size);
            if (newSize != size) obj.FindProperty(name + ".Array.size").intValue = newSize;
            EditorGUI.indentLevel = 3;
            for (int i = 0; i < newSize; i++)
            {
                var prop = obj.FindProperty(string.Format("{0}.Array.data[{1}]", name, i));
                EditorGUILayout.PropertyField(prop);
            }
            EditorGUI.indentLevel = 0;
        }
    }
}
