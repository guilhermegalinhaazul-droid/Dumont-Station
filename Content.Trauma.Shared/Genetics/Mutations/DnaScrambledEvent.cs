// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Trauma.Shared.Genetics.Mutations;

/// <summary>
/// Minimal compatibility event matching the imported Trauma genetics scanner
/// and mutation pipeline. The imported hook only needs a no-payload event to
/// keep the Wega scanner path structurally connected to the mutation engine.
/// </summary>
[ByRefEvent]
public record struct DnaScrambledEvent();
