using System;
using System.Collections;
using System.Collections.Generic;
using TouchScript.InputSources.InputHandlers;
using UnityEngine;
using UnityEngine.SceneManagement;
#if !UNITY_EDITOR
using System.Diagnostics;
using System.Text;
using TouchScript.Debugging.Loggers;

# if UNITY_STANDALONE_WIN
using TouchScript.Utils.Platform;
using Debug = UnityEngine.Debug;
# endif
#endif

namespace TouchScript.Core
{
    /// <summary>
    /// Default implementation of <see cref="IMultiWindowManager"/>.
    /// </summary>
    public class MultiWindowManagerInstance : DebuggableMonoBehaviour, IMultiWindowManager
    {
        /// <summary>
        /// Gets the instance of MultiWindowManager singleton.
        /// </summary>
        public static MultiWindowManagerInstance Instance
        {
            get
            {
                if (!instance && !shuttingDown)
                {
                    if (!Application.isPlaying) return null;
                    var objects = FindObjectsByType<MultiWindowManagerInstance>(FindObjectsSortMode.None);
                    if (objects.Length == 0)
                    {
                        var go = new GameObject("MultiWindowManager Instance");
                        instance = go.AddComponent<MultiWindowManagerInstance>();
                    }
                    else if (objects.Length >= 1)
                    {
                        instance = objects[0];
                    }
                }
                return instance;
            }
        }

        public bool ShouldActivateDisplays
        {
            get => shouldActivateDisplays;
            set => shouldActivateDisplays = value;
        }

        public bool ShouldUpdateInputHandlersOnStart
        {
            get => shouldUpdateInputHandlers;
            set => shouldUpdateInputHandlers = value;
        }
        
        private static bool shuttingDown;
        private static MultiWindowManagerInstance instance;

        private bool shouldActivateDisplays = true;
        private bool shouldUpdateInputHandlers = true;
        
        private Dictionary<int, IntPtr> targetDisplayWindowHandles = new();
        private List<IntPtr> unityWindowHandles = new();

#if UNITY_STANDALONE_LINUX
        private X11PointerHandlerSystem pointerHandlerSystem;
#endif

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else if (instance != this)
            {
                Destroy(this);
                return;
            }
            
            gameObject.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(gameObject);
            
            Input.simulateMouseWithTouches = false;

#if UNITY_STANDALONE_LINUX
            // We have to create this after awake
            pointerHandlerSystem = new X11PointerHandlerSystem();
#endif
            
            // First display is always activated
            OnDisplayActivated(0);
            
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StopAllCoroutines();
            StartCoroutine(LateAwake());
        }

        private IEnumerator LateAwake()
        {
            // Wait 2 frames:
            // Frame 0: TouchManager prepares, inputs add themselves and optionally activate the screen
            // Frame 1: Displays are activated, we can retrieve handles and update the input handlers
            yield return null;
            
#if UNITY_STANDALONE_LINUX
            TouchManager.Instance.AddSystem(pointerHandlerSystem);
#endif
            if (ShouldUpdateInputHandlersOnStart)
            {
                UpdateInputHandlers();
            }
        }
        
        private void OnApplicationQuit()
        {
#if UNITY_STANDALONE_LINUX
            TouchManager.Instance.RemoveSystem(pointerHandlerSystem);
#endif
            shuttingDown = true;
        }

        private void OnDestroy()
        {
#if UNITY_STANDALONE_LINUX
            pointerHandlerSystem?.Dispose();
            pointerHandlerSystem = null;
#endif

            if (instance == this)
            {
                instance = null;
            }
        }

        public IntPtr OnDisplayActivated(int targetDisplay)
        {
            if (targetDisplayWindowHandles.TryGetValue(targetDisplay, out var window))
            {
                return window;
            }
            
#if !UNITY_EDITOR
            RefreshWindowHandles();

            // Now we check for every pointer, if it is present in the dictionary
            foreach (var windowHandle in unityWindowHandles)
            {
                if (!targetDisplayWindowHandles.ContainsValue(windowHandle))
                {
                    targetDisplayWindowHandles.Add(targetDisplay, windowHandle);

                    UnityConsoleLogger.Log($"Registered window handle for display {targetDisplay + 1}.");
                    
                    return windowHandle;
                }
            }
#endif
            
            return IntPtr.Zero;
        }
        
#if !UNITY_EDITOR

# if UNITY_STANDALONE_WIN
        private void RefreshWindowHandles()
        {
            const string unityWindowClassName = "UnityWndClass";

            unityWindowHandles.Clear();
            // For every window of the current process, we check if it is of the unity window class, and if so
            // add it to list of unity windows
            var classNameBuilder = new StringBuilder(33);
            var windows = WindowsUtilsEx.GetRootWindowsOfProcess(Process.GetCurrentProcess().Id);
            
            foreach (var window in windows)
            {
                classNameBuilder.Clear();
                WindowsUtilsEx.GetClassName(window, classNameBuilder, 33);

                var className = classNameBuilder.ToString();
                if (className != unityWindowClassName)
                {
                    continue;
                }

                unityWindowHandles.Add(window);
            }

#  if TOUCHSCRIPT_DEBUG            
            UnityConsoleLogger.Log($"Found {unityWindowHandles.Count} windows.");
#  endif
        }

# elif UNITY_STANDALONE_LINUX
        private void RefreshWindowHandles()
        {
#  if TOUCHSCRIPT_DEBUG
            UnityConsoleLogger.Log($"RefreshWindowHandles.");
#  endif

            unityWindowHandles.Clear();
            pointerHandlerSystem.GetWindowsOfProcess(Process.GetCurrentProcess().Id, unityWindowHandles);

#  if TOUCHSCRIPT_DEBUG
            UnityConsoleLogger.Log($"Found {unityWindowHandles.Count} windows.");
#  endif
        }
# endif
#endif
        
        public IntPtr GetWindowHandle(int targetDisplay)
        {
            return targetDisplayWindowHandles.TryGetValue(targetDisplay, out var windowHandle) ? windowHandle : IntPtr.Zero;
        }

        public void UpdateInputHandlers()
        {
            var inputs = TouchManager.Instance.Inputs;
            foreach (var input in inputs)
            {
                if (input is MultiWindowStandardInput multiWindowInput &&
                    multiWindowInput.isActiveAndEnabled)
                {
                    multiWindowInput.UpdateInputHandlers();
                }
            }
        }
    }
}