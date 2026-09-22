// SPDX-License-Identifier: AGPL-3.0-or-later
using System.Linq;
using System.Reflection;

namespace Content.Shared.Genetics;

/// <summary>
/// Describes the existing Wega unique identifier fields; there is no second appearance store.
/// Colors remain encoded only at the legacy sample boundary, never in the player interface.
/// </summary>
public static class AppearanceGene
{
    public const string Name = nameof(UniqueIdentifiersData.EntityName);
    private static readonly Dictionary<string, PropertyInfo> Properties = typeof(UniqueIdentifiersData)
        .GetProperties().Where(p => p.PropertyType == typeof(string[])).ToDictionary(p => p.Name);

    public static IEnumerable<string> Fields => Properties.Keys.Append(Name);
    public static bool Exists(string field) => field == Name || Properties.ContainsKey(field);
    public static string[]? Get(UniqueIdentifiersData data, string field)
        => Properties.TryGetValue(field, out var property) ? (string[]?) property.GetValue(data) : null;
    public static void Set(UniqueIdentifiersData data, string field, string[] value)
    {
        if (Properties.TryGetValue(field, out var property))
            property.SetValue(data, (string[]) value.Clone());
    }
    public static bool Copy(UniqueIdentifiersData source, UniqueIdentifiersData target, string field)
    {
        if (field == Name)
        {
            target.EntityName = source.EntityName;
            return true;
        }
        if (Get(source, field) is not { } value)
            return false;
        Set(target, field, value);
        return true;
    }
}
