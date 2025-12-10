/*
 * @author Valentin Simonov / http://va.lent.in/
 */

#if UNITY_STANDALONE_WIN

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using UnityEngine;

namespace TouchScript.Utils.Platform
{
    /// <summary>
    /// Utility methods on Windows.
    /// </summary>
    public static class WindowsUtils
    {
        public const int MONITOR_DEFAULTTONEAREST = 2;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        private delegate bool EnumWindowsChildProc(IntPtr hwnd, IntPtr lParam);

        /// <summary>
        /// Retrieves the native monitor resolution.
        /// </summary>
        /// <param name="width">Output width.</param>
        /// <param name="height">Output height.</param>
        public static void GetNativeMonitorResolution(out int width, out int height)
        {
            var monitor = MonitorFromWindow(GetActiveWindow(), MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = Marshal.SizeOf(monitorInfo);
            if (!GetMonitorInfo(monitor, ref monitorInfo))
            {
                width = Screen.width;
                height = Screen.height;
            }
            else
            {
                width = monitorInfo.rcMonitor.Width;
                height = monitorInfo.rcMonitor.Height;
            }
        }

        /// <summary>
        /// Retrieves all the top windows of a process by its PID: <paramref name="processId"/>
        /// </summary>
        /// <param name="processId"></param>
        /// <returns></returns>
        public static List<IntPtr> GetTopWindowHandlesForProcess(int processId)
        {
            var topWindowHandles = new List<IntPtr>();

            EnumWindows((topWindowHandle, lParam) =>
            {
                GetWindowThreadProcessId(topWindowHandle, out var windowProcessId);
                if (windowProcessId == processId)
                {
                    topWindowHandles.Add(topWindowHandle);
                }

                return true;
            }, IntPtr.Zero);

            return topWindowHandles;
        }
        
        /// <summary>
        /// Retrieves all the direct children of a window with pointer: <paramref name="parentWindowHandle"/>
        /// </summary>
        /// <param name="parentWindowHandle"></param>
        /// <returns></returns>
        public static List<IntPtr> GetChildWindowHandlesForProcess(IntPtr parentWindowHandle)
        {
            var childWindowHandles = new List<IntPtr>();

            EnumChildWindows(parentWindowHandle, (childWindowHandle, lParam) =>
            {
                childWindowHandles.Add(childWindowHandle);

                return true;
            }, IntPtr.Zero);

            return childWindowHandles;
        }

        /// <summary>
        /// Retrieves all the top windows of the Unity app by its process PID: <paramref name="processId"/>.<br/>
        /// It should return: one main window and <c>n</c> side windows, one for each <c>Display.Activate</c>
        /// </summary>
        /// <param name="processId"></param>
        /// <returns></returns>
        public static List<IntPtr> GetMainWindowHandlesForBuildProcess(int processId)
        {
            var topWindowHandles = GetTopWindowHandlesForProcess(processId);
            List<IntPtr> unityWndClassHandles = new();

            StringBuilder className = null;
            for(var i = 0; i < topWindowHandles.Count; i++)
            {
                className = new StringBuilder(256);
                if (GetClassName(topWindowHandles[i], className, className.Capacity) != 0
                    && className.ToString() == "UnityWndClass")
                {
                    unityWndClassHandles.Add(topWindowHandles[i]);
                }
            }

            return unityWndClassHandles;
        }

        /// <summary>
        /// Retrieves all the UnityEditor windows by its process PID: <paramref name="processId"/> that represent the <c>Game</c> tabs.<br/>
        /// It should return <c>n</c> side windows, one for each <c>Game</c> tab opened in the UnityEditor
        /// </summary>
        /// <param name="processId"></param>
        /// <returns></returns>
        public static List<IntPtr> GetGameWindowHandlesForEditorProcess(int processId)
        {
            var topWindowHandles = GetTopWindowHandlesForProcess(processId);
            List<IntPtr> unityContainerWndClassHandles = new();

            StringBuilder className;
            for (var i = 0; i < topWindowHandles.Count; i++)
            {
                className = new StringBuilder(256);
                if (GetClassName(topWindowHandles[i], className, className.Capacity) != 0 && className.ToString() == "UnityContainerWndClass")
                {
                    unityContainerWndClassHandles.Add(topWindowHandles[i]);
                }
            }

            List<IntPtr> unityGUIViewWndClassHandles = new();
            for (var i = 0; i < unityContainerWndClassHandles.Count; i++)
            {
                var childWindowHandles = GetChildWindowHandlesForProcess(unityContainerWndClassHandles[i]);
                for(var j = 0; j < childWindowHandles.Count; j++)
                {
                    className = new StringBuilder(256);
                    var length = GetWindowTextLength(childWindowHandles[j]);
                    var windowText = new StringBuilder(length + 1);
                    if (GetClassName(childWindowHandles[j], className, className.Capacity) != 0
                        && className.ToString() == "UnityGUIViewWndClass"
                        && GetWindowText(childWindowHandles[j], windowText, windowText.Capacity) != 0
                        && windowText.ToString() == "UnityEditor.GameView")
                    {
                        unityGUIViewWndClassHandles.Add(childWindowHandles[j]);
                    }
                }
            }

            return unityGUIViewWndClassHandles;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;

            public RECT(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public int Height
            {
                get { return Bottom - Top; }
                set { Bottom = value + Top; }
            }

            public int Width
            {
                get { return Right - Left; }
                set { Right = value + Left; }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FeedbackTypeSettings
        {
            public bool Enable;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PropertyKey
        {
            public Guid fmtid;
            public int pid; // CLS-compliant: changed uint with int
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct PropVariant
        {
            [FieldOffset(0)] public short vt;
            [FieldOffset(8)] public short boolVal; // CLS-compliant: bool to short
            [FieldOffset(8)] public IntPtr pwszVal;
        }

        [ComImport]
        [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IPropertyStore
        {
            void GetCount(ref int cProps);
            void GetAt(int iProp, ref PropertyKey pkey);
            void GetValue(ref PropertyKey key, ref PropVariant pv);
            void SetValue(ref PropertyKey key, ref PropVariant pv);
            void Commit();
        }

        public enum Tablet
        {
            /// <summary>
            /// disables press and hold (right-click) gesture
            /// </summary>
            TABLET_DISABLE_PRESSANDHOLD        = 0x00000001,
            /// <summary>
            /// disables UI feedback on pen up (waves)
            /// </summary>
            TABLET_DISABLE_PENTAPFEEDBACK      = 0x00000008,
            /// <summary>
            /// disables UI feedback on pen button down (circle)
            /// </summary>
            TABLET_DISABLE_PENBARRELFEEDBACK   = 0x00000010,
            TABLET_DISABLE_TOUCHUIFORCEON      = 0x00000100,
            TABLET_DISABLE_TOUCHUIFORCEOFF     = 0x00000200,
            TABLET_DISABLE_TOUCHSWITCH         = 0x00008000,
            /// <summary>
            /// disables pen flicks (back, forward, drag down, drag up)
            /// </summary>
            TABLET_DISABLE_FLICKS              = 0x00010000,
            TABLET_ENABLE_FLICKSONCONTEXT      = 0x00020000,
            TABLET_ENABLE_FLICKLEARNINGMODE    = 0x00040000,
            TABLET_DISABLE_SMOOTHSCROLLING     = 0x00080000,
            TABLET_DISABLE_FLICKFALLBACKKEYS   = 0x00100000,
            TABLET_ENABLE_MULTITOUCHDATA       = 0x01000000
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsChildProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("shell32.dll", SetLastError = true)]
        [SuppressUnmanagedCodeSecurity]
        public static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid riid, out IPropertyStore ppv);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowFeedbackSetting(IntPtr hwnd, uint feedback, uint dwFlags, uint size, IntPtr config);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowFeedbackSetting(IntPtr hwnd, uint feedback, uint dwFlags, ref uint size, IntPtr config);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("Kernel32.dll", SetLastError = true)]
        public static extern ushort GlobalAddAtom(string lpString);

        [DllImport("Kernel32.dll", SetLastError = true)]
        public static extern ushort GlobalDeleteAtom(ushort nAtom);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetProp(IntPtr hWnd, IntPtr lpString, int hData);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int RemoveProp(IntPtr hWnd, IntPtr lpString);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr GetProp(IntPtr hWnd, IntPtr lpString);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr EnableMouseInPointer(bool value);
    }
}

#endif