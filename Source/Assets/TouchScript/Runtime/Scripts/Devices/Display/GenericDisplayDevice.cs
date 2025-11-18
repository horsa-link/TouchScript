/*
 * @author Valentin Simonov / http://va.lent.in/
 */

using System.Text.RegularExpressions;
using UnityEngine;
#if UNITY_STANDALONE_WIN
using TouchScript.Utils.Platform;
#endif

namespace TouchScript.Devices.Display
{
    /// <summary>
    /// Display device which tries to guess current DPI if it's not set by platform.
    /// </summary>
    [HelpURL("http://touchscript.github.io/docs/html/T_TouchScript_Devices_Display_GenericDisplayDevice.htm")]
    public class GenericDisplayDevice : DisplayDevice
    {
        private static bool IsLaptop
        {
            get
            {
                if (isLaptop == null)
                {
                    var gpuName = SystemInfo.graphicsDeviceName.ToLower();
                    var regex = new Regex(@"^(.*mobile.*|intel hd graphics.*|.*m\s*(series)?\s*(opengl engine)?)$", RegexOptions.IgnoreCase);
                    isLaptop = regex.IsMatch(gpuName);
                }
                return isLaptop == true;
            }
        }

        private static bool? isLaptop;

        /// <inheritdoc />
        public override void UpdateDPI()
        {
            if (Screen.fullScreen)
            {
                var res = Screen.currentResolution;
                dpi = Mathf.Max(res.width / nativeResolution.x, res.height / nativeResolution.y) * nativeDPI;
            }
            else
            {
                dpi = nativeDPI;
            }
        }

        /// <inheritdoc />
        protected override void OnEnable()
        {
            base.OnEnable();

            Name = Application.platform.ToString();
            if (IsLaptop) Name += " (Laptop)";

            updateNativeResolution();
            updateNativeDPI();
            UpdateDPI();
        }

        private void updateNativeResolution()
        {
            switch (Application.platform)
            {
                // Editors / windowed
                case RuntimePlatform.WindowsEditor:
                    // This has not been tested and is probably wrong.
                    if (getHighestResolution(out nativeResolution)) break;
                    var res = Screen.currentResolution;
                    nativeResolution = new Vector2(res.width, res.height);
                    break;
                // PCs
                case RuntimePlatform.WindowsPlayer:
#if UNITY_STANDALONE_WIN
                    WindowsUtils.GetNativeMonitorResolution(out var width, out var height);
                    nativeResolution = new Vector2(width, height);
#endif
                    break;
                default:
                    // This has not been tested and is probably wrong.
                    if (getHighestResolution(out nativeResolution)) break;
                    res = Screen.currentResolution;
                    nativeResolution = new Vector2(res.width, res.height);
                    break;
            }
        }

        private void updateNativeDPI()
        {
            nativeDPI = Screen.dpi;
            if (nativeDPI > float.Epsilon) return;

            var res = Screen.currentResolution;
            var width = Mathf.Max(res.width, res.height);
            var height = Mathf.Min(res.width, res.height);

            switch (Application.platform)
            {
                // Editors / windowed
                case RuntimePlatform.WindowsEditor:
                // PCs
                case RuntimePlatform.WindowsPlayer:
                    // This has not been tested and is probably wrong.
                    // Let's guess
                    if (width >= 3840)
                    {
                        dpi = height <= 2160 ? 150 : // 28-31"
                            200;
                    }
                    else if (width >= 2880 && height == 1800)
                    {
                        dpi = 220; // 15" retina
                    }
                    else if (width >= 2560)
                    {
                        if (height >= 1600)
                        {
                            dpi = IsLaptop ? 226 : // 13.3" retina
                                101;               // 30" display
                        }
                        else if (height >= 1440)
                        {
                            dpi = 109; // 27" iMac
                        }
                    }
                    else if (width >= 2048)
                    {
                        dpi = height <= 1152 ? 100 : // 23-27"
                            171;                     // 15" laptop
                    }
                    else if (width >= 1920)
                    {
                        if (height >= 1440)
                        {
                            dpi = 110; // 24"
                        }
                        else if (height >= 1200)
                        {
                            dpi = 90; // 26-27"
                        }
                        else if (height >= 1080)
                        {
                            dpi = IsLaptop ? 130 : // 15" - 18" laptop
                                92;                // +-24" display
                        }
                    }
                    else if (width >= 1680)
                    {
                        dpi = 129; // 15" laptop
                    }
                    else if (width >= 1600)
                    {
                        dpi = 140; // 13" laptop
                    }
                    else if (width >= 1440)
                    {
                        dpi = height >= 1050 ? 125 : // 14" laptop
                            110;                     // 13" air or 15" macbook pro
                    }
                    else if (width >= 1366)
                    {
                        dpi = 125; // 10"-14" laptops
                    }
                    else if (width >= 1280)
                    {
                        dpi = 110;
                    }
                    else
                    {
                        dpi = 96;
                    }

                    break;
                default:
                    // This has not been tested and is probably wrong.
                    nativeDPI = 160;
                    break;
            }
        }

        private bool getHighestResolution(out Vector2 resolution)
        {
            resolution = new Vector2();

            var resolutions = Screen.resolutions;
            if (resolutions.Length == 0) return false;

            var r = resolutions[^1];
            resolution = new Vector2(r.width, r.height);
            return true;
        }
    }
}