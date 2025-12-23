using TouchScript.Editor.EditorUI;
using TouchScript.InputSources.InputHandlers;
using TouchScript.InputSources.InputHandlers.Interop;
using UnityEditor;
using UnityEngine;
using static TouchScript.InputSources.InputHandlers.MultiWindowStandardInput;

namespace TouchScript.Editor.InputSources
{
    [CustomEditor(typeof(MultiWindowStandardInput), true)]
    public class MultiWindowStandardInputEditor : InputSourceEditor
    {
        private enum TargetDisplays
        {
            Display1 = 0,
            Display2 = 1,
            Display3 = 2,
            Display4 = 3,
            Display5 = 4,
            Display6 = 5,
            Display7 = 6,
            Display8 = 7,
        }

        public static readonly GUIContent TEXT_GENERAL_HEADER = new GUIContent("General", "General settings.");

        public static readonly GUIContent TEXT_TARGET_DISPLAY = new GUIContent("Target Display", "The target display for which this component gathers input data.");
        public static readonly GUIContent TEXT_MOUSE = new GUIContent("Mouse", "Select if and how mouse events should be processed.");
        public static readonly GUIContent TEXT_EMULATE_MOUSE = new GUIContent("Emulate Second Mouse Pointer", "If selected, you can press ALT to make a stationary mouse pointer. This is used to simulate multi-touch.");
        public static readonly string TEXT_MOUSE_AS_MOUSE_POINTER_UNSUPPORTED = $"With the new InputSystem the mouse is always processed as a Touch pointer, however changing this property to '{MouseProperties.Enable}' or '{MouseProperties.EnableAndEmulateSecondPointer}' will restore the Mouse input module and its functionalities.";
        public static readonly GUIContent TEXT_WINDOW_PROPERTIES = new GUIContent("Window Properties", "Select which Window property enable, 'Ignore' will skip this process");

        public static readonly GUIContent TEXT_HELP = new GUIContent("This component gathers window specific input data from mouse devices, and touch device on the Windows and Linux platforms.");

        private SerializedProperty basicEditor;
        private SerializedProperty targetDisplay, mouseProperty, windowProperties;
        private SerializedProperty generalProps, windowsProps;

        private MultiWindowStandardInput instance;

        protected override void OnEnable()
        {
            base.OnEnable();

            instance = target as MultiWindowStandardInput;
            basicEditor = serializedObject.FindProperty("basicEditor");
            targetDisplay = serializedObject.FindProperty("targetDisplay");
            mouseProperty = serializedObject.FindProperty("mouseProperty");
            windowProperties = serializedObject.FindProperty("windowProperties");
            generalProps = serializedObject.FindProperty("generalProps");
            windowsProps = serializedObject.FindProperty("windowsProps");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();

            GUILayout.Space(5);

            if (basicEditor.boolValue)
            {
                DoDrawGeneral();

                if (GUIElements.BasicHelpBox(TEXT_HELP))
                {
                    basicEditor.boolValue = false;
                    Repaint();
                }
            }
            else
            {
                DrawGeneral();
            }

            serializedObject.ApplyModifiedProperties();
            base.OnInspectorGUI();
        }

        private void DoDrawGeneral()
        {
            Rect r;
            GUIContent label;

            r = EditorGUILayout.GetControlRect(true, 16f, EditorStyles.popup);
            label = EditorGUI.BeginProperty(r, TEXT_TARGET_DISPLAY, targetDisplay);
            EditorGUI.BeginChangeCheck();
            r = EditorGUI.PrefixLabel(r, label);
            var sFlags1 = (TargetDisplays)EditorGUI.EnumPopup(r, (TargetDisplays)instance.TargetDisplay);
            if (EditorGUI.EndChangeCheck())
            {
                instance.TargetDisplay = (int)sFlags1;
                EditorUtility.SetDirty(instance);
            }
            EditorGUI.EndProperty();

            EditorGUILayout.Space(1);

            r = EditorGUILayout.GetControlRect(true, 16f, EditorStyles.layerMaskField);
            label = EditorGUI.BeginProperty(r, TEXT_WINDOW_PROPERTIES, windowProperties);
            EditorGUI.BeginChangeCheck();
            r = EditorGUI.PrefixLabel(r, label);
            var sFlags = (WindowProperties)EditorGUI.EnumFlagsField(r, instance.WindowProperties);
            if (EditorGUI.EndChangeCheck())
            {
                instance.WindowProperties = sFlags;
                EditorUtility.SetDirty(instance);
            }
            EditorGUI.EndProperty();

            EditorGUILayout.Space(1);

            EditorGUI.BeginChangeCheck();
            r = EditorGUILayout.GetControlRect(true, 16f, EditorStyles.popup);
            r = EditorGUI.PrefixLabel(r, TEXT_MOUSE);
            var sPopup = (int)((MouseProperties)EditorGUI.EnumPopup(r, (MouseProperties)mouseProperty.enumValueIndex));
            if (EditorGUI.EndChangeCheck())
            {
                mouseProperty.enumValueIndex = sPopup;
                EditorUtility.SetDirty(instance);
            }
#if ENABLE_INPUT_SYSTEM
            EditorGUILayout.Space(1);

            EditorGUILayout.HelpBox(TEXT_MOUSE_AS_MOUSE_POINTER_UNSUPPORTED, MessageType.Warning);
#endif
        }

        private void DrawGeneral()
        {
            var display = GUIElements.Header(TEXT_GENERAL_HEADER, generalProps);
            if (display)
            {
                EditorGUI.indentLevel++;
                DoDrawGeneral();
                EditorGUI.indentLevel--;
            }
        }
    }
}