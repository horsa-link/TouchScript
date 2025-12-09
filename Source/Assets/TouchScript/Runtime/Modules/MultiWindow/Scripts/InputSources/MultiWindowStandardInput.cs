#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR
using System;
#endif
using System;
using TouchScript.Core;
using TouchScript.Debugging.Loggers;
using TouchScript.Pointers;
using TouchScript.Utils.Attributes;
using UnityEngine;

namespace TouchScript.InputSources.InputHandlers
{
    /// <summary>
    /// A display specific input handler. Holds a <see cref="MultiWindowMouseHandler"/> and/or a
    /// <see cref="MultiWindowPointerHandler"/>. Unity touch is not supported, for those situations
    /// <see cref="StandardInput"/> is a better fit.
    /// </summary>
    public class MultiWindowStandardInput : InputSource, IMultiWindowInputHandler
    {
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR
        private static readonly Version WIN8_VERSION = new Version(6, 2, 0, 0);
#endif

        public int TargetDisplay
        {
            get => targetDisplay;
            set
            {
                targetDisplay = Mathf.Clamp(value, 0, 7);
                if (mouseHandler != null) mouseHandler.TargetDisplay = value;
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR
                if (pointerHandler != null) pointerHandler.TargetDisplay = value;
#endif
            }
        }
        
        /// <summary>
        /// Use emulated second mouse pointer with ALT or not.
        /// </summary>
        public bool EmulateSecondMousePointer
        {
            get => emulateSecondMousePointer;
            set
            {
                emulateSecondMousePointer = value;
                if (mouseHandler != null) mouseHandler.EmulateSecondMousePointer = value;
            }
        }

        [SerializeField, Min(0)] private int targetDisplay;
        [ToggleLeft, SerializeField] private bool emulateSecondMousePointer = true;
        
#pragma warning disable CS0414

        [SerializeField, HideInInspector] private bool generalProps; // Used in the custom inspector
        [SerializeField, HideInInspector] private bool windowsProps; // Used in the custom inspector
        
#pragma warning restore CS0414
        
        private MultiWindowManagerInstance multiWindowManager;
        private MultiWindowMouseHandler mouseHandler;
#if !UNITY_EDITOR
        private MultiWindowPointerHandler pointerHandler;
#endif
        [SerializeField]
        [ToggleLeft]
        private bool windowsGesturesManagement = true;
        public bool WindowsGesturesManagement
        {
            get { return windowsGesturesManagement; }
            set
            {
                windowsGesturesManagement = value;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                if (pointerHandler != null) pointerHandler.WindowsGesturesManagement = value;
#endif
            }
        }

        /// <inheritdoc />
        protected override void OnEnable()
        {
            base.OnEnable();
            
            multiWindowManager = MultiWindowManagerInstance.Instance;
            if (multiWindowManager.ShouldActivateDisplays)
            {
                // Activate additional display if it is not the main display
                var displays = Display.displays;
                if (targetDisplay > 0 && targetDisplay < displays.Length)
                {
                    var display = displays[targetDisplay];
                    if (!display.active)
                    {
                        // TODO Display activation settings?
                        
                        Display.displays[targetDisplay].Activate();
                        multiWindowManager.OnDisplayActivated(targetDisplay);
                    }
                }
            }

            if (!multiWindowManager.ShouldUpdateInputHandlersOnStart)
            {
                DoEnable();
            }
        }

        /// <inheritdoc />
        protected override void OnDisable()
        {
            DoDisable();
            
            base.OnDisable();
        }
        
        [ContextMenu("Basic Editor")]
        private void SwitchToBasicEditor()
        {
            basicEditor = true;
        }

        public void Activate()
        {
            
        }

        public void UpdateInputHandlers()
        {
            DoDisable();
            DoEnable();
        }
        
        public override bool UpdateInput()
        {
            if (base.UpdateInput()) return true;
            
            var handled = false;
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR
            if (pointerHandler != null)
            {
                handled = pointerHandler.UpdateInput();
            }
#endif
            if (mouseHandler != null)
            {
                if (handled) mouseHandler.CancelMousePointer();
                else handled = mouseHandler.UpdateInput();
            }
            
            return handled;
        }
        
        /// <inheritdoc />
        public override void UpdateResolution()
        {
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR
            pointerHandler?.UpdateResolution();
#endif
            mouseHandler?.UpdateResolution();
        }
        
