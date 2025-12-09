#if UNITY_STANDALONE_WIN
using System;
using System.Collections;
using System.Runtime.InteropServices;
using TouchScript.Debugging.Loggers;
using TouchScript.Utils.Platform;
using UnityEngine;

namespace TouchScript.InputSources.InputHandlers.Interop
{
    [Flags]
    public enum WindowProperties
    {
        None            = 0,
        Tap             = 1,
        DoubleTap       = 2,
        PressAndTap     = 4,
        RightTap        = 8,
        PressAndHold    = 16,
        EdgeGestures    = 32,
        All = Tap | DoubleTap | PressAndTap | RightTap | PressAndHold | EdgeGestures,
        Ignore          = 64,
    }

    // FIXME: should add a way to reset the window state at when this object was created, so that in Dispose we can revert any change
    sealed class NativeWindowHandler
    {
        private const short VT_EMPTY = 0;
        private const short VT_BOOL = 11;
        private const short VT_LPWSTR = 31;
        private const short VARIANT_TRUE = -1;
        private const short VARIANT_FALSE = 0;
        /// <summary>
        /// Windows constant to turn off press and hold visual effect.
        /// </summary>
        private const string PRESS_AND_HOLD_ATOM = "MicrosoftTabletPenServiceProperty";
        /// <summary>
        /// Windows property store guid to turn off edge gestures and 3-4 fingers gestures
        /// </summary>
        private static readonly Guid DISABLE_TOUCH_WHEN_FULLSCREEN = new("32CE38B2-2C9A-41B1-9BC5-B3784394AA44");
        private static Guid IID_IPropertyStore = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");

        private IntPtr hWindow;
        public WindowProperties windowProperties;
        private readonly WindowProperties defaultWindowProperties;

        public NativeWindowHandler(IntPtr hwnd)
        {
            hWindow = hwnd;

            defaultWindowProperties = WindowProperties.Ignore; //FIXME:
            //GetDefaultWindowProperties(hWindow);
        }

        public void ResetWindowProperties() => ApplyWindowProperties(hWindow, ~windowProperties & WindowProperties.All);
        public void ApplyDefaultWindowProperties() => ApplyWindowProperties(defaultWindowProperties);
        public void ApplyWindowProperties(WindowProperties windowProperties)
        {
            this.windowProperties = windowProperties;
            if (windowProperties.HasFlag(WindowProperties.Ignore)) return;

            // if we're updating the EdgeGestures prop
            if (edgeGesturesNeedUpdate(windowProperties))
            {
                UnityConsoleLogger.LogWarning($"Updating the \"{nameof(DISABLE_TOUCH_WHEN_FULLSCREEN)}\" property in a synchronous way is not recommended, use {nameof(ApplyWindowPropertiesAsync)} instead");
            }

            ApplyWindowProperties(hWindow, windowProperties);
        }
        public IEnumerator ApplyWindowPropertiesAsync(WindowProperties windowProperties, bool avoidChangingFullScreenModeIfPossible = true)
        {
            this.windowProperties = windowProperties;
            if (windowProperties.HasFlag(WindowProperties.Ignore)) yield break;

            if (avoidChangingFullScreenModeIfPossible && edgeGesturesNeedUpdate(windowProperties))
            {
                //ResetWindowProperties();
                ApplyWindowProperties(hWindow, windowProperties);
            }
            else
            {
                // The purpose is to "consume" the touch input handled by Windows so that it does not
                // interfere with specific gestures configured by the user in Windows Settings.
                // In Windows 11, settings related to touch gestures (edge gestures, 3-4 finger gestures)
                // were introduced which, if enabled, compromise the use of touch apps since these are managed directly by the operating system.
                // According to Microsoft documentation [https://learn.microsoft.com/en-us/windows/apps/design/input/touch-developer-guide#custom-touch-interactions]
                // there is no "official" way to solve the problem other than the user disabling these settings.
                // Nevertheless, an "unofficial" method was found that uses APIs from "shell32.dll" to "simulate" the use of a touch app in a Windows "kiosk" environment.
                // Specifically, we set the property [https://learn.microsoft.com/en-us/windows/win32/properties/props-system-edgegesture-disabletouchwhenfullscreen]
                // which disables "Edge Gestures" only for Windows in Fullscreen mode.
                // As a side effect, this also disables "3-4 finger gestures" only when the Window is in focus and in the Foreground and only after:
                // - a "focus switch" between the Unity Window and another Window (on the same Display)
                // - or a mode change of the Unity Window from "Windowed" to "FullscreenWindow"
                // In the first case, we have the problem that "3-4 finger gestures" are not disabled at app startup even if the Unity Window is in Focus and Foreground,
                // whereas in the second case, by forcing the FullscreenMode change, we can always apply the side effect to the Unity Window.
                // Therefore, we use the second case (which can be done programmatically and concerns only the app), keeping in mind that Unity
                // allows changing FullscreenMode also via the ALT + ENTER key combination and saves the new FullscreenMode value
                // in the Windows registry at the path "HKEY_CURRENT_USER\Software\[CompanyName]\[ProductName]\Screenmanager Fullscreen mode_<characters>"

                var isFullScreen = Screen.fullScreen;
                var fullScreenMode = Screen.fullScreenMode;
                var changeToFullScreenMode = fullScreenMode == FullScreenMode.ExclusiveFullScreen || fullScreenMode == FullScreenMode.FullScreenWindow ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;

                Screen.fullScreen = !isFullScreen;
                yield return null;
                Screen.SetResolution(Screen.width - 1, Screen.height - 1, changeToFullScreenMode);
                yield return null;

                //ResetWindowProperties();
                ApplyWindowProperties(hWindow, windowProperties);

                Screen.fullScreen = isFullScreen;
                yield return null;
                Screen.SetResolution(Screen.width, Screen.height, fullScreenMode);
            }

            //GetDefaultWindowProperties(hWindow);
        }

