using System;
using System.Linq;
using Vintagestory;
using Vintagestory.API.Common;

namespace RecipesLibrary;

internal static class LoggerWrapper
{
    private const string cPrefix = "[Recipe lib]";

    public static void Notify(ICoreAPI? api, object caller, string format) => api?.Logger?.Notification(Format(caller, format));
    public static void Notify(IWorldAccessor? world, object caller, string format) => world?.Logger?.Notification(Format(caller, format));
    public static void Notify(ILogger? logger, object caller, string format) => logger?.Notification(Format(caller, format));

    public static void Warn(ICoreAPI? api, object caller, string format) => api?.Logger?.Warning(Format(caller, format));
    public static void Warn(IWorldAccessor? world, object caller, string format) => world?.Logger?.Warning(Format(caller, format));
    public static void Warn(ILogger? logger, object caller, string format) => logger?.Warning(Format(caller, format));

    public static void Error(ICoreAPI? api, object caller, string format) => api?.Logger?.Error(Format(caller, format));
    public static void Error(ILogger? logger, object caller, string format) => logger?.Error(Format(caller, format));
    public static void Error(IWorldAccessor? world, object caller, string format) => world?.Logger?.Error(Format(caller, format));

    public static void Debug(ICoreAPI? api, object caller, string format) => api?.Logger?.Debug(Format(caller, format));
    public static void Debug(IWorldAccessor? world, object caller, string format) => world?.Logger?.Debug(Format(caller, format));
    public static void Debug(ILogger? logger, object caller, string format) => logger?.Debug(Format(caller, format));

    public static void Verbose(ICoreAPI? api, object caller, string format) => api?.Logger?.VerboseDebug(Format(caller, format));
    public static void Verbose(IWorldAccessor? world, object caller, string format) => world?.Logger?.VerboseDebug(Format(caller, format));
    public static void Verbose(ILogger? logger, object caller, string format) => logger?.VerboseDebug(Format(caller, format));

    private static string Format(object caller, string format) => $"{cPrefix} [{GetCallerTypeName(caller)}] {format}".Replace("{", "{{").Replace("}", "}}");
    private static string GetCallerTypeName(object caller)
    {
        Type type = caller.GetType();

        if (type.IsGenericType)
        {
            string namePrefix = type.Name.Split(new[] { '`' }, StringSplitOptions.RemoveEmptyEntries)[0];
            string genericParameters = type.GetGenericArguments().Select(GetTypeName).Aggregate((first, second) => $"{first},{second}");
            return $"{namePrefix}<{genericParameters}>";
        }

        return type.Name;
    }
    private static string GetTypeName(Type type)
    {
        if (type.IsGenericType)
        {
            string namePrefix = type.Name.Split(new[] { '`' }, StringSplitOptions.RemoveEmptyEntries)[0];
            string genericParameters = type.GetGenericArguments().Select(GetTypeName).Aggregate((first, second) => $"{first},{second}");
            return $"{namePrefix}<{genericParameters}>";
        }

        return type.Name;
    }
}