using UnityEngine;
using UnityEditor;
using Abb2kTools.Utils;

namespace Abb2kTools.Utils.Editor
{
    [CustomEditor(typeof(FollowObject))]
    public class FollowObjectEditor : UnityEditor.Editor
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

        // Position Lerp Properties
        private SerializedProperty lerpSpeedProp;
        private SerializedProperty useCurveProp;
        private SerializedProperty lerpCurveProp;

        // Position Constant Speed
        private SerializedProperty moveSpeedProp;

        // Rotation Features
        private SerializedProperty rotateMovementTargetToMovementProp;
        private SerializedProperty movementRotationTargetProp;
        private SerializedProperty rotateToTargetProp;
        
        private SerializedProperty syncRotationToCustomTargetProp;
        private SerializedProperty syncRotationTargetProp;

        // Rotation Mode Properties
        private SerializedProperty rotationModeProp;
        private SerializedProperty rotationLerpSpeedProp;
        private SerializedProperty rotationUseCurveProp;
        private SerializedProperty rotationLerpCurveProp;
        private SerializedProperty rotationMoveSpeedProp;

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

            // Rotation
            rotateMovementTargetToMovementProp = serializedObject.FindProperty("rotateMovementTargetToMovement");
            movementRotationTargetProp = serializedObject.FindProperty("movementRotationTarget");
            
            rotateToTargetProp = serializedObject.FindProperty("rotateToTarget");
            
            syncRotationToCustomTargetProp = serializedObject.FindProperty("syncRotationToCustomTarget");
            syncRotationTargetProp = serializedObject.FindProperty("syncRotationTarget");

            rotationModeProp = serializedObject.FindProperty("rotationMode");
            rotationLerpSpeedProp = serializedObject.FindProperty("rotationLerpSpeed");
            rotationUseCurveProp = serializedObject.FindProperty("rotationUseCurve");
            rotationLerpCurveProp = serializedObject.FindProperty("rotationLerpCurve");
            rotationMoveSpeedProp = serializedObject.FindProperty("rotationMoveSpeed");
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

            EditorGUILayout.LabelField("Movement Modes", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(moveModeProp);

            // Determine current move mode (0 = Snap, 1 = Lerp, 2 = SLerp, 3 = ConstantSpeed)
            int currentMoveMode = moveModeProp.enumValueIndex;

            // --- LERP SETTINGS BOX ---
            if (currentMoveMode == 1 || currentMoveMode == 2)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Movement Lerp Settings", EditorStyles.boldLabel);
                
                EditorGUILayout.PropertyField(lerpSpeedProp);
                EditorGUILayout.PropertyField(useCurveProp);

                EditorGUI.BeginDisabledGroup(!useCurveProp.boolValue);
                EditorGUILayout.PropertyField(lerpCurveProp);
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            // --- CONSTANT SPEED BOX ---
            if (currentMoveMode == 3)
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
            EditorGUIUtility.labelWidth = 15f;
            
            EditorGUILayout.PropertyField(constrainsXProp, new GUIContent("X"), GUILayout.Width(45));
            EditorGUILayout.PropertyField(constrainsYProp, new GUIContent("Y"), GUILayout.Width(45));
            EditorGUILayout.PropertyField(constrainsZProp, new GUIContent("Z"), GUILayout.Width(45));
            
            EditorGUIUtility.labelWidth = originalLabelWidth;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // --- ROTATION SETTINGS BOX ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Rotation Toggles", EditorStyles.boldLabel);

            // 1. Target To Movement
            EditorGUILayout.PropertyField(rotateMovementTargetToMovementProp, new GUIContent("Rotate Target To Movement"));
            if (rotateMovementTargetToMovementProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(movementRotationTargetProp, new GUIContent("Custom Rot Target", "If empty, defaults to the main Target object"));
                EditorGUI.indentLevel--;
            }

            // 2. Current To Target
            EditorGUILayout.PropertyField(rotateToTargetProp, new GUIContent("Rotate Current To Target"));
            
            // 3. Sync Rotation
            EditorGUILayout.PropertyField(syncRotationToCustomTargetProp, new GUIContent("Sync Current Rot To Object"));
            if (syncRotationToCustomTargetProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(syncRotationTargetProp, new GUIContent("Sync Target Object"));
                EditorGUI.indentLevel--;
            }

            // Rotation Modes
            bool needsRotationModes = rotateMovementTargetToMovementProp.boolValue || rotateToTargetProp.boolValue;
            if (needsRotationModes)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Rotation Modes", EditorStyles.boldLabel);
                
                EditorGUILayout.PropertyField(rotationModeProp, new GUIContent("Mode"));
                int currentRotMode = rotationModeProp.enumValueIndex;

                if (currentRotMode == 1 || currentRotMode == 2)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.PropertyField(rotationLerpSpeedProp, new GUIContent("Lerp Speed"));
                    EditorGUILayout.PropertyField(rotationUseCurveProp, new GUIContent("Use Curve"));

                    EditorGUI.BeginDisabledGroup(!rotationUseCurveProp.boolValue);
                    EditorGUILayout.PropertyField(rotationLerpCurveProp, new GUIContent("Curve"));
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.EndVertical();
                }
                else if (currentRotMode == 3)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.PropertyField(rotationMoveSpeedProp, new GUIContent("Speed (Degrees/Sec)"));
                    EditorGUILayout.EndVertical();
                }
            }
            
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }
    }
}