        private void ApplyWindowProperties(IntPtr hwnd, WindowProperties windowProperties)
        {
            UnityConsoleLogger.Log($"[{nameof(NativeWindowHandler)}] *** Start {nameof(ApplyWindowProperties)}: {hwnd.ToString("X")} ***");

            enableTap(hwnd, windowProperties.HasFlag(WindowProperties.Tap));

            enableDoubleTap(hwnd, windowProperties.HasFlag(WindowProperties.DoubleTap));

            enablePressAndTap(hwnd, windowProperties.HasFlag(WindowProperties.PressAndTap));

            enableRightTap(hwnd, windowProperties.HasFlag(WindowProperties.RightTap));

            enablePressAndHold(hwnd, windowProperties.HasFlag(WindowProperties.PressAndHold));

            enableEdgeGestures(hwnd, windowProperties.HasFlag(WindowProperties.EdgeGestures));
            
            UnityConsoleLogger.Log($"[{nameof(NativeWindowHandler)}] *** End {nameof(ApplyWindowProperties)}: {hwnd.ToString("X")} ***");
        }

        private void GetDefaultWindowProperties(IntPtr hwnd)
        {
            foreach(var v in Enum.GetValues(typeof(FeedbackType)))
            {
                if ((FeedbackType)v == FeedbackType.FeedbackMax) continue;
                var res = getWindowFeedbackSetting(hwnd, (FeedbackType)v);
                UnityConsoleLogger.Log($"{(FeedbackType)v} is {res}");
            }
            UnityConsoleLogger.Log($"AreEdgeGestures enabled: {areEdgeGesturesEnabled(hwnd)}");
            UnityConsoleLogger.Log($"IsPressAndHold enabled: {isPressAndHoldEnabled(hwnd)}");
        }

        private void enableTap(IntPtr hwnd, bool enable, bool verify = true)
        {
            setWindowFeedbackSetting(hwnd, FeedbackType.FeedbackTouchTap, enable, verify);
            setWindowFeedbackSetting(hwnd, FeedbackType.FeedbackTouchContactVisualization, enable, verify);
        }

