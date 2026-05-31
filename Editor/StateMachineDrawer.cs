using UnityEngine;
using UnityEditor;

namespace Abb2kTools
{
    [CustomPropertyDrawer(typeof(StateMachine))]
    public class StateMachineDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.LabelField(position, label);
            if (GUI.Button(new Rect(position.x + 100, position.y, 100, position.height), "Edit"))
            {
                StateMachine machine = (StateMachine)fieldInfo.GetValue(property.serializedObject.targetObject);
                StateMachineWIndow.SetWindowTo(machine);
                StateMachineWIndow.OpenWindow();
            }
        }
    }
}
