#if UNITY_STANDALONE_LINUX

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TouchScript.Debugging.Loggers;
using TouchScript.InputSources.InputHandlers.Interop;
using UnityEngine;

namespace TouchScript.InputSources.InputHandlers
{
    public class X11PointerHandlerSystem : IInputSourceSystem, IDisposable
    {
        [DllImport("libX11TouchMultiWindow")]
        private static extern Result PointerHandlerSystem_Create(MessageCallback messageCallback, ref IntPtr handle);
        [DllImport("libX11TouchMultiWindow")]
        private static extern Result PointerHandlerSystem_ProcessEventQueue(IntPtr handle);
        [DllImport("libX11TouchMultiWindow")]
        private static extern Result PointerHandlerSystem_GetWindowsOfProcess(IntPtr handle, int pid, out IntPtr windows, out uint numWindows);
        [DllImport("libX11TouchMultiWindow")]
        private static extern Result PointerHandlerSystem_FreeWindowsOfProcess(IntPtr handle, IntPtr windows);
        [DllImport("libX11TouchMultiWindow")]
        private static extern Result PointerHandlerSystem_Destroy(IntPtr handle);

        private MessageCallback messageCallback;
        private IntPtr handle;

        public X11PointerHandlerSystem()
        {
            messageCallback = OnNativeMessage;
            
            // Create native resources
            handle = new IntPtr();
            var result = PointerHandlerSystem_Create(messageCallback, ref handle);
            if (result != Result.Ok)
            {
                handle = IntPtr.Zero;
                ResultHelper.CheckResult(result);
            }
        }
        
        ~X11PointerHandlerSystem()
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
                PointerHandlerSystem_Destroy(handle);
                handle = IntPtr.Zero;
            }
        }

        public void PrepareInputs()
        {
            var result = PointerHandlerSystem_ProcessEventQueue(handle);
#if TOUCHSCRIPT_DEBUG
            ResultHelper.CheckResult(result);
#endif
        }

        public void GetWindowsOfProcess(int pid, List<IntPtr> procWindows)
        {
            var result =
                PointerHandlerSystem_GetWindowsOfProcess(handle, pid, out var windows, out uint numWindows);
            ResultHelper.CheckResult(result);
            
            // Copy window handles
            IntPtr[] w = new IntPtr[numWindows];
            Marshal.Copy(windows, w, 0, (int)numWindows);
            
            // Cleanup native side
            PointerHandlerSystem_FreeWindowsOfProcess(handle, windows);
            
            procWindows.AddRange(w);
        }
        
        // Attribute used for IL2CPP
        [AOT.MonoPInvokeCallback(typeof(MessageCallback))]
        private void OnNativeMessage(int messageType, string message)
        {
            switch (messageType)
            {
#if TOUCHSCRIPT_DEBUG
                case 0:
                    ConsoleLogger.Log("[libX11TouchMultiWindow.so]: " + message);
                    break;
#endif
                case 1:
                    ConsoleLogger.Log("[libX11TouchMultiWindow.so]: " + message);
                    break;
                case 2:
                    ConsoleLogger.Warning("[libX11TouchMultiWindow.so]: " + message);
                    break;
                case 3:
                    ConsoleLogger.Error("[libX11TouchMultiWindow.so]: " + message);
                    break;
            }
        }
    }
}
#endif