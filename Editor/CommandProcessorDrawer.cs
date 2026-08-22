#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Abb2kTools.Commands;

namespace Abb2kTools.EditorScripts
{
    // =================================================================================
    // TEMPORARY SCRIPTABLE OBJECT FOR NATIVE DRAWING
    // This allows the "Execute Manually" box to natively draw absolutely any data type!
    // =================================================================================
    public class CommandTesterSO : ScriptableObject
    {
        [SerializeReference] public ICommand testingCommand;
        public string executionReason = "";
    }

    [CustomPropertyDrawer(typeof(CommandProcessor))]
    public class CommandProcessorDrawer : PropertyDrawer
    {
        private const float LineHeight = 20f;
        private const float Padding = 4f;
        private const float BoxPadding = 8f;

        private static Dictionary<Type, FieldInfo[]> _typeFieldsCache = new Dictionary<Type, FieldInfo[]>();
        
        private GUIStyle _richTextLabel;
        private GUIStyle _boldFoldout;

        private readonly Dictionary<string, ExecutionState> _states = new Dictionary<string, ExecutionState>();

        private class ExecutionState
        {
            public bool isTestingExpanded = false;
            public bool isHistoryExpanded = true; 
            
            public HashSet<int> expandedHistoryItems = new HashSet<int>();
            public int currentPage = 0;
            public int itemsPerPage = 10;

            // Native Serialization Wrappers
            public CommandTesterSO testerSO;
            public SerializedObject testerSerializedObj;
            public SerializedProperty testingCommandProp;
            public SerializedProperty executionReasonProp;
        }

        private GUIStyle GetRichTextLabel()
        {
            if (_richTextLabel == null) _richTextLabel = new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };
            return _richTextLabel;
        }

        private GUIStyle GetBoldFoldout()
        {
            if (_boldFoldout == null) _boldFoldout = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            return _boldFoldout;
        }

        private ExecutionState GetState(string propPath, SerializedProperty property)
        {
            if (!_states.TryGetValue(propPath, out var state))
            {
                state = new ExecutionState();
                
                state.testerSO = ScriptableObject.CreateInstance<CommandTesterSO>();
                state.testerSO.hideFlags = HideFlags.DontSave; 
                state.testerSerializedObj = new SerializedObject(state.testerSO);
                state.testingCommandProp = state.testerSerializedObj.FindProperty("testingCommand");
                state.executionReasonProp = state.testerSerializedObj.FindProperty("executionReason");

                // Listen to Unity's global Undo/Redo events
                Undo.undoRedoPerformed += () => 
                {
                    if (property.serializedObject != null)
                    {
                        property.serializedObject.Update();
                        GUI.changed = true;
                    }
                };
                
                _states[propPath] = state;
            }
            return state;
        }

        private FieldInfo[] GetCommandFields(Type type)
        {
            if (!_typeFieldsCache.TryGetValue(type, out var fields))
            {
                fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                             .Where(f => !f.Name.Contains("<"))
                             .ToArray();
                _typeFieldsCache[type] = fields;
            }
            return fields;
        }

        private CommandProcessor GetProcessor(SerializedProperty property)
        {
            object obj = property.serializedObject.targetObject;
            if (obj == null) return null;

            string path = property.propertyPath.Replace(".Array.data[", "[");
            string[] elements = path.Split('.');

            foreach (var element in elements)
            {
                if (element.Contains("["))
                {
                    string elementName = element.Substring(0, element.IndexOf('['));
                    int index = Convert.ToInt32(element.Substring(element.IndexOf('[')).Replace("[", "").Replace("]", ""));
                    obj = GetFieldValue(obj, elementName);
                    obj = GetCollectionElementValue(obj, index);
                }
                else
                {
                    obj = GetFieldValue(obj, element);
                }

                if (obj == null) break;
            }

            if (obj is CommandProcessor processor) return processor;
            return CreateAndAssignProcessor(property);
        }

