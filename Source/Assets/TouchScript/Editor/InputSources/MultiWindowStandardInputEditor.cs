using TouchScript.Editor.EditorUI;
using TouchScript.InputSources.InputHandlers;
using TouchScript.InputSources.InputHandlers.Interop;
using UnityEditor;
using UnityEngine;

namespace TouchScript.Editor.InputSources
{
    [CustomEditor(typeof(MultiWindowStandardInput), true)]
    public class MultiWindowStandardInputEditor : InputSourceEditor
    {
        private static readonly int[] targetDisplays = {
            0, 1, 2, 3, 4, 5, 6, 7
        };
        private static readonly GUIContent[] targetDisplayNames = {
            new GUIContent("Display 1"),
            new GUIContent("Display 2"),
            new GUIContent("Display 3"),
            new GUIContent("Display 4"),
            new GUIContent("Display 5"),
            new GUIContent("Display 6"),
            new GUIContent("Display 7"),
            new GUIContent("Display 8"),
        };

        public static readonly GUIContent TEXT_GENERAL_HEADER = new GUIContent("General", "General settings.");

        public static readonly GUIContent TEXT_TARGET_DISPLAY = new GUIContent("Target Display", "The target display for which this component gathers input data.");
        public static readonly GUIContent TEXT_EMULATE_MOUSE = new GUIContent("Emulate Second Mouse Pointer", "If selected, you can press ALT to make a stationary mouse pointer. This is used to simulate multi-touch.");
        public static readonly GUIContent TEXT_WINDOW_PROPERTIES = new GUIContent("Window properties", "Select which Window property enable, 'Ignore' will skip this process");

        public static readonly GUIContent TEXT_HELP = new GUIContent("This component gathers window specific input data from mouse devices, and touch device on the Windows and Linux platforms.");

        private SerializedProperty basicEditor;
        private SerializedProperty targetDisplay, emulateSecondMousePointer, windowProperties;
        private SerializedProperty generalProps, windowsProps;

        private MultiWindowStandardInput instance;

        protected override void OnEnable()
        {
            base.OnEnable();

            instance = target as MultiWindowStandardInput;
            basicEditor = serializedObject.FindProperty("basicEditor");
            targetDisplay = serializedObject.FindProperty("targetDisplay");
            emulateSecondMousePointer = serializedObject.FindProperty("emulateSecondMousePointer");
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
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(emulateSecondMousePointer, TEXT_EMULATE_MOUSE);
            if (EditorGUI.EndChangeCheck())
            {
                instance.EmulateSecondMousePointer = emulateSecondMousePointer.boolValue;
            }

            var r = EditorGUILayout.GetControlRect(true, 16f, EditorStyles.layerMaskField);
            var label = EditorGUI.BeginProperty(r, TEXT_WINDOW_PROPERTIES, windowProperties);
            EditorGUI.BeginChangeCheck();
            r = EditorGUI.PrefixLabel(r, label);
            var sFlags = (WindowProperties)EditorGUI.EnumFlagsField(r, instance.WindowProperties);
            if (EditorGUI.EndChangeCheck())
            {
                instance.WindowProperties = sFlags;
                EditorUtility.SetDirty(instance);
            }
            EditorGUI.EndProperty();
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.IntPopup(targetDisplay, targetDisplayNames, targetDisplays, TEXT_TARGET_DISPLAY);
            if (EditorGUI.EndChangeCheck())
            {
                instance.TargetDisplay = targetDisplay.intValue;
            }
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