        private void enableDoubleTap(IntPtr hwnd, bool enable, bool verify = true) => setWindowFeedbackSetting(hwnd, FeedbackType.FeedbackTouchDoubleTap, enable, verify);

        private void enablePressAndTap(IntPtr hwnd, bool enable, bool verify = true) => setWindowFeedbackSetting(hwnd, FeedbackType.FeedbackGesturePressAndTap, enable, verify);

        private void enableRightTap(IntPtr hwnd, bool enable, bool verify = true) => setWindowFeedbackSetting(hwnd, FeedbackType.FeedbackTouchRightTap, enable, verify);

        private void enablePressAndHold(IntPtr hwnd, bool enable, bool verify = true)
        {
            var atomID = WindowsUtils.GlobalAddAtom(PRESS_AND_HOLD_ATOM);
            if (atomID == 0)
            {
                UnityConsoleLogger.LogError($"Cannot retrieve \"{nameof(PRESS_AND_HOLD_ATOM)}\" atom from atom table, GetLastWin32Error: {Marshal.GetLastWin32Error()}");
                return;
            }

            var atomStr = new IntPtr(unchecked((int)atomID));
            if (enable)
            {
                if (WindowsUtils.RemoveProp(hwnd, atomStr) == 0)
                {
                    UnityConsoleLogger.Log($"Cannot remove the window property named \"{nameof(PRESS_AND_HOLD_ATOM)}\", GetLastWin32Error: {Marshal.GetLastWin32Error()}");
                }
            }
            else
            {
                if (WindowsUtils.SetProp(hwnd, atomStr,
                    (int)(WindowsUtils.Tablet.TABLET_DISABLE_PRESSANDHOLD |      // disables press and hold (right-click) gesture
                    WindowsUtils.Tablet.TABLET_DISABLE_PENTAPFEEDBACK |    // disables UI feedback on pen up (waves)
                    WindowsUtils.Tablet.TABLET_DISABLE_PENBARRELFEEDBACK | // disables UI feedback on pen button down (circle)
                    WindowsUtils.Tablet.TABLET_DISABLE_FLICKS)              // disables pen flicks (back, forward, drag down, drag up);
                    ) == 0)
                {
                    UnityConsoleLogger.Log($"Cannot set the window property named \"{nameof(PRESS_AND_HOLD_ATOM)}\", GetLastWin32Error: {Marshal.GetLastWin32Error()}");
                }
            }

            if (WindowsUtils.GlobalDeleteAtom(atomID) == 0)
            {
                UnityConsoleLogger.LogWarning($"Cannot delete \"{nameof(PRESS_AND_HOLD_ATOM)}\" atom from atom table, this can cause memory leak, GetLastWin32Error: {Marshal.GetLastWin32Error()}");
            }

            setWindowFeedbackSetting(hwnd, FeedbackType.FeedbackTouchPressAndHold, enable);

#if TOUCHSCRIPT_DEBUG
            if (verify)
            {
                if (enable != isPressAndHoldEnabled(hwnd))
                {
                    UnityConsoleLogger.LogError($"Did not change the window property named \"{nameof(PRESS_AND_HOLD_ATOM)}\" into {enable}");
                }
                if(enable != getWindowFeedbackSetting(hwnd, FeedbackType.FeedbackTouchPressAndHold))
                {
                    UnityConsoleLogger.LogError($"Did not change the window feedback setting named \"{Enum.GetName(typeof(FeedbackType), FeedbackType.FeedbackTouchPressAndHold)}\" into {enable}");
                }
            }
            else
            {
                UnityConsoleLogger.Log($"Changed the window property named \"{nameof(PRESS_AND_HOLD_ATOM)}\" and the the window feedback setting named \"{Enum.GetName(typeof(FeedbackType), FeedbackType.FeedbackTouchPressAndHold)}\" into {enable}");
            }
#else
            UnityConsoleLogger.Log($"Changed the window property named \"{nameof(PRESS_AND_HOLD_ATOM)}\" and the window feedback setting named \"{Enum.GetName(typeof(FeedbackType), FeedbackType.FeedbackTouchPressAndHold)}\" into {enable}");
#endif
        }

