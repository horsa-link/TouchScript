/*
 * @author Valentin Simonov / http://va.lent.in/
 */

using System;
using System.Collections;
using System.Collections.Generic;
using TouchScript.Debugging;
using TouchScript.Devices.Display;
using TouchScript.InputSources;
using TouchScript.InputSources.InputHandlers;
using TouchScript.Layers;
using TouchScript.Pointers;
using TouchScript.Utils;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
#if TOUCHSCRIPT_DEBUG
using TouchScript.Debugging.GL;
using TouchScript.Debugging.Loggers;
#endif

namespace TouchScript.Core
{
    /// <summary>
    /// Default implementation of <see cref="ITouchManager"/>.
    /// </summary>
    public sealed class TouchManagerInstance : DebuggableMonoBehaviour, ITouchManager
    {
        /// <summary>
        /// Predicate of Pointers to remove from touch recognition
        /// </summary>
        public Predicate<Pointer> AreaToRemove = null;
        
        /// <inheritdoc />
        public event EventHandler FrameStarted
        {
            add => frameStartedInvoker += value;
            remove => frameStartedInvoker -= value;
        }

        /// <inheritdoc />
        public event EventHandler FrameFinished
        {
            add => frameFinishedInvoker += value;
            remove => frameFinishedInvoker -= value;
        }

        /// <inheritdoc />
        public event EventHandler<PointerEventArgs> PointersAdded
        {
            add => pointersAddedInvoker += value;
            remove => pointersAddedInvoker -= value;
        }

        /// <inheritdoc />
        public event EventHandler<PointerEventArgs> PointersUpdated
        {
            add => pointersUpdatedInvoker += value;
            remove => pointersUpdatedInvoker -= value;
        }

        /// <inheritdoc />
        public event EventHandler<PointerEventArgs> PointersPressed
        {
            add => pointersPressedInvoker += value;
            remove => pointersPressedInvoker -= value;
        }

        /// <inheritdoc />
        public event EventHandler<PointerEventArgs> PointersReleased
        {
            add => pointersReleasedInvoker += value;
            remove => pointersReleasedInvoker -= value;
        }

        /// <inheritdoc />
        public event EventHandler<PointerEventArgs> PointersRemoved
        {
            add => pointersRemovedInvoker += value;
            remove => pointersRemovedInvoker -= value;
        }

        /// <inheritdoc />
        public event EventHandler<PointerEventArgs> PointersCancelled
        {
            add => pointersCancelledInvoker += value;
            remove => pointersCancelledInvoker -= value;
        }

        // Needed to overcome iOS AOT limitations
        private EventHandler<PointerEventArgs> pointersAddedInvoker, pointersUpdatedInvoker, pointersPressedInvoker, pointersReleasedInvoker, pointersRemovedInvoker, pointersCancelledInvoker;

        private EventHandler frameStartedInvoker, frameFinishedInvoker;

        /// <summary>
        /// Gets the instance of TouchManager singleton.
        /// </summary>
        public static TouchManagerInstance Instance
        {
            get
            {
                if (!instance && !shuttingDown)
                {
                    if (!Application.isPlaying) return null;
                    var objects = FindObjectsByType<TouchManagerInstance>(FindObjectsSortMode.None);

                    if (objects.Length == 0)
                    {
                        var go = new GameObject("TouchManager Instance");
                        instance = go.AddComponent<TouchManagerInstance>();
                    }
                    else if (objects.Length >= 1)
                    {
                        instance = objects[0];
                    }
                }

                return instance;
            }
        }

        /// <inheritdoc />
        public IDisplayDevice DisplayDevice
        {
            get
            {
                if (displayDevice == null)
                {
                    displayDevice = ScriptableObject.CreateInstance<GenericDisplayDevice>();
                }

                return displayDevice;
            }
            set
            {
                displayDevice = value ?? ScriptableObject.CreateInstance<GenericDisplayDevice>();

                UpdateResolution();
            }
        }

        /// <inheritdoc />
        public float DPI { get; private set; } = 96;

        /// <inheritdoc />
        public bool ShouldCreateCameraLayer { get; set; } = true;

