using System.Collections;
using System.Reflection;

namespace StarPlayer.Domain.Tests;

/// <summary>
/// Walks the PUBLIC object graph of a value (public instance properties of StarPlayer types, and the elements of every enumerable) and reports each place where a
/// caller could change shared state: an array, a mutable list, or a mutable dictionary. A wrapper such as <c>ReadOnlyCollection</c> is accepted (it exposes no writer),
/// an <c>ImmutableArray</c> is accepted. This is the "cast it back and write to it" check that the interface type of a property cannot give. Linked into the
/// Infrastructure test project so the tuning profile is audited with the same code.
/// </summary>
internal static class ImmutabilityAudit
{
    public static List<string> Violations(object? root)
    {
        var found = new List<string>();
        Walk(root, root?.GetType().Name ?? "null", found, new HashSet<object>(ReferenceEqualityComparer.Instance));
        return found;
    }

    private static void Walk(object? value, string path, List<string> found, HashSet<object> seen)
    {
        if (value is null || value is string)
            return;
        var type = value.GetType();
        if (type.IsPrimitive || type.IsEnum)
            return;
        if (!type.IsValueType && !seen.Add(value))
            return;

        if (value is Array)
        {
            found.Add($"{path}: array {type.Name}");
            return;
        }

        if (value is IList list && !list.IsReadOnly)
            found.Add($"{path}: mutable list {type.Name}");
        else if (value is IDictionary dictionary && !dictionary.IsReadOnly)
            found.Add($"{path}: mutable dictionary {type.Name}");
        else if (type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>)
                     && (bool)i.GetProperty("IsReadOnly")!.GetValue(value)! == false))
            found.Add($"{path}: mutable collection {type.Name}");

        if (value is IEnumerable enumerable)
        {
            var index = 0;
            foreach (var item in enumerable)
                Walk(item, $"{path}[{index++}]", found, seen);
            return;
        }

        if (type.Namespace is null || !type.Namespace.StartsWith("StarPlayer.", StringComparison.Ordinal))
            return;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0 && p.GetMethod is not null))
            Walk(property.GetValue(value), $"{path}.{property.Name}", found, seen);
    }
}
