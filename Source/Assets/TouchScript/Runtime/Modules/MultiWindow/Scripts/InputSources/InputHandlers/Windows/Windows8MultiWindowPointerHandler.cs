#if UNITY_STANDALONE_WIN

using System;
using System.Runtime.InteropServices;
using TouchScript.Debugging.Loggers;
using TouchScript.InputSources.InputHandlers.Interop;
using TouchScript.Pointers;
using TouchScript.Utils;
using TouchScript.Utils.Platform;
using UnityEngine;

namespace TouchScript.InputSources.InputHandlers
{
    sealed class Windows8MultiWindowPointerHandler : WindowsMultiWindowPointerHandler
    {
        /// <summary>
        /// Enable processing mouse events
        /// </summary>
        private bool enableMouse = true;
        /// <summary>
        /// Enable processing mouse events as touch pointers
        /// </summary>
        private bool enableMouseInPointer = true;
        
        public Windows8MultiWindowPointerHandler(int targetDisplay, IntPtr hWindow, WindowProperties windowProperties, PointerDelegate addPointer,
            PointerDelegate updatePointer, PointerDelegate pressPointer, PointerDelegate releasePointer,
            PointerDelegate removePointer, PointerDelegate cancelPointer)
            : base(targetDisplay, hWindow, windowProperties, addPointer, updatePointer, pressPointer, releasePointer, removePointer, cancelPointer)
        {
            mousePool = new ObjectPool<MousePointer>(4, () => new MousePointer(this), null, resetPointer);
            penPool = new ObjectPool<PenPointer>(2, () => new PenPointer(this), null, resetPointer);

            mousePointer = internalAddMousePointer(Vector3.zero);
            
            initialize(TOUCH_API.WIN8);
        }
        
        /// <inheritdoc />
        public override void Dispose()
        {
            if (mousePointer != null)
            {
                cancelPointer(mousePointer);
                mousePointer = null;
            }
            if (penPointer != null)
            {
                cancelPointer(penPointer);
                penPointer = null;
            }

#if !ENABLE_INPUT_SYSTEM
            WindowsUtils.EnableMouseInPointer(false);   // it's a one-shot function, we cannot revert the value
#endif

            base.Dispose();
        }
        
        /// <inheritdoc />
        public override bool UpdateInput()
        {
            base.UpdateInput();
            
            if(enableMouse && !enableMouseInPointer)
            {
                return winTouchToInternalId.Count > 0;
            }
            else
            {
                return true;
            }
        }

        /// <inheritdoc />
        public override bool CancelPointer(Pointer pointer, bool shouldReturn)
        {
            if (pointer.Equals(mousePointer))
            {
                cancelPointer(mousePointer);
                if (shouldReturn) mousePointer = internalReturnMousePointer(mousePointer);
                else mousePointer = internalAddMousePointer(pointer.Position); // can't totally cancel mouse pointer
                return true;
            }
            if (pointer.Equals(penPointer))
            {
                cancelPointer(penPointer);
                if (shouldReturn) penPointer = internalReturnPenPointer(penPointer);
                return true;
            }
            return base.CancelPointer(pointer, shouldReturn);
        }

        /// <inheritdoc />
        public override void INTERNAL_DiscardPointer(Pointer pointer)
        {
            if (pointer is MousePointer) mousePool.Release(pointer as MousePointer);
            else if (pointer is PenPointer) penPool.Release(pointer as PenPointer);
            else base.INTERNAL_DiscardPointer(pointer);
        }

        /// <summary>
        /// Updates the module based on if and how the mouse is processed
        /// </summary>
        /// <param name="enableMouse"></param>
        /// <param name="enableMouseInPointer"></param>
        public void UpdateMouse(bool enableMouse, bool enableMouseInPointer)
        {
            this.enableMouse = enableMouse;
            this.enableMouseInPointer = enableMouseInPointer;

#if !ENABLE_INPUT_SYSTEM
            // We change how the process handles the mouse events only if we're not using the 'New Input System'
            if (!WindowsUtils.EnableMouseInPointer(enableMouseInPointer))
            {
                UnityConsoleLogger.LogWarning(
                    $"Cannot change \"IsMouseInPointer\" value into \"{enableMouseInPointer}\" maybe it was already set, current value: {WindowsUtils.IsMouseInPointerEnabled()}, " +
                    $"GetLastWin32Error: {Marshal.GetLastWin32Error().ToString("X")}");
            }
#endif
            if (pointerHandler != null) pointerHandler.SetMouseParams(messageCallback, enableMouse, enableMouseInPointer);

            if (enableMouse && enableMouseInPointer)
            {
                if (mousePointer == null) mousePointer = internalAddMousePointer(Vector3.zero);
            }
            else
            {
                if (mousePointer != null)
                {
                    if ((mousePointer.Buttons & Pointer.PointerButtonState.AnyButtonPressed) != 0)
                    {
                        mousePointer.Buttons = PointerUtils.UpPressedButtons(mousePointer.Buttons);
                        releasePointer(mousePointer);
                    }
                    removePointer(mousePointer);
                }
            }
        }
    }
}

#endif