        /// <summary>
        /// Enables or disables Windows 11 gestures (e.g. edge gestures, tap feedback, right click etc)
        /// </summary>
        public bool WindowsGesturesManagement
        {
            set
            {
                for (var i = 0; i < inputCount; i++)
                {
                    if (inputs[i] is MultiWindowStandardInput standardInput)
                    {
                        standardInput.WindowsGesturesManagement = value;
                    }
                }
            }
        }

        /// <inheritdoc />
        public bool ShouldCreateStandardInput { get; set; } = true;

        /// <inheritdoc />
        public IList<IInputSource> Inputs => new List<IInputSource>(inputs);

        /// <inheritdoc />
        public float DotsPerCentimeter { get; private set; } = TouchManager.CM_TO_INCH * 96;

        /// <inheritdoc />
        public int PointersCount => pointers.Count;

        /// <inheritdoc />
        public IList<Pointer> Pointers => new List<Pointer>(pointers);

        /// <inheritdoc />
        public int PressedPointersCount => pressedPointers.Count;

        /// <inheritdoc />
        public IList<Pointer> PressedPointers => new List<Pointer>(pressedPointers);

        /// <inheritdoc />
        public bool IsInsidePointerFrame { get; private set; }

        private static bool shuttingDown;
        private static TouchManagerInstance instance;

        private IDisplayDevice displayDevice;

        private ILayerManager layerManager;

        private List<IInputSourceSystem> systems = new();
        private int systemCount;

        private List<IInputSource> inputs = new(3);
        private int inputCount;

        private List<Pointer> pointers = new(30);
        private HashSet<Pointer> pressedPointers = new();
        private Dictionary<int, Pointer> idToPointer = new(30);

        // Upcoming changes
        private List<Pointer> pointersAdded = new(10);
        private HashSet<int> pointersUpdated = new();
        private HashSet<int> pointersPressed = new();
        private HashSet<int> pointersReleased = new();
        private HashSet<int> pointersRemoved = new();
        private HashSet<int> pointersCancelled = new();

        private static ObjectPool<List<Pointer>> pointerListPool = new(2, () => new List<Pointer>(10), null, l => l.Clear());

        private static ObjectPool<List<int>> intListPool = new(3, () => new List<int>(10), null, l => l.Clear());

        private int nextPointerId;
        private object pointerLock = new();

		// Cache delegates
		private Func<TouchLayer, bool> _layerAddPointer, _layerUpdatePointer, _layerRemovePointer, _layerCancelPointer;

        // Used in layer dispatch functions
        private Pointer tmpPointer;

#if TOUCHSCRIPT_DEBUG
        private IPointerLogger pLogger;
#endif

		private CustomSampler samplerUpdateInputs, samplerUpdateAdded, samplerUpdatePressed, samplerUpdateUpdated, samplerUpdateReleased, samplerUpdateRemoved, samplerUpdateCancelled;

        public bool AddSystem(IInputSourceSystem system)
        {
            if (system == null) return false;
            if (systems.Contains(system)) return true;
            systems.Add(system);
            systemCount++;
            return true;
        }

        public bool RemoveSystem(IInputSourceSystem system)
        {
            if (system == null) return false;
            var result = systems.Remove(system);
            if (result) systemCount--;
            return result;
        }

        /// <inheritdoc />
        public bool AddInput(IInputSource input)
        {
            if (input == null) return false;
            if (inputs.Contains(input)) return true;
            inputs.Add(input);
            inputCount++;

            return true;
        }

        /// <inheritdoc />
        public bool RemoveInput(IInputSource input)
        {
            if (input == null) return false;
            var result = inputs.Remove(input);
            if (result) inputCount--;

            return result;
        }

        /// <inheritdoc />
        public void CancelPointer(int id, bool shouldReturn)
        {
            if (idToPointer.TryGetValue(id, out var pointer))
            {
                pointer.InputSource.CancelPointer(pointer, shouldReturn);
            }
        }

        /// <inheritdoc />
        public void CancelPointer(int id)
        {
            CancelPointer(id, false);
        }

        /// <inheritdoc />
        public void UpdateResolution()
        {
            if (DisplayDevice != null)
            {
                DisplayDevice.UpdateDPI();
                DPI = DisplayDevice.DPI;
            }
            else
            {
                DPI = 96;
            }
            DotsPerCentimeter = TouchManager.CM_TO_INCH * DPI;
#if TOUCHSCRIPT_DEBUG
            debugPointerSize = Vector2.one * DotsPerCentimeter;
#endif
            
            foreach (var input in inputs) input.UpdateResolution();
        }

