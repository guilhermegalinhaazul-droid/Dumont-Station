// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Medical.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Genetics;
using Content.Shared.Genetics.Systems;
using Content.Shared.Genetics.UI;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Genetics.System;

/// <summary>
/// Server-authoritative controller for EI capture/application in the existing Wega genetics PC.
/// Storage remains owned by the existing DnaServer/DnaClient buffer and disk systems.
/// </summary>
public sealed class DnaModifierEiSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly DnaClientSystem _dnaClient = default!;
    [Dependency] private readonly DnaModifierConsoleSystem _consoleSystem = default!;
    [Dependency] private readonly DnaModifierSystem _dnaModifier = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    private static readonly ProtoId<DamageTypePrototype> RadDamage = "Radiation";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<DnaModifierEiCopyRequestEvent>(OnCopyRequest);
        SubscribeNetworkEvent<DnaModifierEiApplyRequestEvent>(OnApplyRequest);
    }

    private void OnCopyRequest(DnaModifierEiCopyRequestEvent args, EntitySessionEventArgs sessionArgs)
    {
        var console = GetEntity(args.Console);
        if (!IsAuthorized(console, sessionArgs.SenderSession)
            || !ValidBuffer(args.BufferIndex)
            || !TryComp<DnaModifierConsoleComponent>(console, out var consoleComp)
            || !TryComp<DnaClientComponent>(console, out var client)
            || !TryGetScannerBody(consoleComp, out var donor)
            || !TryComp<DnaModifierComponent>(donor, out var donorDna))
        {
            return;
        }

        // Never overwrite an existing buffer implicitly.
        if (_dnaClient.TryGetBufferData((console, client), args.BufferIndex, out _))
            return;

        var profile = _dnaModifier.CaptureGeneticProfile((donor, donorDna));
        if (!_dnaClient.TryAddToBuffer((console, client), args.BufferIndex, profile))
            return;

        _consoleSystem.UpdateUserInterface(console, consoleComp);
    }

    private void OnApplyRequest(DnaModifierEiApplyRequestEvent args, EntitySessionEventArgs sessionArgs)
    {
        var console = GetEntity(args.Console);
        if (!IsAuthorized(console, sessionArgs.SenderSession)
            || !ValidBuffer(args.BufferIndex)
            || !TryComp<DnaModifierConsoleComponent>(console, out var consoleComp)
            || !TryComp<DnaClientComponent>(console, out var client)
            || !TryGetScannerBody(consoleComp, out var receiver)
            || !TryComp<DnaModifierComponent>(receiver, out var receiverDna)
            || !_dnaClient.TryGetBufferData((console, client), args.BufferIndex, out var profile)
            || !profile.IsFullGeneticProfile)
        {
            return;
        }

        if (!_dnaModifier.ApplyGeneticProfile((receiver, receiverDna), profile, sessionArgs.SenderSession.AttachedEntity))
            return;

        // Preserve the existing Wega direct-subject injection cost.
        var radiation = new DamageSpecifier
        {
            DamageDict = { { RadDamage, 20 } }
        };
        _damage.TryChangeDamage(receiver, radiation, true);

        _consoleSystem.UpdateUserInterface(console, consoleComp);
    }

    private bool IsAuthorized(EntityUid console, ICommonSession session)
    {
        return _power.IsPowered(console)
            && session.AttachedEntity is { } actor
            && TryComp<DnaModifierConsoleComponent>(console, out var component)
            && component.GeneticScannerInRange
            && _ui.IsUiOpen(console, DnaModifierUiKey.Key, actor);
    }

    private bool TryGetScannerBody(DnaModifierConsoleComponent console, out EntityUid body)
    {
        body = default;

        if (console.GeneticScanner is not { } scannerUid
            || !console.GeneticScannerInRange
            || !TryComp<MedicalScannerComponent>(scannerUid, out var scanner)
            || scanner.BodyContainer.ContainedEntity is not { } contained)
        {
            return false;
        }

        body = contained;
        return true;
    }

    private static bool ValidBuffer(int index)
        => index is >= 1 and <= 3;
}
