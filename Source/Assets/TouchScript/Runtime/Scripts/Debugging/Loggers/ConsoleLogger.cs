using System;
using UnityEngine;

namespace TouchScript.Debugging.Loggers
{
    public enum LogLevel
    {
        None,
        Exception,
        Error,
        Assert,
        Warning,
        Log
    }
    
    internal class UnityConsoleLogger
    {
        public static LogLevel Level = LogLevel.Log;

        public static void LogException(Exception exception, UnityEngine.Object context = null)
        {
//#if TOUCHSCRIPT_DEBUG
            if (Level >= LogLevel.Exception)
            {
                Debug.LogException(exception, context);
            }
//#endif
        }

        public static void LogError(string message, UnityEngine.Object context = null)
        {
//#if TOUCHSCRIPT_DEBUG
            if (Level >= LogLevel.Error)
            {
                Debug.LogError($"[TouchScript] {message}", context);
            }
//#endif
        }

        public static void LogWarning(string message, UnityEngine.Object context = null)
        {
//#if TOUCHSCRIPT_DEBUG
            if (Level >= LogLevel.Warning)
            {
                Debug.LogWarning($"[TouchScript] {message}", context);
            }
//#endif
        }

        public static void LogAssertion(string message, UnityEngine.Object context = null)
        {
//#if TOUCHSCRIPT_DEBUG
            if (Level >= LogLevel.Assert)
            {
                Debug.LogAssertion($"[TouchScript] {message}", context);
            }
//#endif
        }

        public static void Log(string message, UnityEngine.Object context = null)
        {
//#if TOUCHSCRIPT_DEBUG
            if (Level >= LogLevel.Log)
            {
                Debug.Log($"[TouchScript] {message}", context);
            }
//#endif
        }
    }
}