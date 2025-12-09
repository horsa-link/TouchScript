#if UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;

namespace TouchScript.InputSources.InputHandlers.Interop
{
    sealed class NativePointerHandler : IDisposable
    {
        #region Native Methods

        [DllImport("WindowsTouch", CallingConvention = CallingConvention.StdCall)]
        private static extern void Init(TOUCH_API api, NativeLog log, NativePointerDelegate pointerDelegate);

        [DllImport("WindowsTouch", EntryPoint = "Dispose", CallingConvention = CallingConvention.StdCall)]
        private static extern void DisposePlugin();

        [DllImport("WindowsTouch", CallingConvention = CallingConvention.StdCall)]
        private static extern void SetScreenParams(int width, int height, float offsetX, float offsetY, float scaleX, float scaleY);

        #endregion

        internal NativePointerHandler() {}

        ~NativePointerHandler()
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
            DisposePlugin();
        }

        internal void Initialize(TOUCH_API api, NativeLog log, NativePointerDelegate pointerDelegate)
        {
            Init(api, log, pointerDelegate);
        }

        internal void SettingScreenParams(int width, int height, float offsetX, float offsetY, float scaleX, float scaleY)
        {
            SetScreenParams(width, height, offsetX, offsetY, scaleX, scaleY);
        }
    }
}
#endif