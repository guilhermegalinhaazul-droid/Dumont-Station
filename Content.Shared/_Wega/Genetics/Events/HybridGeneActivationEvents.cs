// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared.Genetics;

[Serializable, NetSerializable]
public sealed class DnaModifierHybridSetGeneActiveEvent : EntityEventArgs
{
    public NetEntity Console { get; }
    public string CanonicalKey { get; }
    public bool Active { get; }

    public DnaModifierHybridSetGeneActiveEvent(NetEntity console, string canonicalKey, bool active)
    {
        Console = console;
        CanonicalKey = canonicalKey;
        Active = active;
    }
}

[Serializable, NetSerializable]
public sealed class DnaModifierHybridActivationResultEvent : EntityEventArgs
{
    public NetEntity Console { get; }
    public bool Success { get; }
    public string Message { get; }

    public DnaModifierHybridActivationResultEvent(NetEntity console, bool success, string message)
    {
        Console = console;
        Success = success;
        Message = message;
    }
}
