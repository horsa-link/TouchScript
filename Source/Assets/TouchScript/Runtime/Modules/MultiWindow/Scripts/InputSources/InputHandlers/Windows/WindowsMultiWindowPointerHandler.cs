#if UNITY_STANDALONE_WIN

using AOT;
using System;
using System.Collections;
using System.Collections.Generic;
using TouchScript.Debugging.Loggers;
using TouchScript.InputSources.InputHandlers.Interop;
using TouchScript.Pointers;
using TouchScript.Utils.Platform;
using UnityEngine;
using PointerData = TouchScript.InputSources.InputHandlers.Interop.PointerData;
using PointerEvent = TouchScript.InputSources.InputHandlers.Interop.PointerEvent;
using PointerType = TouchScript.InputSources.InputHandlers.Interop.PointerType;

namespace TouchScript.InputSources.InputHandlers
{
    /// <summary>
    /// Most is copied from WindowsPointerHandler, except we try to retrieve a window for a given display.
    /// </summary>
    class WindowsMultiWindowPointerHandler : MultiWindowPointerHandler, IDisposable
    {
        public override int TargetDisplay
        {
            get => targetDisplay;
            set
            {
                if (targetDisplay != value)
                {
                    targetDisplay = value;
                    pointerHandler.SetTargetDisplay(messageCallback, value);
                }
            }
        }

        public WindowProperties WindowProperties
        {
            set { UpdateWindow(value); }
        }

        private readonly IntPtr hWindow;

        private NativeWindowHandler nativeWindowHandler;
        private readonly WindowsMultiWindowNativePointerDelegate pointerCallback;
        
        protected WindowsMultiWindowNativePointerHandler pointerHandler;
        protected readonly WindowsMultiWindowNativeLog messageCallback;
        protected readonly Dictionary<int, TouchPointer> winTouchToInternalId = new(10);
        
        protected WindowsMultiWindowPointerHandler(int targetDisplay, IntPtr hWindow, WindowProperties windowProperties, PointerDelegate addPointer,
            PointerDelegate updatePointer, PointerDelegate pressPointer, PointerDelegate releasePointer,
            PointerDelegate removePointer, PointerDelegate cancelPointer)
            : base(targetDisplay, addPointer, updatePointer, pressPointer, releasePointer, removePointer, cancelPointer)
        {
            this.hWindow = hWindow;
            
            messageCallback = onNativeMessage;
            pointerCallback = onNativePointer;

            pointerHandler = new WindowsMultiWindowNativePointerHandler();
            nativeWindowHandler = new NativeWindowHandler(this.hWindow, windowProperties);
        }

        /// <inheritdoc />
        public override void UpdateWindow() => UpdateWindow(nativeWindowHandler.windowProperties);

