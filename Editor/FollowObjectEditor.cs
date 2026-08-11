using UnityEngine;
using UnityEditor;
using Abb2kTools.Utils;

namespace Abb2kTools.Utils
{
    [CustomEditor(typeof(FollowObject))]
    public class FollowObjectEditor : Editor
    {
        // General Properties
        private SerializedProperty targetProp;
        private SerializedProperty offsetProp;
        private SerializedProperty forwardOffsetProp;
        private SerializedProperty forwardOffsetSmoothnessProp;
        private SerializedProperty moveModeProp;
        private SerializedProperty frameMovementProp;
        
        // Constraints
        private SerializedProperty constrainsXProp;
        private SerializedProperty constrainsYProp;
        private SerializedProperty constrainsZProp;

        // Lerp Properties
        private SerializedProperty lerpSpeedProp;
        private SerializedProperty useCurveProp;
        private SerializedProperty lerpCurveProp;

        // Constant Speed
        private SerializedProperty moveSpeedProp;

        private void OnEnable()
        {
            targetProp = serializedObject.FindProperty("target");
            offsetProp = serializedObject.FindProperty("offset");
            forwardOffsetProp = serializedObject.FindProperty("forwardOffset");
            forwardOffsetSmoothnessProp = serializedObject.FindProperty("forwardOffsetSmoothness");
            moveModeProp = serializedObject.FindProperty("moveMode");
            frameMovementProp = serializedObject.FindProperty("frameMovement");

            SerializedProperty constrainsProp = serializedObject.FindProperty("constrains");
            constrainsXProp = constrainsProp.FindPropertyRelative("x");
            constrainsYProp = constrainsProp.FindPropertyRelative("y");
            constrainsZProp = constrainsProp.FindPropertyRelative("z");

            lerpSpeedProp = serializedObject.FindProperty("lerpSpeed");
            useCurveProp = serializedObject.FindProperty("useCurve");
            lerpCurveProp = serializedObject.FindProperty("lerpCurve");

            moveSpeedProp = serializedObject.FindProperty("moveSpeed");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // --- GENERAL BOX ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(targetProp);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(offsetProp);
            EditorGUILayout.PropertyField(forwardOffsetProp);
            EditorGUILayout.PropertyField(forwardOffsetSmoothnessProp);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Modes", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(moveModeProp);

            // Determine current move mode (0 = Snap, 1 = Lerp, 2 = SLerp, 3 = ConstantSpeed)
            int currentMoveMode = moveModeProp.enumValueIndex;
            bool showLerpSettings = currentMoveMode == 1 || currentMoveMode == 2;
            bool showConstantSpeed = currentMoveMode == 3;

            // --- LERP SETTINGS BOX ---
            if (showLerpSettings)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Lerp Settings", EditorStyles.boldLabel);
                
                EditorGUILayout.PropertyField(lerpSpeedProp);
                EditorGUILayout.PropertyField(useCurveProp);

                // Enable/Disable Curve based on useCurve bool (Matches Odin's EnableIf)
                EditorGUI.BeginDisabledGroup(!useCurveProp.boolValue);
                EditorGUILayout.PropertyField(lerpCurveProp);
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndVertical();

                EditorGUILayout.Space();
            }

            // --- CONSTANT SPEED BOX ---
            if (showConstantSpeed)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Constant Speed Settings", EditorStyles.boldLabel);
                
                EditorGUILayout.PropertyField(moveSpeedProp);
                
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space();
            }

            EditorGUILayout.PropertyField(frameMovementProp);

            EditorGUILayout.Space();

            // Inline Horizontal Constrains
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Constrains");
            
            float originalLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 15f; // Matches Odin's LabelWidth = 15
            
            EditorGUILayout.PropertyField(constrainsXProp, new GUIContent("X"), GUILayout.Width(45));
            EditorGUILayout.PropertyField(constrainsYProp, new GUIContent("Y"), GUILayout.Width(45));
            EditorGUILayout.PropertyField(constrainsZProp, new GUIContent("Z"), GUILayout.Width(45));
            
            EditorGUIUtility.labelWidth = originalLabelWidth;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();

            

            serializedObject.ApplyModifiedProperties();
        }
    }
}