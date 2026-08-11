using UnityEngine;
using UnityEditor;
using Abb2kTools.Utils;

namespace Abb2kTools.EditorScripts
{
    [CustomEditor(typeof(HitSender))]
    public class HitSenderEditor : Editor
    {
        private SerializedProperty hitRecieverObjProp;
        private SerializedProperty hitIDProp;
        
        private SerializedProperty send3DProp;
        private SerializedProperty sendCollision3DProp;
        private SerializedProperty sendTrigger3DProp;
        
        private SerializedProperty send2DProp;
        private SerializedProperty sendCollision2DProp;
        private SerializedProperty sendTrigger2DProp;

        private void OnEnable()
        {
            hitRecieverObjProp = serializedObject.FindProperty("_hitRecieverObj");
            hitIDProp = serializedObject.FindProperty("hitID");
            
            send3DProp = serializedObject.FindProperty("send3D");
            sendCollision3DProp = serializedObject.FindProperty("sendCollision3D");
            sendTrigger3DProp = serializedObject.FindProperty("sendTrigger3D");
            
            send2DProp = serializedObject.FindProperty("send2D");
            sendCollision2DProp = serializedObject.FindProperty("sendCollision2D");
            sendTrigger2DProp = serializedObject.FindProperty("sendTrigger2D");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            HitSender script = (HitSender)target;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Reference", EditorStyles.boldLabel);
            
            // Draw the GameObject property
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(hitRecieverObjProp, new GUIContent("Hit Reciever Obj"));
            if (EditorGUI.EndChangeCheck())
            {
                script.HitRecieverObj = hitRecieverObjProp.objectReferenceValue as GameObject;
            }

            if (script.HitReciever == null && hitRecieverObjProp.objectReferenceValue != null)
            {
                GameObject obj = hitRecieverObjProp.objectReferenceValue as GameObject;
                if (obj.GetComponent<IHitReciever>() == null)
                {
                    EditorGUILayout.HelpBox("To work the 'HitReciever' interface must be set, or the object in this field must have a 'IHitReciever' component on it.", MessageType.Warning);
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(hitIDProp);

            EditorGUILayout.PropertyField(send3DProp, new GUIContent("Send 3D"));
            if (send3DProp.boolValue)
            {
                EditorGUILayout.BeginVertical("helpbox");
                
                float originalLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 52.5f;

                EditorGUILayout.PropertyField(sendCollision3DProp, new GUIContent("Collision"));
                EditorGUILayout.PropertyField(sendTrigger3DProp, new GUIContent("Trigger"));

                EditorGUIUtility.labelWidth = originalLabelWidth;
                
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.PropertyField(send2DProp, new GUIContent("Send 2D"));
            if (send2DProp.boolValue)
            {
                EditorGUILayout.BeginVertical("helpbox");

                float originalLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 52.5f;

                EditorGUILayout.PropertyField(sendCollision2DProp, new GUIContent("Collision"));
                EditorGUILayout.PropertyField(sendTrigger2DProp, new GUIContent("Trigger"));

                EditorGUIUtility.labelWidth = originalLabelWidth;
                
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }
    }
}