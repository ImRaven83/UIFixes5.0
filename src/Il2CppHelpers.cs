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
}
