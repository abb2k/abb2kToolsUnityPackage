using UnityEngine;
using UnityEditor;

namespace Abb2kTools.Utils
{
    [CustomEditor(typeof(SpriteRenderer3D))]
    public class SpriteRenderer3DEditor : Editor
    {
        private SerializedProperty spriteProp;
        private SerializedProperty colorProp;
        private SerializedProperty flipXProp;
        private SerializedProperty flipYProp;
        private SerializedProperty drawModeProp;
        private SerializedProperty sizeProp;
        private SerializedProperty surfaceTypeProp;
        private SerializedProperty opaqueMaterialProp;
        private SerializedProperty transparentMaterialProp;
        private SerializedProperty setNativeSizeForYProp;

        private void OnEnable()
        {
            spriteProp = serializedObject.FindProperty("_sprite");
            colorProp = serializedObject.FindProperty("_color");
            flipXProp = serializedObject.FindProperty("_flipX");
            flipYProp = serializedObject.FindProperty("_flipY");
            drawModeProp = serializedObject.FindProperty("_drawMode");
            sizeProp = serializedObject.FindProperty("_size");
            surfaceTypeProp = serializedObject.FindProperty("_surfaceType");
            opaqueMaterialProp = serializedObject.FindProperty("opaqueMaterial");
            transparentMaterialProp = serializedObject.FindProperty("transparentMaterial");
            setNativeSizeForYProp = serializedObject.FindProperty("setNativeSizeForY");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SpriteRenderer3D script = (SpriteRenderer3D)target;

            // Odin [PropertyOrder(-1)] equivalent: Button at the very top
            if (GUILayout.Button("Open Sprite Editor", GUILayout.Height(25)))
            {
                script.OpenSpriteEditor();
            }
            EditorGUILayout.Space();

            // We use a helper method to handle Odin's [OnValueChanged] logic easily
            DrawPropertyWithChangeCheck(spriteProp, "Sprite", script);
            DrawPropertyWithChangeCheck(colorProp, "Color", script);
            DrawPropertyWithChangeCheck(flipXProp, "Flip X", script);
            DrawPropertyWithChangeCheck(flipYProp, "Flip Y", script);
            DrawPropertyWithChangeCheck(drawModeProp, "Draw Mode", script);

            // Odin [ShowIf] equivalent
            if (drawModeProp.enumValueIndex != (int)SpriteRenderer3D.DrawMode.Simple)
            {
                DrawPropertyWithChangeCheck(sizeProp, "Size", script);
            }

            EditorGUILayout.Space();

            DrawPropertyWithChangeCheck(surfaceTypeProp, "Surface Type", script);

            // Odin [Required] equivalents
            EditorGUILayout.PropertyField(opaqueMaterialProp, new GUIContent("Opaque Material"));
            if (opaqueMaterialProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Opaque Material is required.", MessageType.Warning);
            }

            EditorGUILayout.PropertyField(transparentMaterialProp, new GUIContent("Transparent Material"));
            if (transparentMaterialProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Transparent Material is required.", MessageType.Warning);
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(setNativeSizeForYProp);

            EditorGUILayout.Space();
            
            // Bottom button
            if (GUILayout.Button("Set Native Size", GUILayout.Height(25)))
            {
                script.SetNativeSize();
            }

            serializedObject.ApplyModifiedProperties();
        }

        // Helper function to replicate Odin's [OnValueChanged("UpdateMaterialData")]
        private void DrawPropertyWithChangeCheck(SerializedProperty prop, string label, SpriteRenderer3D script)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                script.UpdateMaterialData();
            }
        }
    }
}