using System;
using System.Collections.Generic;
using System.Linq;

namespace UIFixes;

// Small helpers for crossing the managed/il2cpp boundary
public static class Il2CppHelpers
{
    // Managed replacement for Diz.Utils.LinqExtensions.Flatten, which only takes il2cpp delegates
    public static IEnumerable<T> FlattenManaged<T>(this IEnumerable<T> source, Func<T, IEnumerable<T>> children)
    {
        foreach (var item in source)
        {
            yield return item;
            foreach (var child in children(item).FlattenManaged(children))
            {
                yield return child;
            }
        }
    }

    public static Il2CppSystem.Collections.Generic.List<T> Il2CppList<T>(params T[] items)
    {
        var list = new Il2CppSystem.Collections.Generic.List<T>();
        foreach (var item in items)
        {
            list.Add(item);
        }

        return list;
    }

    public static Il2CppSystem.Type Il2CppType(this Type type)
    {
        return Il2CppInterop.Runtime.Il2CppType.From(type);
    }

    public static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<T> ToIl2CppArray<T>(this IEnumerable<T> source) where T : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
    {
        return new(source.ToArray());
    }

    // il2cpp boxes enums (e.g. ItemAttribute.Id) as Il2CppSystem.Enum
    public static Il2CppSystem.Enum BoxEnum<T>(this T value) where T : struct, Enum
    {
        return SPTushonka.Reflection.Il2Cpp.Il2CppConversions.BoxIl2Cpp(value).Cast<Il2CppSystem.Enum>();
    }

    public static T ToEnum<T>(this Il2CppSystem.Enum value) where T : unmanaged, Enum
    {
        return value.Unbox<T>();
    }

    // Interop types have an extra (IntPtr) constructor that wraps an existing il2cpp object
    public static bool IsIl2CppPointerConstructor(this System.Reflection.ConstructorInfo constructor)
    {
        var parameters = constructor.GetParameters();
        return parameters.Length == 1 && parameters[0].ParameterType == typeof(IntPtr);
    }

    // Managed counterpart of the game's TasksExtensions.HandleExceptions, which only accepts il2cpp tasks
    public static void HandleExceptions(this System.Threading.Tasks.Task task)
    {
        task.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                Plugin.Instance.Logger.LogError(t.Exception);
            }
        }, System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
    }

    // The interop generator can't project multi-dimensional arrays, so they come through as a bare Il2CppObjectBase.
    // Read an element straight from the il2cpp array layout: header (object, bounds*, max_length) followed by the data.
    public static unsafe T Get2D<T>(this Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase array, int i, int j) where T : unmanaged
    {
        byte* ptr = (byte*)array.Pointer;
        byte* bounds = *(byte**)(ptr + 2 * IntPtr.Size);

        // Il2CppArrayBounds is { il2cpp_array_size_t length; int32 lower_bound; }, padded to 2 pointers
        int boundsSize = 2 * IntPtr.Size;
        long length1 = *(long*)(bounds + boundsSize);
        int lower0 = *(int*)(bounds + IntPtr.Size);
        int lower1 = *(int*)(bounds + boundsSize + IntPtr.Size);

        T* data = (T*)(ptr + 4 * IntPtr.Size);
        return data[(i - lower0) * length1 + (j - lower1)];
    }
}
