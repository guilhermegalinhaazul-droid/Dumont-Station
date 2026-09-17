// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Genetics;

/// <summary>
/// Provides the canonical identity used to merge Wega structural enzymes and Trauma mutations
/// into a single catalog entry. Explicit aliases can be added here when equivalent genes use
/// different prototype IDs; otherwise the prototype ID itself is the canonical identity.
/// </summary>
public static class GeneCanonicalKeys
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase);

    public static string Get(string geneId)
    {
        if (Aliases.TryGetValue(geneId, out var canonical))
            return canonical;

        return geneId;
    }
}