        /// <inheritdoc />
        public override bool CancelPointer(Pointer pointer, bool shouldReturn)
        {
            base.CancelPointer(pointer, shouldReturn);
            
            var handled = false;
            
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR
            if (pointerHandler != null) handled = pointerHandler.CancelPointer(pointer, shouldReturn);
#endif
            if (mouseHandler != null && !handled) handled = mouseHandler.CancelPointer(pointer, shouldReturn);
            
            return handled;
        }
        
        /// <inheritdoc />
        protected override void updateCoordinatesRemapper(ICoordinatesRemapper remapper)
        {
            base.updateCoordinatesRemapper(remapper);
            
            if (mouseHandler != null) mouseHandler.CoordinatesRemapper = remapper;
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR
            if (pointerHandler != null) pointerHandler.CoordinatesRemapper = remapper;
#endif
        }

        private void DoEnable()
        {
#if UNITY_EDITOR
            EnableMouse();
#else
# if UNITY_STANDALONE_WIN
            if (Environment.OSVersion.Platform == PlatformID.Win32NT &&
                Environment.OSVersion.Version >= WIN8_VERSION)
            {
                // Windows 8+
                EnableTouch();
                EnableMouse();
            }
            else
            {
                // Other windows
                EnableMouse();
            }
# elif UNITY_STANDALONE_LINUX
            EnableTouch();
# else
            EnableMouse();
# endif
#endif
            
            if (CoordinatesRemapper != null) updateCoordinatesRemapper(CoordinatesRemapper);
        }
        
        private void EnableMouse()
        {
            mouseHandler = new MultiWindowMouseHandler(addPointer, updatePointer, pressPointer, releasePointer, removePointer,
                cancelPointer);
            mouseHandler.EmulateSecondMousePointer = emulateSecondMousePointer;
            mouseHandler.TargetDisplay = TargetDisplay;
            
            UnityConsoleLogger.Log($"Initialized Unity mouse input for display {TargetDisplay + 1}.");
        }

#if !UNITY_EDITOR

# if UNITY_STANDALONE_WIN
        private void EnableTouch()
        {
            var window = multiWindowManager.GetWindowHandle(targetDisplay);
            if (window == IntPtr.Zero)
            {
                UnityConsoleLogger.LogError($"Failed to initialize Windows pointer input for display {TargetDisplay + 1}.");
                return;
            }

            var windows8PointerHandler = new Windows8MultiWindowPointerHandler(TargetDisplay, window, addPointer,
                updatePointer, pressPointer, releasePointer, removePointer, cancelPointer);
            windows8PointerHandler.MouseInPointer = false;
            pointerHandler = windows8PointerHandler;

            UnityConsoleLogger.Log($"Initialized Windows pointer input for display {TargetDisplay + 1}.");
        }

# elif UNITY_STANDALONE_LINUX
        private void EnableTouch()
        {
            var window = multiWindowManager.GetWindowHandle(targetDisplay);
            if (window == IntPtr.Zero)
            {
                UnityConsoleLogger.LogError($"Failed to initialize X11 pointer input for display {TargetDisplay + 1}.");
                return;
            }

            var x11PointerHandler = new X11MultiWindowPointerHandler(TargetDisplay, window, addPointer, updatePointer,
                pressPointer, releasePointer, removePointer, cancelPointer);
            pointerHandler = x11PointerHandler;

            UnityConsoleLogger.Log($"Initialized X11 pointer input for display {TargetDisplay + 1}.");
        }
# endif
#endif

        public override void UpdateWindowsInput(IntPtr[] hwnds)
        {
            base.UpdateWindowsInput(hwnds);
            mouseHandler?.UpdateWindowsInput(hwnds);
#if !UNITY_EDITOR
            pointerHandler?.UpdateWindowsInput(hwnds);
#endif
        }

        private void DoDisable()
        {
            DisableMouse();
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR
            DisableTouch();
#endif
        }
        
        private void DisableMouse()
        {
            if (mouseHandler != null)
            {
                mouseHandler.Dispose();
                mouseHandler = null;
                
                UnityConsoleLogger.Log($"Disposed Unity mouse input for display {TargetDisplay + 1}.");
            }
        }

#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR
        private void DisableTouch()
        {
            if (pointerHandler != null)
            {
                pointerHandler.Dispose();
                pointerHandler = null;

                UnityConsoleLogger.Log($"Disposed pointer input for display {TargetDisplay + 1}.");
            }
        }
#endif
    }
}