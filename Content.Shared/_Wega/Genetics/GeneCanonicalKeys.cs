// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Genetics;

/// <summary>
/// Resolves the prototype-provided canonical identity used to fold equivalent Wega genes and
/// Trauma mutations into one catalog entry. Equivalence data belongs to prototypes, not code.
/// </summary>
public static class GeneCanonicalKeys
{
    public static string Get(string geneId, string? canonicalKey = null)
        => string.IsNullOrWhiteSpace(canonicalKey) ? geneId : canonicalKey;
}