        public void UpdateWindowsInput(IntPtr[] hwnds)
        {
            for (var i = 0; i < inputCount; i++)
            {
                if (inputs[i] is MultiWindowStandardInput {WindowsGesturesManagement: true})
                {
                    // if there is a new Window we reset all of them
                    inputs[i].UpdateWindowsInput(DisplayDevices.Instance.WindowHandles);
                }
            }
        }

        internal void INTERNAL_AddPointer(Pointer pointer)
        {
            lock (pointerLock)
            {
                pointer.INTERNAL_Init(nextPointerId);
                pointersAdded.Add(pointer);

#if TOUCHSCRIPT_DEBUG
                pLogger.Log(pointer, PointerEvent.IdAllocated);
#endif

                nextPointerId++;
            }
        }

        internal void INTERNAL_UpdatePointer(int id)
        {
            lock (pointerLock)
            {
                Pointer pointer;
                if (!idToPointer.TryGetValue(id, out pointer))
                {
                    // This pointer was added this frame
					if (!wasPointerAddedThisFrame(id, out pointer))
                    {
						// No pointer with such id
#if TOUCHSCRIPT_DEBUG
                        if (DebugMode) UnityConsoleLogger.LogWarning($"Pointer with id [{id}] is requested to MOVE to but no pointer with such id found.");
#endif
                        return;
                    }
                }

                pointersUpdated.Add(id);
            }
        }

        internal void INTERNAL_PressPointer(int id)
        {
            lock (pointerLock)
            {
                Pointer pointer;
                if (!idToPointer.TryGetValue(id, out pointer))
                {
                    // This pointer was added this frame
					if (!wasPointerAddedThisFrame(id, out pointer))
					{
						// No pointer with such id
#if TOUCHSCRIPT_DEBUG
                        if (DebugMode)
                            UnityConsoleLogger.LogWarning($"Pointer with id [{id}] is requested to PRESS but no pointer with such id found.");
#endif
                        return;
                    }
                }
#if TOUCHSCRIPT_DEBUG
                if (!pointersPressed.Add(id))
                    if (DebugMode)
                        UnityConsoleLogger.LogWarning($"Pointer with id [{id}] is requested to PRESS more than once this frame.");
#else
                pointersPressed.Add(id);
#endif

            }
        }

        /// <inheritdoc />
        internal void INTERNAL_ReleasePointer(int id)
        {
            lock (pointerLock)
            {
                Pointer pointer;
                if (!idToPointer.TryGetValue(id, out pointer))
                {
					// This pointer was added this frame
					if (!wasPointerAddedThisFrame(id, out pointer))
					{
						// No pointer with such id
#if TOUCHSCRIPT_DEBUG
                        if (DebugMode)
                            UnityConsoleLogger.LogWarning($"Pointer with id [{id}] is requested to END but no pointer with such id found.");
#endif
                        return;
                    }
                }
#if TOUCHSCRIPT_DEBUG
                if (!pointersReleased.Add(id))
                    if (DebugMode)
                        UnityConsoleLogger.LogWarning($"Pointer with id [{id}] is requested to END more than once this frame.");
#else
                pointersReleased.Add(id);
#endif

            }
        }

        /// <inheritdoc />
        internal void INTERNAL_RemovePointer(int id)
        {
            lock (pointerLock)
            {
                if (!idToPointer.TryGetValue(id, out var pointer))
                {
					// This pointer was added this frame
					if (!wasPointerAddedThisFrame(id, out pointer))
					{
						// No pointer with such id
#if TOUCHSCRIPT_DEBUG
                        if (DebugMode)
                            UnityConsoleLogger.LogWarning($"Pointer with id [{id}] is requested to REMOVE but no pointer with such id found.");
#endif
                        return;
                    }
                }
#if TOUCHSCRIPT_DEBUG
                if (!pointersRemoved.Add(pointer.Id))
                    if (DebugMode)
                        UnityConsoleLogger.LogWarning($"Pointer with id [{id}] is requested to REMOVE more than once this frame.");
#else
                pointersRemoved.Add(pointer.Id);
#endif

            }
        }