        private bool isPressAndHoldEnabled(IntPtr hwnd)
        {
            var atomID = WindowsUtils.GlobalAddAtom(PRESS_AND_HOLD_ATOM);
            if (atomID == 0)
            {
                UnityConsoleLogger.LogError($"Cannot retrieve \"{nameof(PRESS_AND_HOLD_ATOM)}\" atom from atom table, GetLastWin32Error: {Marshal.GetLastWin32Error()}");
                return false;
            }

            var atomStr = new IntPtr(unchecked((int)atomID));
            int result = (int)WindowsUtils.GetProp(hwnd, atomStr);
            if (result == 0)
            {
                UnityConsoleLogger.Log($"Cannot get the window property named \"{nameof(PRESS_AND_HOLD_ATOM)}\", GetLastWin32Error: {Marshal.GetLastWin32Error()}");
            }

            if (WindowsUtils.GlobalDeleteAtom(atomID) == 0)
            {
                UnityConsoleLogger.LogWarning($"Cannot delete \"{nameof(PRESS_AND_HOLD_ATOM)}\" atom from atom table, this can cause memory leak, GetLastWin32Error: {Marshal.GetLastWin32Error()}");
            }

            return result != (int)(WindowsUtils.Tablet.TABLET_DISABLE_PRESSANDHOLD |
                                WindowsUtils.Tablet.TABLET_DISABLE_PENTAPFEEDBACK |
                                WindowsUtils.Tablet.TABLET_DISABLE_PENBARRELFEEDBACK |
                                WindowsUtils.Tablet.TABLET_DISABLE_FLICKS);
        }

        private void enableEdgeGestures(IntPtr hwnd, bool enable, bool verify = true)
        {
            var hr = WindowsUtils.SHGetPropertyStoreForWindow(hwnd, ref IID_IPropertyStore, out var propStore);
            if (hr != 0 || propStore == null)
            {
                UnityConsoleLogger.LogError($"Cannot retrieve the property store for window named \"{nameof(DISABLE_TOUCH_WHEN_FULLSCREEN)}\", GetLastWin32Error: {Marshal.GetLastWin32Error()}");
                return;
            }
            
            var key = new WindowsUtils.PropertyKey { fmtid = DISABLE_TOUCH_WHEN_FULLSCREEN, pid = 2 };
            var value = new WindowsUtils.PropVariant();
            if (enable)
            {
                //value = new WindowsUtils.PropVariant { vt = VT_EMPTY };
                value = new WindowsUtils.PropVariant { vt = VT_BOOL, boolVal = VARIANT_FALSE }; // -1 = TRUE, 0 = FALSE
            }
            else
            {
                value = new WindowsUtils.PropVariant { vt = VT_BOOL, boolVal = VARIANT_TRUE }; // -1 = TRUE, 0 = FALSE
            }

            propStore.SetValue(ref key, ref value);
            propStore.Commit(); // shouldn't be needed

            Marshal.ReleaseComObject(propStore);

#if TOUCHSCRIPT_DEBUG
            if (verify && enable != areEdgeGesturesEnabled(hwnd))
            {
                UnityConsoleLogger.LogError($"Did not change the property store for window named \"{nameof(DISABLE_TOUCH_WHEN_FULLSCREEN)}\" into {enable}");
            }
            else
            {
                UnityConsoleLogger.Log($"Changed the property store for window named \"{nameof(DISABLE_TOUCH_WHEN_FULLSCREEN)}\" into {enable}");
            }
#else
            UnityConsoleLogger.Log($"Changed the property store for window named \"{nameof(DISABLE_TOUCH_WHEN_FULLSCREEN)}\" into {enable}");
#endif
        }