        private object GetFieldValue(object source, string name)
        {
            if (source == null) return null;
            Type type = source.GetType();
            
            var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (field != null) return field.GetValue(source);

            var prop = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (prop != null) return prop.GetValue(source);

            return null;
        }

        private object GetCollectionElementValue(object source, int index)
        {
            if (source is System.Collections.IEnumerable enumerable)
            {
                int i = 0;
                foreach (var item in enumerable)
                {
                    if (i == index) return item;
                    i++;
                }
            }
            return null;
        }

        private CommandProcessor CreateAndAssignProcessor(SerializedProperty property)
        {
            object targetObject = property.serializedObject.targetObject;
            if (targetObject == null) return null;

            Type targetType = targetObject.GetType();
            FieldInfo field = targetType.GetField(property.propertyPath, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (field != null)
            {
                var newProcessor = new CommandProcessor();
                field.SetValue(targetObject, newProcessor);
                property.serializedObject.ApplyModifiedProperties();
                return newProcessor;
            }

            var propField = targetType.GetField(property.name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (propField != null)
            {
                var newProcessor = new CommandProcessor();
                propField.SetValue(targetObject, newProcessor);
                property.serializedObject.ApplyModifiedProperties();
                return newProcessor;
            }
            return null;
        }

        // =========================================================================
        // HEIGHT CALCULATION
        // =========================================================================

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return LineHeight;
            CommandProcessor processor = GetProcessor(property);
            if (processor == null) return LineHeight;

            processor.SyncUndoState();

            ExecutionState state = GetState(property.propertyPath, property);
            float totalHeight = LineHeight + Padding; 

            // Box 1 (Testing) Height
            float box1Height = (LineHeight * 2) + (BoxPadding * 2);
            if (state.isTestingExpanded)
            {
                // Rely on native Unity height calculations for accuracy!
                box1Height += EditorGUI.GetPropertyHeight(state.testingCommandProp, true) + Padding;
                box1Height += EditorGUI.GetPropertyHeight(state.executionReasonProp, true) + Padding;
                box1Height += LineHeight + Padding; // Execute btn
            }
            totalHeight += box1Height + Padding;

            // Box 2 (History) Height
            float box2Lines = 1; 
            if (state.isHistoryExpanded)
            {
                int historyCount = processor.History != null ? processor.History.Count : 0;
                int totalPages = Mathf.Max(1, Mathf.CeilToInt(historyCount / (float)state.itemsPerPage));
                state.currentPage = Mathf.Clamp(state.currentPage, 0, totalPages - 1);

                box2Lines += processor.IsUnlimited ? 3 : 4; 

                int startIndex = state.currentPage * state.itemsPerPage;
                int endIndex = Mathf.Min(startIndex + state.itemsPerPage, historyCount);

                for (int i = startIndex; i < endIndex; i++)
                {
                    box2Lines += 1;
                    if (state.expandedHistoryItems.Contains(i))
                    {
                        var cmd = processor.History[i];
                        if (!string.IsNullOrEmpty(cmd.Metadata.Description)) box2Lines++;
                        if (!string.IsNullOrEmpty(processor.HistoryReasons[i])) box2Lines++;
                        
                        var fields = GetCommandFields(cmd.GetType());
                        if (fields.Length == 0) box2Lines += 1; 
                        else box2Lines += 1 + fields.Length; 
                        
                        box2Lines += 0.25f; 
                    }
                }
            }
            float box2Height = (box2Lines * LineHeight) + (BoxPadding * 2);
            totalHeight += box2Height + Padding;

            return totalHeight;
        }

        // =========================================================================
        // MAIN GUI 
        // =========================================================================

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            CommandProcessor processor = GetProcessor(property);
            EditorGUI.BeginProperty(position, label, property);

            Rect rootFoldoutRect = new Rect(position.x, position.y, position.width, LineHeight);
            property.isExpanded = EditorGUI.Foldout(rootFoldoutRect, property.isExpanded, label, true, EditorStyles.foldout);

            if (!property.isExpanded || processor == null)
            {
                EditorGUI.EndProperty();
                return;
            }

            processor.SyncUndoState();

            int originalIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            ExecutionState state = GetState(property.propertyPath, property);
            float currentY = position.y + LineHeight + Padding;
            float innerX = position.x + (originalIndent * 15f);
            float boxWidth = position.width - (originalIndent * 15f);

            // ==========================================
            // BOX 1: TESTING
            // ==========================================
            float box1Height = (LineHeight * 2) + (BoxPadding * 2);
            if (state.isTestingExpanded)
            {
                box1Height += EditorGUI.GetPropertyHeight(state.testingCommandProp, true) + Padding;
                box1Height += EditorGUI.GetPropertyHeight(state.executionReasonProp, true) + Padding;
                box1Height += LineHeight + Padding;
            }

            GUI.Box(new Rect(innerX, currentY, boxWidth, box1Height), GUIContent.none, EditorStyles.helpBox);

            float contentX = innerX + BoxPadding;
            float contentWidth = boxWidth - (BoxPadding * 2);
            float cy = currentY + BoxPadding;

            Rect undoBtnRect = new Rect(contentX, cy, contentWidth / 2f - 2f, LineHeight - 2);
            Rect redoBtnRect = new Rect(contentX + contentWidth / 2f + 2f, cy, contentWidth / 2f - 2f, LineHeight - 2);
            
            if (GUI.Button(undoBtnRect, "Undo")) 
            { 
                Undo.RecordObject(property.serializedObject.targetObject, "Command Processor Undo");
                processor.Undo(); 
                EditorUtility.SetDirty(property.serializedObject.targetObject);
                GUI.changed = true; 
            }
            
            if (GUI.Button(redoBtnRect, "Redo")) 
            { 
                Undo.RecordObject(property.serializedObject.targetObject, "Command Processor Redo");
                processor.Redo(); 
                EditorUtility.SetDirty(property.serializedObject.targetObject);
                GUI.changed = true; 
            }
            cy += LineHeight;

            state.isTestingExpanded = EditorGUI.Foldout(new Rect(contentX + 12f, cy, contentWidth - 12f, LineHeight - 2), state.isTestingExpanded, "Execute Command Manually", true);
            cy += LineHeight;

            if (state.isTestingExpanded)
            {
                state.testerSerializedObj.Update();

                EditorGUI.indentLevel += 1;

                // Draw Native Unity Properties with correct bounds
                float cmdHeight = EditorGUI.GetPropertyHeight(state.testingCommandProp, true);
                EditorGUI.PropertyField(new Rect(contentX + (state.testingCommandProp.boxedValue != null ? 10 : 0), cy, contentWidth - (state.testingCommandProp.boxedValue != null ? 10 : 0), cmdHeight), state.testingCommandProp, new GUIContent("Command"), true);
                cy += cmdHeight + Padding;

                float reasonHeight = EditorGUI.GetPropertyHeight(state.executionReasonProp, true);
                EditorGUI.PropertyField(new Rect(contentX, cy, contentWidth, reasonHeight), state.executionReasonProp, new GUIContent("Usage Description"), true);
                cy += reasonHeight + Padding;

                Color defColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
                if (GUI.Button(new Rect(contentX, cy, contentWidth, LineHeight), "Execute Command"))
                {
                    state.testerSerializedObj.ApplyModifiedProperties();
                    ICommand cmdToExecute = state.testerSO.testingCommand;

                    if (cmdToExecute != null)
                    {
                        // Register undo record on the host component/script
                        Undo.RecordObject(property.serializedObject.targetObject, $"Execute Command: {cmdToExecute.Metadata.Name}");

                        ICommand clone = (ICommand)Activator.CreateInstance(cmdToExecute.GetType());
                        var fields = cmdToExecute.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        foreach (var f in fields) f.SetValue(clone, f.GetValue(cmdToExecute));

                        processor.ExecuteCommand(clone, state.testerSO.executionReason);
                        
                        EditorUtility.SetDirty(property.serializedObject.targetObject);
                        state.testerSO.executionReason = "";
                        state.testerSerializedObj.Update();
                        GUI.changed = true; 
                    }
                    else
                    {
                        Debug.LogWarning("Please select a Command from the dropdown before executing.");
                    }
                }
                GUI.backgroundColor = defColor;
                state.testerSerializedObj.ApplyModifiedProperties();

                EditorGUI.indentLevel -= 1;
            }
            currentY += box1Height + Padding;

            // ==========================================
            // BOX 2: HISTORY
            // ==========================================
            int historyCount = processor.History != null ? processor.History.Count : 0;
            int totalPages = Mathf.Max(1, Mathf.CeilToInt(historyCount / (float)state.itemsPerPage));
            state.currentPage = Mathf.Clamp(state.currentPage, 0, totalPages - 1);
            
            float box2Lines = 1;
            if (state.isHistoryExpanded)
            {
                box2Lines += processor.IsUnlimited ? 3 : 4;
                int startIndex = state.currentPage * state.itemsPerPage;
                int endIndex = Mathf.Min(startIndex + state.itemsPerPage, historyCount);

                for (int i = startIndex; i < endIndex; i++)
                {
                    box2Lines += 1;
                    if (state.expandedHistoryItems.Contains(i))
                    {
                        var cmd = processor.History[i];
                        if (!string.IsNullOrEmpty(cmd.Metadata.Description)) box2Lines++;
                        if (!string.IsNullOrEmpty(processor.HistoryReasons[i])) box2Lines++;
                        var fields = GetCommandFields(cmd.GetType());
                        box2Lines += (fields.Length == 0) ? 1 : 1 + fields.Length;
                        box2Lines += 0.25f;
                    }
                }
            }
            
            float box2Height = (box2Lines * LineHeight) + (BoxPadding * 2);
            GUI.Box(new Rect(innerX, currentY, boxWidth, box2Height), GUIContent.none, EditorStyles.helpBox);
            cy = currentY + BoxPadding;

            Rect historyFoldoutRect = new Rect(contentX + 12f, cy, contentWidth * 0.6f - 12f, LineHeight - 2);
            state.isHistoryExpanded = EditorGUI.Foldout(historyFoldoutRect, state.isHistoryExpanded, $"History Timeline [{historyCount}]", true, GetBoldFoldout());
            
            if (GUI.Button(new Rect(contentX + contentWidth * 0.6f, cy, contentWidth * 0.4f, LineHeight - 2), "Clear History")) 
            { 
                processor.ClearHistory(); GUI.changed = true; 
            }
            cy += LineHeight;

            if (state.isHistoryExpanded)
            {
                EditorGUI.BeginChangeCheck();
                bool newUnlimited = EditorGUI.Toggle(new Rect(contentX, cy, contentWidth, LineHeight - 2), "Unlimited Capacity", processor.IsUnlimited);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(property.serializedObject.targetObject, "Toggle Unlimited Capacity");
                    processor.IsUnlimited = newUnlimited;
                    EditorUtility.SetDirty(property.serializedObject.targetObject);
                    state.expandedHistoryItems.Clear(); 
                    GUI.changed = true;
                }
                cy += LineHeight;

                // Max Capacity Numeric Field
                if (!processor.IsUnlimited)
                {
                    EditorGUI.BeginChangeCheck();
                    int newCap = EditorGUI.IntField(new Rect(contentX, cy, contentWidth, LineHeight - 2), "Max Capacity Limit", processor.MaxHistorySize);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(property.serializedObject.targetObject, "Change Max Capacity");
                        processor.MaxHistorySize = newCap;
                        EditorUtility.SetDirty(property.serializedObject.targetObject);
                        state.expandedHistoryItems.Clear(); 
                        GUI.changed = true;
                    }
                    cy += LineHeight;
                }

