// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared.Genetics;

[Serializable, NetSerializable]
public sealed class DnaModifierEiCopyRequestEvent : EntityEventArgs
{
    public NetEntity Console { get; }
    public int BufferIndex { get; }

    public DnaModifierEiCopyRequestEvent(NetEntity console, int bufferIndex)
    {
        Console = console;
        BufferIndex = bufferIndex;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierEiApplyRequestEvent : EntityEventArgs
{
    public NetEntity Console { get; }
    public int BufferIndex { get; }

    public DnaModifierEiApplyRequestEvent(NetEntity console, int bufferIndex)
    {
        Console = console;
        BufferIndex = bufferIndex;
    }
}
