#if UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;

namespace TouchScript.InputSources.InputHandlers.Interop
{
    sealed class WindowsMultiWindowNativePointerHandler : IDisposable
    {
        #region Native Methods
        
        [DllImport("WindowsTouchMultiWindow")]
        private static extern Result PointerHandler_Create(ref IntPtr handle);
        [DllImport("WindowsTouchMultiWindow")]
        private static extern Result PointerHandler_Destroy(IntPtr handle);
        [DllImport("WindowsTouchMultiWindow")]
        private static extern Result PointerHandler_Initialize(IntPtr handle, WindowsMultiWindowNativeLog messageCallback, int targetDisplay, TOUCH_API api, IntPtr windowHandle, WindowsMultiWindowNativePointerDelegate pointerCallback);
        [DllImport("WindowsTouchMultiWindow")]
        private static extern Result PointerHandler_SetTargetDisplay(IntPtr handle, int targetDisplay);
        [DllImport("WindowsTouchMultiWindow")]
        private static extern Result PointerHandler_SetScreenParams(IntPtr handle, WindowsMultiWindowNativeLog messageCallback, int width, int height, float offsetX, float offsetY, float scaleX, float scaleY);
        
        #endregion

        private IntPtr handle;

        internal WindowsMultiWindowNativePointerHandler()
        {
            // Create native resources
            handle = new IntPtr();
            var result = PointerHandler_Create(ref handle);
            if (result != Result.Ok)
            {
                handle = IntPtr.Zero;
                ResultHelper.CheckResult(result);
            }
        }

        ~WindowsMultiWindowNativePointerHandler()
        {
            Dispose(false);
        }
        
        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Free managed resources
            }

            // Free native resources
            if (handle != IntPtr.Zero)
            {
                PointerHandler_Destroy(handle);
                handle = IntPtr.Zero;
            }
        }

        internal void Initialize(WindowsMultiWindowNativeLog messageCallback, int targetDisplay, TOUCH_API api, IntPtr hWindow, WindowsMultiWindowNativePointerDelegate pointerCallback)
        {
            var result = PointerHandler_Initialize(handle, messageCallback, targetDisplay, api, hWindow, pointerCallback);
#if TOUCHSCRIPT_DEBUG
            ResultHelper.CheckResult(result);
#endif
        }

        internal void SetTargetDisplay(int value)
        {
            var result = PointerHandler_SetTargetDisplay(handle, value);
#if TOUCHSCRIPT_DEBUG
            ResultHelper.CheckResult(result);
#endif
        }

        internal void SetScreenParams(WindowsMultiWindowNativeLog messageCallback, int width, int height, float offsetX, float offsetY, float scaleX, float scaleY)
        {
            var result = PointerHandler_SetScreenParams(handle, messageCallback, width, height, offsetX, offsetY, scaleX, scaleY);
#if TOUCHSCRIPT_DEBUG
            ResultHelper.CheckResult(result);
#endif
        }
    }
}
#endif