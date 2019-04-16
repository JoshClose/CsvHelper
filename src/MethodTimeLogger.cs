using System.Reflection;
using Catel.Logging;
using System;

/// <summary>
/// Note: do not rename this class or put it inside a namespace.
/// </summary>
internal static class MethodTimeLogger
{
    #region Methods
    public static void Log(MethodBase methodBase, long milliseconds, string message)
    {
        Log(methodBase.DeclaringType, methodBase.Name, milliseconds, message);
    }

    public static void Log(Type type, string methodName, long milliseconds, string message)
    {
        if (type is null)
        {
            return;
        }

        if (milliseconds == 0)
        {
            // Don't log superfast methods
            return;
        }

        var finalMessage = $"[METHODTIMER] {type.Name}.{methodName} took '{milliseconds}' ms";

        if (!string.IsNullOrWhiteSpace(message))
        {
            finalMessage += $" | {message}";
        }

        var logger = LogManager.GetLogger(type);
        logger.Debug(finalMessage);
    }
    #endregion
}