                historyCount = processor.History.Count;
                totalPages = Mathf.Max(1, Mathf.CeilToInt(historyCount / (float)state.itemsPerPage));
                state.currentPage = Mathf.Clamp(state.currentPage, 0, totalPages - 1);
                
                int startIndex = state.currentPage * state.itemsPerPage;
                int endIndex = Mathf.Min(startIndex + state.itemsPerPage, historyCount);

                DrawInitialStateItem(processor, property, ref cy, contentX, contentWidth);

                for (int i = startIndex; i < endIndex; i++)
                {
                    DrawHistoryItem(processor, state, property, i, ref cy, contentX, contentWidth);
                }

                DrawPaginationControls(state, totalPages, ref cy, contentX, contentWidth);
            }

            EditorGUI.indentLevel = originalIndent;
            EditorGUI.EndProperty();
        }

        private void DrawPaginationControls(ExecutionState state, int totalPages, ref float cy, float x, float w)
        {
            float btnW = 40f;
            Rect pageRect = new Rect(x, cy, w, LineHeight - 2);

            GUI.enabled = state.currentPage > 0;
            if (GUI.Button(new Rect(pageRect.x, pageRect.y, btnW, pageRect.height), "<")) { state.currentPage--; GUI.changed = true; }
            GUI.enabled = true;
            
            EditorGUI.LabelField(new Rect(pageRect.x + btnW, pageRect.y, pageRect.width - (btnW * 2), pageRect.height), $"Page {state.currentPage + 1} of {totalPages}", EditorStyles.centeredGreyMiniLabel);
            
            GUI.enabled = state.currentPage < totalPages - 1;
            if (GUI.Button(new Rect(pageRect.x + pageRect.width - btnW, pageRect.y, btnW, pageRect.height), ">")) { state.currentPage++; GUI.changed = true; }
            GUI.enabled = true;
            
            cy += LineHeight;
        }

        private void DrawInitialStateItem(CommandProcessor processor, SerializedProperty property, ref float cy, float x, float w)
        {
            bool isActive = (processor.CurrentIndex == -1);
            Color defaultColor = GUI.backgroundColor;
            GUI.backgroundColor = isActive ? new Color(0.4f, 1f, 0.4f) : Color.white;

            Rect bgRect = new Rect(x, cy, w, LineHeight - 2);
            GUI.Box(bgRect, GUIContent.none);
            EditorGUI.LabelField(new Rect(x + 4f, cy, w * 0.65f, LineHeight - 2), "--- Initial State ---");

            GUI.enabled = !isActive;
            if (GUI.Button(new Rect(x + w * 0.65f, cy, w * 0.35f, LineHeight - 2), "Revert Here"))
            {
                Undo.RecordObject(property.serializedObject.targetObject, "Revert to Initial State");
                
                while (processor.CanUndo)
                {
                    processor.Undo();
                }

                EditorUtility.SetDirty(property.serializedObject.targetObject);
                GUI.changed = true;
            }
            GUI.enabled = true;
            GUI.backgroundColor = defaultColor;
            cy += LineHeight;
        }

        private void DrawHistoryItem(CommandProcessor processor, ExecutionState state, SerializedProperty property, int i, ref float cy, float x, float w)
        {
            var cmd = processor.History[i];
            string label = $"[{i}] {(cmd.Metadata.Name != null ? cmd.Metadata.Name : cmd.GetType().Name)}";
            
            bool isActive = (processor.CurrentIndex == i);
            Color defaultColor = GUI.backgroundColor;
            GUI.backgroundColor = isActive ? new Color(0.4f, 1f, 0.4f) : (i > processor.CurrentIndex ? Color.gray : Color.white);

            Rect rowRect = new Rect(x, cy, w, LineHeight - 2);
            GUI.Box(rowRect, GUIContent.none);
            
            Rect foldoutRect = new Rect(x + 12f, cy, w * 0.65f - 12f, LineHeight - 2);
            bool isExpanded = state.expandedHistoryItems.Contains(i);
            bool newExpanded = EditorGUI.Foldout(foldoutRect, isExpanded, label, true);
            
            if (newExpanded != isExpanded)
            {
                if (newExpanded) state.expandedHistoryItems.Add(i);
                else state.expandedHistoryItems.Remove(i);
            }

            GUI.enabled = !isActive;
            if (GUI.Button(new Rect(x + w * 0.65f, cy, w * 0.35f, LineHeight - 2), i < processor.CurrentIndex ? "Revert Here" : "Redo Here"))
            {
                // Register undo state on the target object
                Undo.RecordObject(property.serializedObject.targetObject, i < processor.CurrentIndex ? "Revert History" : "Redo History");
                
                // Step through one by one so Unity's undo system registers the command calls correctly
                while (processor.CurrentIndex > i && processor.CanUndo)
                {
                    processor.Undo();
                }
                while (processor.CurrentIndex < i && processor.CanRedo)
                {
                    processor.Redo();
                }

                EditorUtility.SetDirty(property.serializedObject.targetObject);
                GUI.changed = true;
            }
            GUI.enabled = true;
            GUI.backgroundColor = defaultColor;
            cy += LineHeight;

            if (newExpanded)
            {
                string desc = cmd.Metadata.Description;
                string reason = processor.HistoryReasons[i];
                var fields = GetCommandFields(cmd.GetType());

                float detailLines = 0;
                if (!string.IsNullOrEmpty(desc)) detailLines++;
                if (!string.IsNullOrEmpty(reason)) detailLines++;
                detailLines += (fields.Length == 0) ? 1 : 1 + fields.Length;
                
                Rect detailsBox = new Rect(x + 16f, cy, w - 16f, (detailLines * LineHeight));
                GUI.Box(detailsBox, GUIContent.none, EditorStyles.helpBox);

                float innerX = x + 22f;
                float innerW = w - 24f;
                cy += 2f; 

                GUIStyle richText = GetRichTextLabel();

                if (!string.IsNullOrEmpty(desc))
                {
                    GUI.Label(new Rect(innerX, cy, innerW, LineHeight - 2), $"<b>Desc:</b> {desc}", richText);
                    cy += LineHeight;
                }
                
                if (!string.IsNullOrEmpty(reason))
                {
                    GUI.Label(new Rect(innerX, cy, innerW, LineHeight - 2), $"<b>Usage:</b> {reason}", richText);
                    cy += LineHeight;
                }

                if (fields.Length == 0)
                {
                    GUI.Label(new Rect(innerX, cy, innerW, LineHeight - 2), "<i>[No Parameters]</i>", richText);
                    cy += LineHeight;
                }
                else
                {
                    GUI.Label(new Rect(innerX, cy, innerW, LineHeight - 2), "<b>Parameters:</b>", richText);
                    cy += LineHeight;
                    
                    foreach (var f in fields)
                    {
                        object val = f.GetValue(cmd);
                        
                        // Handle Arrays dynamically in the history readout so it doesn't say "Int32[]"
                        string valStr = "null";
                        if (val != null)
                        {
                            if (val is System.Collections.IEnumerable enumerable && !(val is string))
                            {
                                var elements = new List<string>();
                                foreach (var el in enumerable) elements.Add(el?.ToString() ?? "null");
                                valStr = $"[{string.Join(", ", elements)}]";
                            }
                            else
                            {
                                valStr = val.ToString();
                            }
                        }

                        GUI.Label(new Rect(innerX + 8f, cy, innerW - 8f, LineHeight - 2), $"<color=#777>▪</color> <b>{f.Name}</b>: {valStr}", richText);
                        cy += LineHeight;
                    }
                }
                cy += (LineHeight * 0.25f) - 2f; 
            }
        }
    }
}
#endif