        private bool? areEdgeGesturesEnabled(IntPtr hwnd)
        {
            var hr = WindowsUtils.SHGetPropertyStoreForWindow(hwnd, ref IID_IPropertyStore, out var propStore);
            if (hr != 0 || propStore == null)
            {
                UnityConsoleLogger.LogError($"Cannot retrieve the property store for window named \"{nameof(DISABLE_TOUCH_WHEN_FULLSCREEN)}\", GetLastWin32Error: {Marshal.GetLastWin32Error()}");

                Marshal.ReleaseComObject(propStore);
                return null;
            }

            var key = new WindowsUtils.PropertyKey { fmtid = DISABLE_TOUCH_WHEN_FULLSCREEN, pid = 2 };
            var value = new WindowsUtils.PropVariant { vt = VT_BOOL };

            propStore.GetValue(ref key, ref value);

            bool? result = null;
            if (value.vt == VT_BOOL) result = value.boolVal == VARIANT_FALSE;
            else if (value.vt == VT_LPWSTR) result = short.Parse(Marshal.PtrToStringUni(value.pwszVal)) == VARIANT_FALSE;

            if (result == null)
            {
                UnityConsoleLogger.LogError($"Cannot retrieve the property store for window named \"{nameof(DISABLE_TOUCH_WHEN_FULLSCREEN)}\"");
            }

            Marshal.ReleaseComObject(propStore);

            return result;
        }

        private bool edgeGesturesNeedUpdate(WindowProperties windowProperties)
        {
            var areEdgeGesturesEnabled = this.areEdgeGesturesEnabled(hWindow);
            return (areEdgeGesturesEnabled == null
                || (windowProperties.HasFlag(WindowProperties.EdgeGestures) && !(bool)areEdgeGesturesEnabled)
                || (!windowProperties.HasFlag(WindowProperties.EdgeGestures) && (bool)areEdgeGesturesEnabled));
        }

        /*-------------------------------------------------------------------------------------------*/

        // FIXME: only from Windows 8 onward
        private bool setWindowFeedbackSetting(IntPtr hwnd, FeedbackType feedback, bool enable, bool verify = true)
        {
            var settings = new WindowsUtils.FeedbackTypeSettings { Enable = enable };

            var size = Marshal.SizeOf(settings);
            var ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(settings, ptr, false);

            var result = WindowsUtils.SetWindowFeedbackSetting(hwnd, (uint)feedback, 0, (uint)size, ptr);
            if (!result)
            {
                UnityConsoleLogger.LogWarning(
                    $"Cannot change the window feedback setting named \"{Enum.GetName(typeof(FeedbackType), feedback)}\" into {enable}, " +
                    $"Win32Error: {Marshal.GetLastWin32Error().ToString("X")}");
            }

            Marshal.FreeHGlobal(ptr);

#if TOUCHSCRIPT_DEBUG
            if (verify && enable != getWindowFeedbackSetting(hwnd, feedback))
            {
                UnityConsoleLogger.LogError($"Did not change the window feedback setting named \"{Enum.GetName(typeof(FeedbackType), feedback)}\" into {enable}");
            }
            else
            {
                UnityConsoleLogger.Log($"Changed window feedback setting named \"{Enum.GetName(typeof(FeedbackType), feedback)}\" into {enable}");
            }
#else
            UnityConsoleLogger.Log($"Changed window feedback setting named \"{Enum.GetName(typeof(FeedbackType), feedback)}\" into {enable}");
#endif

            return result;
        }

        private bool? getWindowFeedbackSetting(IntPtr hwnd, FeedbackType feedback)
        {
            var settings = new WindowsUtils.FeedbackTypeSettings();

            uint size = (uint)Marshal.SizeOf(settings);
            var ptr = Marshal.AllocHGlobal((int)size);
            Marshal.StructureToPtr(settings, ptr, false);

            var result = WindowsUtils.GetWindowFeedbackSetting(hwnd, (uint)feedback, 0, ref size, ptr);
            if (!result)
            {
                UnityConsoleLogger.LogWarning(
                    $"Cannot get the window feedback setting named \"{Enum.GetName(typeof(FeedbackType), feedback)}\", " +
                    $"Win32Error: {Marshal.GetLastWin32Error().ToString("X")}");

                Marshal.FreeHGlobal(ptr);

                return null;
            }
            else
            {
                Marshal.FreeHGlobal(ptr);

                return settings.Enable;
            }
        }
    }
}
#endif