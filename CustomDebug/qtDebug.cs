using System;
using System.Diagnostics;
using UnityEngine;

namespace qtLib.CustomDebug
{
    public static class qtDebug
    {
        [HideInCallstack]
        [Conditional("ENABLE_CUSTOM_DEBUG")]
        public static void Log(
            object message,
            UnityEngine.Object context = null)
        {
            Log(LogType.Log, message, context);
        }
        
        [HideInCallstack]
        [Conditional("ENABLE_CUSTOM_DEBUG")]
        public static void LogWarning(
            string message,
            UnityEngine.Object context = null)
        {
            Log(LogType.Warning, message, context);
        }

        [HideInCallstack]
        [Conditional("ENABLE_CUSTOM_DEBUG")]
        public static void LogError(
            string message,
            UnityEngine.Object context = null)
        {
            Log(LogType.Warning, message, context);
        }
        
        [HideInCallstack]
        [Conditional("ENABLE_CUSTOM_DEBUG")]
        public static void Log(
            LogType type,
            object message,
            UnityEngine.Object context = null)
        {
            StackFrame frame = new StackFrame(1);
            var method = frame.GetMethod();

            string className = method?.DeclaringType?.Name ?? "UnknownClass";
            string functionName = method?.Name ?? "UnknownFunction";

            string log = $"[{className}.{functionName}] {message}";
            switch (type)
            {
                case LogType.Log:
                {
                    UnityEngine.Debug.unityLogger.Log(log, context);
                    break;
                }                
                case LogType.Warning:
                {
                    UnityEngine.Debug.LogWarning(log, context);
                    break;
                }
                case LogType.Error:
                {
                    UnityEngine.Debug.LogError(log, context);
                    break;
                }
                default:
                {
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
                }
            }
        }
    }
}
