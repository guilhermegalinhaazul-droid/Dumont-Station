// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Genetics.UI;
using Robust.Shared.Serialization;

namespace Content.Shared.Genetics;

[Serializable, NetSerializable]
public sealed class DnaModifierHybridCatalogRequestEvent : EntityEventArgs
{
    public NetEntity Console { get; }

    public DnaModifierHybridCatalogRequestEvent(NetEntity console)
    {
        Console = console;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierHybridSelectGeneEvent : EntityEventArgs
{
    public NetEntity Console { get; }
    public string MutationId { get; }

    public DnaModifierHybridSelectGeneEvent(NetEntity console, string mutationId)
    {
        Console = console;
        MutationId = mutationId;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierHybridSetBaseEvent : EntityEventArgs
{
    public NetEntity Console { get; }
    public uint Index { get; }
    public string Base { get; }

    public DnaModifierHybridSetBaseEvent(NetEntity console, uint index, string @base)
    {
        Console = console;
        Index = index;
        Base = @base;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierHybridResetSequenceEvent : EntityEventArgs
{
    public NetEntity Console { get; }

    public DnaModifierHybridResetSequenceEvent(NetEntity console)
    {
        Console = console;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierHybridSubmitSequenceEvent : EntityEventArgs
{
    public NetEntity Console { get; }

    public DnaModifierHybridSubmitSequenceEvent(NetEntity console)
    {
        Console = console;
    }
}

/// <summary>
/// Minimal transport state for the Wega UI. The actual mutation DNA remains owned by
/// MutationData/ScannedGenomeSystem; this event only mirrors the selected sequence for display.
/// </summary>
[Serializable, NetSerializable]
public sealed class DnaModifierHybridSequencingStateEvent : EntityEventArgs
{
    public NetEntity Console { get; }
    public List<GeneCatalogEntry> Catalog { get; }
    public string? MutationId { get; }
    public string? Bases { get; }
    public string? OriginalBases { get; }
    public bool LastAttemptFailed { get; }

    public DnaModifierHybridSequencingStateEvent(
        NetEntity console,
        List<GeneCatalogEntry> catalog,
        string? mutationId,
        string? bases,
        string? originalBases,
        bool lastAttemptFailed)
    {
        Console = console;
        Catalog = catalog;
        MutationId = mutationId;
        Bases = bases;
        OriginalBases = originalBases;
        LastAttemptFailed = lastAttemptFailed;
    }
}