        /// <inheritdoc />
        internal void INTERNAL_CancelPointer(int id)
        {
            lock (pointerLock)
            {
                if (!idToPointer.TryGetValue(id, out var pointer))
                {
					// This pointer was added this frame
					if (!wasPointerAddedThisFrame(id, out pointer))
					{
						// No pointer with such id
#if TOUCHSCRIPT_DEBUG
                        if (DebugMode)
                            UnityConsoleLogger.LogWarning($"Pointer with id [{id}] is requested to CANCEL but no pointer with such id found.");
#endif
                        return;
                    }
                }
#if TOUCHSCRIPT_DEBUG
                if (!pointersCancelled.Add(pointer.Id))
                    if (DebugMode)
                        UnityConsoleLogger.LogWarning($"Pointer with id [{id}]] is requested to CANCEL more than once this frame.");
#else
                pointersCancelled.Add(pointer.Id);
#endif

            }
        }

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

#if TOUCHSCRIPT_DEBUG
            pLogger = TouchScriptDebugger.Instance.PointerLogger;
#endif

            SceneManager.sceneLoaded += sceneLoadedHandler;
            gameObject.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(gameObject);

            layerManager = LayerManager.Instance;

            DisplayDevices.Instance.OnWindowsActivated += UpdateWindowsInput;
            DisplayDevices.Instance.Init();

            UpdateResolution();

            StopAllCoroutines();
            StartCoroutine(lateAwake());

            pointerListPool.WarmUp(2);
            intListPool.WarmUp(3);

			_layerAddPointer = layerAddPointer;
			_layerUpdatePointer = layerUpdatePointer;
			_layerRemovePointer = layerRemovePointer;
			_layerCancelPointer = layerCancelPointer;

            samplerUpdateInputs = CustomSampler.Create("[TouchScript] Update Inputs");
			samplerUpdateAdded = CustomSampler.Create("[TouchScript] Added Pointers");
			samplerUpdatePressed = CustomSampler.Create("[TouchScript] Press Pointers");
			samplerUpdateUpdated = CustomSampler.Create("[TouchScript] Update Pointers");
			samplerUpdateReleased = CustomSampler.Create("[TouchScript] Release Pointers");
			samplerUpdateRemoved = CustomSampler.Create("[TouchScript] Remove Pointers");
			samplerUpdateCancelled = CustomSampler.Create("[TouchScript] Cancel Pointers");
        }

        private void sceneLoadedHandler(Scene scene, LoadSceneMode mode)
        {
            StopAllCoroutines();
            StartCoroutine(lateAwake());
        }

        private IEnumerator lateAwake()
        {
            // Wait 2 frames:
            // Frame 0: TouchManager adds layers in order
            // Frame 1: Layers add themselves
            // Frame 2: We add a layer if there are none
            yield return null;
            yield return null;

            createCameraLayer();
            createInput();

            UpdateWindowsInput(null);
        }

        private void Update()
        {
            sendFrameStartedToPointers();
            updateInputs();
            updatePointers();
            updateWindowsInput();
        }

