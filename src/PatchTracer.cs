using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;

namespace UIFixes;

// Diagnostic: records the first few entries/exits of every method UIFixes patches into BepInEx/UIFixes-trace.log,
// flushing each line, so the last lines survive a native crash and point at the patch that was running.
public static class PatchTracer
{
    private const int CallsPerMethod = 5;

    private static readonly object LockObject = new();
    private static readonly Dictionary<MethodBase, int> EnterCounts = [];
    private static readonly Dictionary<MethodBase, int> ExitCounts = [];
    private static StreamWriter writer;
    private static DateTime start;

    public static void Install()
    {
        if (!Settings.TracePatchCalls.Value)
        {
            return;
        }

        try
        {
            string path = Path.Combine(BepInEx.Paths.BepInExRootPath, "UIFixes-trace.log");
            writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
            start = DateTime.Now;
            Write($"UIFixes {PluginInfo.PLUGIN_VERSION} trace started {start:yyyy-MM-dd HH:mm:ss}");
        }
        catch (Exception ex)
        {
            Plugin.Instance.Logger.LogError($"PatchTracer: could not open trace file: {ex}");
            return;
        }

        var harmony = new Harmony("Tyfon.UIFixes.Tracer");
        var prefix = new HarmonyMethod(typeof(PatchTracer).GetMethod(nameof(Enter), BindingFlags.Static | BindingFlags.NonPublic)) { priority = Priority.First };
        var postfix = new HarmonyMethod(typeof(PatchTracer).GetMethod(nameof(Exit), BindingFlags.Static | BindingFlags.NonPublic)) { priority = Priority.Last };

        Assembly ours = typeof(Plugin).Assembly;
        int traced = 0;
        foreach (MethodBase method in Harmony.GetAllPatchedMethods().ToList())
        {
            var info = Harmony.GetPatchInfo(method);
            if (info == null || !info.Prefixes.Concat(info.Postfixes).Concat(info.Finalizers).Any(p => p.PatchMethod.DeclaringType?.Assembly == ours))
            {
                continue;
            }

            try
            {
                harmony.Patch(method, prefix: prefix, postfix: postfix);
                traced++;
            }
            catch (Exception ex)
            {
                Write($"could not trace {Describe(method)}: {ex.Message}");
            }
        }

        Write($"tracing {traced} methods");
    }

    private static void Enter(MethodBase __originalMethod)
    {
        Record(EnterCounts, __originalMethod, ">");
    }

    private static void Exit(MethodBase __originalMethod)
    {
        Record(ExitCounts, __originalMethod, "<");
    }

    private static void Record(Dictionary<MethodBase, int> counts, MethodBase method, string marker)
    {
        if (method == null)
        {
            return;
        }

        lock (LockObject)
        {
            counts.TryGetValue(method, out int count);
            if (count >= CallsPerMethod)
            {
                return;
            }

            counts[method] = count + 1;
            Write($"{marker} {Describe(method)} #{count + 1}");
        }
    }

    private static string Describe(MethodBase method)
    {
        return $"{method.DeclaringType?.FullName}.{method.Name}";
    }

    private static void Write(string line)
    {
        try
        {
            writer?.WriteLine($"[{(DateTime.Now - start).TotalSeconds,9:F3}] {line}");
        }
        catch
        {
            // Never let tracing break the game
        }
    }
}
