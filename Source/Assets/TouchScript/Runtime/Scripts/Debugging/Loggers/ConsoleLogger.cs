using System;
using UnityEngine;

namespace TouchScript.Debugging.Loggers
{
    public class ConsoleLogger
    {
        public enum LoggerLevel
        {
            None,
            Exception,
            Error,
            Warning,
            Assert,
            Log
        }

        public static LoggerLevel Level = LoggerLevel.Log;

        public static void LogException(Exception exception, UnityEngine.Object context = null)
        {
//#if TOUCHSCRIPT_DEBUG
            if (Level >= LoggerLevel.Exception)
            {
                Debug.LogException(exception, context);
            }
//#endif
        }

        public static void LogError(string message, UnityEngine.Object context = null)
        {
//#if TOUCHSCRIPT_DEBUG
            if (Level >= LoggerLevel.Error)
            {
                Debug.LogError($"[TouchScript] {message}", context);
            }
//#endif
        }

        public static void LogWarning(string message, UnityEngine.Object context = null)
        {
#if TOUCHSCRIPT_DEBUG
            if (Level > LoggerLevel.Error)
            {
                Debug.LogWarning($"[TouchScript] {message}", context);
            }
#endif
        }

        public static void LogAssertion(string message, UnityEngine.Object context = null)
        {
#if TOUCHSCRIPT_DEBUG
            if (Level >= LoggerLevel.Assert)
            {
                Debug.LogAssertion($"[TouchScript] {message}", context);
            }
#endif
        }


        public static void Log(string message, UnityEngine.Object context = null)
        {
#if TOUCHSCRIPT_DEBUG
            if (Level >= LoggerLevel.Log)
            {
                Debug.Log($"[TouchScript] {message}", context);
            }
#endif
        }
    }
}