        private void OnApplicationQuit()
        {
            shuttingDown = true;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void createCameraLayer()
        {
            if (layerManager.LayerCount == 0 && ShouldCreateCameraLayer)
            {
                if (Camera.main)
                {
                    if (Application.isEditor) UnityConsoleLogger.Log("No touch layers found, adding StandardLayer for the main camera. (this message is harmless)");
                    var layer = Camera.main.gameObject.AddComponent<StandardLayer>();
                    layerManager.AddLayer(layer);
                }
            }
        }

        private void createInput()
        {
            if (inputCount == 0 && ShouldCreateStandardInput)
            {
                if (Application.isEditor) UnityConsoleLogger.Log("No input source found, adding StandardInput. (this message is harmless)");
                GameObject obj = null;
                var objects = FindObjectsByType<TouchManager>(FindObjectsSortMode.None);

                if (objects.Length == 0)
                {
                    obj = GameObject.Find("TouchScript");
                    if (obj == null) obj = new GameObject("TouchScript");
                }
                else
                {
                    obj = objects[0].gameObject;
                }

                obj.AddComponent<StandardInput>();
            }
        }

        private void updateInputs()
        {
            samplerUpdateInputs.Begin();
            for (var i = 0; i < systemCount; i++) systems[i].PrepareInputs();
            for (var i = 0; i < inputCount; i++) inputs[i].UpdateInput();
            samplerUpdateInputs.End();
        }

        private void updateWindowsInput()
        {
            DisplayDevices.Instance.manualUpdate();
        }

        private void updateAdded(List<Pointer> pointers)
        {
			samplerUpdateAdded.Begin();

            var addedCount = pointers.Count;
            var list = pointerListPool.Get();

            for (var i = 0; i < addedCount; i++)
            {
                var pointer = pointers[i];
                list.Add(pointer);
                this.pointers.Add(pointer);
                idToPointer.Add(pointer.Id, pointer);

#if TOUCHSCRIPT_DEBUG
                pLogger.Log(pointer, PointerEvent.Added);
#endif

                tmpPointer = pointer;
                layerManager.ForEach(_layerAddPointer);
                tmpPointer = null;

#if TOUCHSCRIPT_DEBUG
                if (DebugMode) addDebugFigureForPointer(pointer);
#endif
            }

            pointersAddedInvoker?.InvokeHandleExceptions(this, PointerEventArgs.GetCachedEventArgs(list));
            pointerListPool.Release(list);

			samplerUpdateAdded.End();
        }

        private bool layerAddPointer(TouchLayer layer)
        {
            layer.INTERNAL_AddPointer(tmpPointer);

            return true;
        }

        private void updateUpdated(List<int> pointers)
        {
			samplerUpdateUpdated.Begin();
            var updatedCount = pointers.Count;
            var list = pointerListPool.Get();

            for (var i = 0; i < updatedCount; i++)
            {
                var id = pointers[i];
                if (!idToPointer.TryGetValue(id, out var pointer))
                {
#if TOUCHSCRIPT_DEBUG
                    if (DebugMode)
                        UnityConsoleLogger.LogWarning($"Id [{id}] was in UPDATED list but no pointer with such id found.");
#endif
                    continue;
                }
                list.Add(pointer);

#if TOUCHSCRIPT_DEBUG
                pLogger.Log(pointer, PointerEvent.Updated);
#endif

                var layer = pointer.GetPressData().Layer;
                if (layer)
                {
                    layer.INTERNAL_UpdatePointer(pointer);
                }
                else
                {
                    tmpPointer = pointer;
					layerManager.ForEach(_layerUpdatePointer);
                    tmpPointer = null;
                }

#if TOUCHSCRIPT_DEBUG
                if (DebugMode) addDebugFigureForPointer(pointer);
#endif
            }

            pointersUpdatedInvoker?.InvokeHandleExceptions(this, PointerEventArgs.GetCachedEventArgs(list));
            pointerListPool.Release(list);

			samplerUpdateUpdated.End();
        }

        private bool layerUpdatePointer(TouchLayer layer)
        {
            layer.INTERNAL_UpdatePointer(tmpPointer);

            return true;
        }

        private void updatePressed(List<int> pointers)
        {
			samplerUpdatePressed.Begin();

            var pressedCount = pointers.Count;
            var list = pointerListPool.Get();
            for (var i = 0; i < pressedCount; i++)
            {
                var id = pointers[i];
                if (!idToPointer.TryGetValue(id, out var pointer))
                {
#if TOUCHSCRIPT_DEBUG
                    if (DebugMode)
                        UnityConsoleLogger.LogWarning($"Id [{id}] was in PRESSED list but no pointer with such id found.");
#endif
                    continue;
                }
                list.Add(pointer);
                pressedPointers.Add(pointer);

                var hit = pointer.GetOverData();
                
                if (hit.Layer)
                {
                    pointer.INTERNAL_SetPressData(hit);
                    hit.Layer.INTERNAL_PressPointer(pointer);
                }

#if TOUCHSCRIPT_DEBUG
                pLogger.Log(pointer, PointerEvent.Pressed);
                if (DebugMode) addDebugFigureForPointer(pointer);
#endif
            }

            pointersPressedInvoker?.InvokeHandleExceptions(this, PointerEventArgs.GetCachedEventArgs(list));
            pointerListPool.Release(list);

			samplerUpdatePressed.End();
        }

        private void updateReleased(List<int> pointers)
        {
			samplerUpdateReleased.Begin();

            var releasedCount = pointers.Count;
            var list = pointerListPool.Get();
            for (var i = 0; i < releasedCount; i++)
            {
                var id = pointers[i];
                if (!idToPointer.TryGetValue(id, out var pointer))
                {
#if TOUCHSCRIPT_DEBUG
                    if (DebugMode) UnityConsoleLogger.LogWarning($"Id [{id}] was in RELEASED list but no pointer with such id found.");
#endif
                    continue;
                }

                list.Add(pointer);
                pressedPointers.Remove(pointer);

#if TOUCHSCRIPT_DEBUG
                pLogger.Log(pointer, PointerEvent.Released);
#endif

                var layer = pointer.GetPressData().Layer;
                if (layer != null) layer.INTERNAL_ReleasePointer(pointer);

#if TOUCHSCRIPT_DEBUG
                if (DebugMode) addDebugFigureForPointer(pointer);
#endif
            }

            pointersReleasedInvoker?.InvokeHandleExceptions(this, PointerEventArgs.GetCachedEventArgs(list));

            releasedCount = list.Count;

            for (var i = 0; i < releasedCount; i++)
            {
                var pointer = list[i];
                pointer.INTERNAL_ClearPressData();
            }

            pointerListPool.Release(list);
			samplerUpdateReleased.End();
        }

        private void updateRemoved(List<int> pointers)
        {
			samplerUpdateRemoved.Begin();

            var removedCount = pointers.Count;
            var list = pointerListPool.Get();

            for (var i = 0; i < removedCount; i++)
            {
                var id = pointers[i];
                if (!idToPointer.TryGetValue(id, out var pointer))
                {
#if TOUCHSCRIPT_DEBUG
                    if (DebugMode) UnityConsoleLogger.LogWarning($"Id [{id}] was in REMOVED list but no pointer with such id found.");
#endif
                    continue;
                }

                idToPointer.Remove(id);
                this.pointers.Remove(pointer);
                pressedPointers.Remove(pointer);
                list.Add(pointer);

#if TOUCHSCRIPT_DEBUG
                pLogger.Log(pointer, PointerEvent.Removed);
#endif

                tmpPointer = pointer;
                layerManager.ForEach(_layerRemovePointer);
                tmpPointer = null;

#if TOUCHSCRIPT_DEBUG
                if (DebugMode) removeDebugFigureForPointer(pointer);
#endif
            }

            pointersRemovedInvoker?.InvokeHandleExceptions(this, PointerEventArgs.GetCachedEventArgs(list));

            removedCount = list.Count;

            for (var i = 0; i < removedCount; i++)
            {
                var pointer = list[i];
                pointer.InputSource.INTERNAL_DiscardPointer(pointer);
            }

            pointerListPool.Release(list);

			samplerUpdateRemoved.End();
        }

        private bool layerRemovePointer(TouchLayer layer)
        {
            layer.INTERNAL_RemovePointer(tmpPointer);

            return true;
        }

        private void updateCancelled(List<int> pointers)
        {
			samplerUpdateCancelled.Begin();

            var cancelledCount = pointers.Count;
            var list = pointerListPool.Get();
            for (var i = 0; i < cancelledCount; i++)
            {
                var id = pointers[i];
                if (!idToPointer.TryGetValue(id, out var pointer))
                {
#if TOUCHSCRIPT_DEBUG
                    if (DebugMode)
                        UnityConsoleLogger.LogWarning($"Id [{id}] was in CANCELLED list but no pointer with such id found.");
#endif
                    continue;
                }
                idToPointer.Remove(id);
                this.pointers.Remove(pointer);
                pressedPointers.Remove(pointer);
                list.Add(pointer);

#if TOUCHSCRIPT_DEBUG
                pLogger.Log(pointer, PointerEvent.Cancelled);
#endif

                tmpPointer = pointer;
                layerManager.ForEach(_layerCancelPointer);
                tmpPointer = null;

#if TOUCHSCRIPT_DEBUG
                if (DebugMode) removeDebugFigureForPointer(pointer);
#endif
            }

            pointersCancelledInvoker?.InvokeHandleExceptions(this, PointerEventArgs.GetCachedEventArgs(list));

            for (var i = 0; i < cancelledCount; i++)
            {
                var pointer = list[i];
                pointer.InputSource.INTERNAL_DiscardPointer(pointer);
            }

            pointerListPool.Release(list);
			samplerUpdateCancelled.End();
        }

        private bool layerCancelPointer(TouchLayer layer)
        {
            layer.INTERNAL_CancelPointer(tmpPointer);

            return true;
        }

        private void sendFrameStartedToPointers()
        {
            var count = pointers.Count;

            for (var i = 0; i < count; i++)
            {
                pointers[i].INTERNAL_FrameStarted();
            }
        }

        private void updatePointers()
        {
            IsInsidePointerFrame = true;
            frameStartedInvoker?.InvokeHandleExceptions(this, EventArgs.Empty);

            // need to copy buffers since they might get updated during execution
            List<Pointer> addedList = null;
            List<int> updatedList = null;
            List<int> pressedList = null;
            List<int> releasedList = null;
            List<int> removedList = null;
            List<int> cancelledList = null;

            lock (pointerLock)
            {
                if (pointersAdded.Count > 0)
                {
                    addedList = pointerListPool.Get();
                    addedList.AddRange(pointersAdded);
                    pointersAdded.Clear();
                }

                if (pointersUpdated.Count > 0)
                {
                    updatedList = intListPool.Get();
                    updatedList.AddRange(pointersUpdated);
                    pointersUpdated.Clear();
                }

                if (pointersPressed.Count > 0)
                {
                    pressedList = intListPool.Get();
                    pressedList.AddRange(pointersPressed);
                    pointersPressed.Clear();
                }

                if (pointersReleased.Count > 0)
                {
                    releasedList = intListPool.Get();
                    releasedList.AddRange(pointersReleased);
                    pointersReleased.Clear();
                }

                if (pointersRemoved.Count > 0)
                {
                    removedList = intListPool.Get();
                    removedList.AddRange(pointersRemoved);
                    pointersRemoved.Clear();
                }

                if (pointersCancelled.Count > 0)
                {
                    cancelledList = intListPool.Get();
                    cancelledList.AddRange(pointersCancelled);
                    pointersCancelled.Clear();
                }
            }

            var count = pointers.Count;

            for (var i = 0; i < count; i++)
            {
                pointers[i].INTERNAL_UpdatePosition();
            }

            if (AreaToRemove != null && addedList?.Count > 0)
            {
                addedList.RemoveAll(AreaToRemove);
            }

            if (addedList != null)
            {
                updateAdded(addedList);
                pointerListPool.Release(addedList);
            }

            if (updatedList != null)
            {
                updateUpdated(updatedList);
                intListPool.Release(updatedList);
            }

            if (pressedList != null)
            {
                updatePressed(pressedList);
                intListPool.Release(pressedList);
            }

            if (releasedList != null)
            {
                updateReleased(releasedList);
                intListPool.Release(releasedList);
            }

            if (removedList != null)
            {
                updateRemoved(removedList);
                intListPool.Release(removedList);
            }

            if (cancelledList != null)
            {
                updateCancelled(cancelledList);
                intListPool.Release(cancelledList);
            }

            frameFinishedInvoker?.InvokeHandleExceptions(this, EventArgs.Empty);
            IsInsidePointerFrame = false;
        }

		private bool wasPointerAddedThisFrame(int id, out Pointer pointer)
		{
			pointer = null;

			foreach (var p in pointersAdded)
			{
				if (p.Id == id)
				{
					pointer = p;

					return true;
				}
			}

			return false;
		}

#if TOUCHSCRIPT_DEBUG
        private Vector2 debugPointerSize;

        private void removeDebugFigureForPointer(Pointer pointer)
        {
            GLDebug.RemoveFigure(TouchManager.DEBUG_GL_TOUCH + pointer.Id);
        }

        private void addDebugFigureForPointer(Pointer pointer)
        {
            GLDebug.DrawSquareScreenSpace(TouchManager.DEBUG_GL_TOUCH + pointer.Id, pointer.Position, 0, debugPointerSize,
                GLDebug.MULTIPLY, float.PositiveInfinity);
        }
#endif
    }
}