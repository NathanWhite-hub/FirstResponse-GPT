using System;
using Rage;

namespace FirstResponseGPT.Utils
{
    public static class Logger
    {
        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="className">The name of the class calling the logger.</param>
        /// <param name="message">The message to log.</param>
        public static void LogInfo(string className, string message)
        {
            Game.LogTrivial($"[FirstResponseGPT][{className}] {message}");
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="className">The name of the class calling the logger.</param>
        /// <param name="message">The warning message to log.</param>
        public static void LogWarning(string className, string message)
        {
            Game.LogTrivial($"[FirstResponseGPT][{className}][WARNING] {message}");
        }

        /// <summary>
        /// Logs an error message with an optional exception.
        /// </summary>
        /// <param name="className">The name of the class calling the logger.</param>
        /// <param name="message">The error message to log.</param>
        /// <param name="ex">Optional exception for stack trace logging.</param>
        public static void LogError(string className, string message, Exception ex = null)
        {
            string errorLog = $"[FirstResponseGPT][{className}][ERROR] {message}";
            if (ex != null)
            {
                errorLog += $" | Exception: {ex.Message}\nStack Trace:\n{ex.StackTrace}";
            }
            Game.LogTrivial(errorLog);
        }

        /// <summary>
        /// Logs a debug message, useful for troubleshooting.
        /// </summary>
        /// <param name="className">The name of the class calling the logger.</param>
        /// <param name="message">The debug message to log.</param>
        public static void LogDebug(string className, string message)
        {
        #if DEBUG
            Game.LogTrivial($"[FirstResponseGPT][{className}][DEBUG] {message}");
        #endif
        }
    }
}
