using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;

namespace UIFixes;

// Diagnostic for native crashes, which kill the process before BepInEx flushes its log:
// - BepInEx/UIFixes-trace.log gets the first few entries/exits of every traced method, flushed per line
// - BepInEx/UIFixes-trace-ring.log is a memory-mapped ring of the most recent calls. The OS writes mapped pages
//   to disk even if the process dies, so after a crash it shows exactly what UIFixes code ran last.
// Traced methods are every game method UIFixes patches, plus the per-frame methods of UIFixes' injected types.
public static unsafe class PatchTracer
{
    private const int CallsPerMethod = 5;
    private const int RingEntries = 2048;
    private const int EntrySize = 160;
    private const int HeaderSize = EntrySize;

    private static readonly object LockObject = new();
    private static readonly Dictionary<MethodBase, int> EnterCounts = [];
    private static readonly Dictionary<MethodBase, int> ExitCounts = [];
    private static readonly Dictionary<MethodBase, (byte[] enter, byte[] exit)> RingNames = [];
    private static StreamWriter writer;
    private static DateTime start;

    private static MemoryMappedFile ringFile;
    private static MemoryMappedViewAccessor ringView;
    private static byte* ring;
    private static long sequence;

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

            OpenRing(Path.Combine(BepInEx.Paths.BepInExRootPath, "UIFixes-trace-ring.log"));
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
        List<MethodBase> targets = [];

        // Game methods patched by UIFixes
        foreach (MethodBase method in Harmony.GetAllPatchedMethods().ToList())
        {
            var info = Harmony.GetPatchInfo(method);
            if (info != null && info.Prefixes.Concat(info.Postfixes).Concat(info.Finalizers).Any(p => p.PatchMethod.DeclaringType?.Assembly == ours))
            {
                targets.Add(method);
            }
        }

        // UIFixes' own injected types, which the game calls directly (MonoBehaviour messages, overridden virtuals)
        string[] messages = ["Awake", "Start", "Update", "LateUpdate", "OnGUI", "OnEnable", "OnDisable", "OnDestroy"];
        foreach (Type type in ours.GetTypes().Where(t => typeof(Il2CppObjectBase).IsAssignableFrom(t) && !t.IsAbstract && !t.ContainsGenericParameters))
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (messages.Contains(method.Name) || (method.IsVirtual && method.GetBaseDefinition().DeclaringType != type))
                {
                    targets.Add(method);
                }
            }
        }

        int traced = 0;
        foreach (MethodBase method in targets)
        {
            string name = Describe(method);
            RingNames[method] = (Encoding.ASCII.GetBytes(" > " + name), Encoding.ASCII.GetBytes(" < " + name));

            try
            {
                harmony.Patch(method, prefix: prefix, postfix: postfix);
                traced++;
            }
            catch (Exception ex)
            {
                Write($"could not trace {name}: {ex.Message}");
            }
        }

        Write($"tracing {traced} methods");
    }

    private static void OpenRing(string path)
    {
        long size = HeaderSize + (long)RingEntries * EntrySize;
        var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        stream.SetLength(size);
        ringFile = MemoryMappedFile.CreateFromFile(stream, null, size, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false);
        ringView = ringFile.CreateViewAccessor(0, size);

        byte* pointer = null;
        ringView.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
        ring = pointer;

        // Blank lines so the file reads cleanly in a text editor
        for (long i = 0; i < size; i++)
        {
            ring[i] = (byte)((i + 1) % EntrySize == 0 ? '\n' : ' ');
        }

        WriteLine(ring, Encoding.ASCII.GetBytes($"UIFixes {PluginInfo.PLUGIN_VERSION} ring trace. Lines are numbered; sort by number, the highest is the last call."), -1);
    }

    private static void Enter(MethodBase __originalMethod)
    {
        Record(EnterCounts, __originalMethod, ">", true);
    }

    private static void Exit(MethodBase __originalMethod)
    {
        Record(ExitCounts, __originalMethod, "<", false);
    }

    private static void Record(Dictionary<MethodBase, int> counts, MethodBase method, string marker, bool entering)
    {
        if (method == null)
        {
            return;
        }

        try
        {
            if (ring != null && RingNames.TryGetValue(method, out var names))
            {
                long seq = Interlocked.Increment(ref sequence);
                WriteLine(ring + HeaderSize + (seq % RingEntries) * EntrySize, entering ? names.enter : names.exit, seq);
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
        catch
        {
            // Never let tracing break the game
        }
    }

    // Writes "<seq padded to 10> <text>" padded with spaces and ending in a newline, into one fixed-size entry
    private static void WriteLine(byte* entry, byte[] text, long seq)
    {
        int pos = 0;
        if (seq >= 0)
        {
            for (int i = 9; i >= 0; i--)
            {
                entry[i] = (byte)('0' + (seq % 10));
                seq /= 10;
            }

            pos = 10;
        }

        int length = Math.Min(text.Length, EntrySize - 1 - pos);
        for (int i = 0; i < length; i++)
        {
            entry[pos + i] = text[i];
        }

        for (int i = pos + length; i < EntrySize - 1; i++)
        {
            entry[i] = (byte)' ';
        }

        entry[EntrySize - 1] = (byte)'\n';
    }

    private static string Describe(MethodBase method)
    {
        string type = method.DeclaringType?.FullName ?? "?";
        int generic = type.IndexOf("[[", StringComparison.Ordinal);
        if (generic > 0)
        {
            type = type[..generic];
        }

        return $"{type}.{method.Name}";
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