        /// <inheritdoc />
        public override bool CancelPointer(Pointer pointer, bool shouldReturn)
        {
            var touch = pointer as TouchPointer;
            if (touch == null) return false;

            var internalTouchId = -1;
            foreach (var t in winTouchToInternalId)
            {
                if (t.Value == touch)
                {
                    internalTouchId = t.Key;
                    break;
                }
            }
            if (internalTouchId > -1)
            {
                cancelPointer(touch);
                winTouchToInternalId.Remove(internalTouchId);
                if (shouldReturn) winTouchToInternalId[internalTouchId] = internalReturnTouchPointer(touch);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Releases resources.
        /// </summary>
        public override void Dispose()
        {
            foreach (var i in winTouchToInternalId) cancelPointer(i.Value);
            winTouchToInternalId.Clear();

#if !UNITY_EDITOR
            nativeWindowHandler.ResetWindowProperties();
            nativeWindowHandler.Dispose();
            nativeWindowHandler = null;
#endif
            pointerHandler.Dispose();
            pointerHandler = null;
        }

        #region Protected methods

        protected void initialize(TOUCH_API api)
        {
            pointerHandler.Initialize(messageCallback, targetDisplay, api, hWindow, pointerCallback);
            UpdateWindow();
        }
        
        protected override void setScaling()
        {
            WindowsUtils.GetNativeMonitorResolution(hWindow, out var width, out var height);
            pointerHandler.SetDisplayParams(messageCallback, width, height, 0, 0, 1, 1);
        }

        #endregion
        
        #region Private functions

        private void UpdateWindow(WindowProperties windowProperties)
        {
#if !UNITY_EDITOR
            if (TouchManager.Instance is MonoBehaviour touchManagerGo)
            {
                touchManagerGo.StartCoroutine(updateWindowCo(windowProperties));
            }
#endif
        }

        private IEnumerator updateWindowCo(WindowProperties windowProperties)
        {
            yield return nativeWindowHandler.ApplyWindowPropertiesAsync(windowProperties);

            setScaling();
        }

        #endregion
        
        #region Pointer callbacks

        // Attribute used for IL2CPP
        [MonoPInvokeCallback(typeof(WindowsMultiWindowNativeLog))]
        private void onNativeMessage(int messageType, string message)
        {
            switch (messageType)
            {
                case 2:
                    UnityConsoleLogger.LogWarning($"[WindowsTouchMultiWindow.dll] {message}");
                    break;
                case 3:
                    UnityConsoleLogger.LogError($"[WindowsTouchMultiWindow.dll] {message}");
                    break;
                default:
                    UnityConsoleLogger.Log($"[WindowsTouchMultiWindow.dll] {message}");
                    break;
            }
        }

        private void onNativePointer(int id, PointerEvent evt, PointerType type, Vector2 position, PointerData data)
        {
            switch (type)
            {
                case PointerType.Mouse:
                    switch (evt)
                    {
                        // Enter and Exit are not used - mouse is always present
                        // TODO: how does it work with 2+ mice?
                        case PointerEvent.Enter:
                            throw new NotImplementedException("This is not supposed to be called o.O");
                        case PointerEvent.Leave:
                            break;
                        case PointerEvent.Down:
                            mousePointer.Buttons = updateButtons(mousePointer.Buttons, data.PointerFlags, data.ChangedButtons);
                            pressPointer(mousePointer);
                            break;
                        case PointerEvent.Up:
                            mousePointer.Buttons = updateButtons(mousePointer.Buttons, data.PointerFlags, data.ChangedButtons);
                            releasePointer(mousePointer);
                            break;
                        case PointerEvent.Update:
                            mousePointer.Position = position;
                            mousePointer.Buttons = updateButtons(mousePointer.Buttons, data.PointerFlags, data.ChangedButtons);
                            updatePointer(mousePointer);
                            break;
                        case PointerEvent.Cancelled:
                            cancelPointer(mousePointer);
                            // can't cancel the mouse pointer, it is always present
                            mousePointer = internalAddMousePointer(mousePointer.Position);
                            break;
                    }
                    break;
                case PointerType.Touch:
                    TouchPointer touchPointer;
                    switch (evt)
                    {
                        case PointerEvent.Enter:
                            break;
                        case PointerEvent.Leave:
                            // Sometimes Windows might not send Up, so have to execute touch release logic here.
                            // Has been working fine on test devices so far.
                            if (winTouchToInternalId.TryGetValue(id, out touchPointer))
                            {
                                winTouchToInternalId.Remove(id);
                                internalRemoveTouchPointer(touchPointer);
                            }
                            break;
                        case PointerEvent.Down:
                            touchPointer = internalAddTouchPointer(position);
                            touchPointer.Rotation = getTouchRotation(ref data);
                            touchPointer.Pressure = getTouchPressure(ref data);
                            winTouchToInternalId.Add(id, touchPointer);
                            break;
                        case PointerEvent.Up:
                            break;
                        case PointerEvent.Update:
                            if (!winTouchToInternalId.TryGetValue(id, out touchPointer)) return;
                            touchPointer.Position = position;
                            touchPointer.Rotation = getTouchRotation(ref data);
                            touchPointer.Pressure = getTouchPressure(ref data);
                            updatePointer(touchPointer);
                            break;
                        case PointerEvent.Cancelled:
                            if (winTouchToInternalId.TryGetValue(id, out touchPointer))
                            {
                                winTouchToInternalId.Remove(id);
                                cancelPointer(touchPointer);
                            }
                            break;
                    }
                    break;
                case PointerType.Pen:
                    switch (evt)
                    {
                        case PointerEvent.Enter:
                            penPointer = internalAddPenPointer(position);
                            penPointer.Pressure = getPenPressure(ref data);
                            penPointer.Rotation = getPenRotation(ref data);
                            break;
                        case PointerEvent.Leave:
                            if (penPointer == null) break;
                            internalRemovePenPointer(penPointer);
                            break;
                        case PointerEvent.Down:
                            if (penPointer == null) break;
                            penPointer.Buttons = updateButtons(penPointer.Buttons, data.PointerFlags, data.ChangedButtons);
                            penPointer.Pressure = getPenPressure(ref data);
                            penPointer.Rotation = getPenRotation(ref data);
                            pressPointer(penPointer);
                            break;
                        case PointerEvent.Up:
                            if (penPointer == null) break;
                            mousePointer.Buttons = updateButtons(penPointer.Buttons, data.PointerFlags, data.ChangedButtons);
                            releasePointer(penPointer);
                            break;
                        case PointerEvent.Update:
                            if (penPointer == null) break;
                            penPointer.Position = position;
                            penPointer.Pressure = getPenPressure(ref data);
                            penPointer.Rotation = getPenRotation(ref data);
                            penPointer.Buttons = updateButtons(penPointer.Buttons, data.PointerFlags, data.ChangedButtons);
                            updatePointer(penPointer);
                            break;
                        case PointerEvent.Cancelled:
                            if (penPointer == null) break;
                            cancelPointer(penPointer);
                            break;
                    }
                    break;
            }
        }
        
        private Pointer.PointerButtonState updateButtons(Pointer.PointerButtonState current, PointerFlags flags, ButtonChangeType change)
        {
            var currentUpDown = ((uint) current) & 0xFFFFFC00;
            var pressed = ((uint) flags >> 4) & 0x1F;
            var newUpDown = 0U;
            if (change != ButtonChangeType.None) newUpDown = 1U << (10 + (int) change);
            var combined = (Pointer.PointerButtonState) (pressed | newUpDown | currentUpDown);
            return combined;
        }

        private float getTouchPressure(ref PointerData data)
        {
            var reliable = (data.Mask & (uint) TouchMask.Pressure) > 0;
            if (reliable) return data.Pressure / 1024f;
            return TouchPointer.DEFAULT_PRESSURE;
        }

        private float getTouchRotation(ref PointerData data)
        {
            var reliable = (data.Mask & (uint) TouchMask.Orientation) > 0;
            if (reliable) return data.Rotation / 180f * Mathf.PI;
            return TouchPointer.DEFAULT_ROTATION;
        }

        private float getPenPressure(ref PointerData data)
        {
            var reliable = (data.Mask & (uint) PenMask.Pressure) > 0;
            if (reliable) return data.Pressure / 1024f;
            return PenPointer.DEFAULT_PRESSURE;
        }

        private float getPenRotation(ref PointerData data)
        {
            var reliable = (data.Mask & (uint) PenMask.Rotation) > 0;
            if (reliable) return data.Rotation / 180f * Mathf.PI;
            return PenPointer.DEFAULT_ROTATION;
        }

        #endregion       
    }
}

#endif