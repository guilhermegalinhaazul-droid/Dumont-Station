// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Medical.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Genetics;
using Content.Shared.Genetics.UI;
using Content.Trauma.Shared.Genetics.Mutations;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Genetics.System;

/// <summary>
/// Server-authoritative activation/deactivation adapter for the unified catalog.
/// It delegates state changes to Wega DnaModifierSystem or Trauma MutationSystem.
/// </summary>
public sealed class HybridGeneActivationSystem : EntitySystem
{
    [Dependency] private readonly DnaModifierSystem _dnaModifier = default!;
    [Dependency] private readonly HybridGeneCatalogSystem _catalog = default!;
    [Dependency] private readonly MutationSystem _mutation = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<DnaModifierHybridSetGeneActiveEvent>(OnSetActive);
    }

    private void OnSetActive(DnaModifierHybridSetGeneActiveEvent args, EntitySessionEventArgs sessionArgs)
    {
        var console = GetEntity(args.Console);
        var session = sessionArgs.SenderSession;

        if (!IsAuthorized(console, session)
            || !TryGetScannedBody(console, out var body)
            || session.AttachedEntity is not { } user)
        {
            return;
        }

        var entry = _catalog.FindByCanonicalKey(body, args.CanonicalKey);
        if (entry == null)
        {
            SendResult(console, session, false, "Gene não encontrado no catálogo atual.");
            return;
        }

        if (args.Active && (!entry.Discovered || !entry.Available))
        {
            SendResult(console, session, false, "O gene precisa estar descoberto e disponível antes de ser ativado.");
            return;
        }

        var changed = args.Active
            ? Activate(body, user, entry, out var message)
            : Deactivate(body, user, entry, out message);

        // Evaluate the final catalog state rather than trusting a local boolean from either
        // genetics engine. This keeps canonical Wega+Trauma rows consistent.
        var finalEntry = _catalog.FindByCanonicalKey(body, args.CanonicalKey);
        var reachedState = finalEntry != null && finalEntry.Active == args.Active;

        SendResult(console, session, changed && reachedState, reachedState ? message : "A operação não pôde atingir o estado solicitado.");
    }

    private bool Activate(EntityUid body, EntityUid user, GeneCatalogEntry entry, out string message)
    {
        // Wega is the canonical implementation when an equivalent Wega gene exists.
        if (entry.WegaGeneId is { } wegaId)
        {
            var ok = _dnaModifier.TrySetStructuralGeneActive(body, wegaId, true);
            message = ok ? "Gene Wega ativado." : "A assimilação do gene Wega falhou ou o gene não pode ser ativado.";
            return ok;
        }

        if (entry.TraumaMutationId is not { } traumaId
            || !TryResolveMutation(traumaId, out var id, out var mutation)
            || _mutation.GetMutatable(body, force: false) is not { } mutatable)
        {
            message = "Mutação Trauma indisponível para este organismo.";
            return false;
        }

        if (mutation.Locked && !mutatable.Comp.Dormant.Contains(id))
        {
            message = "Esta mutação é bloqueada e não pode ser ativada diretamente.";
            return false;
        }

        var ok = mutatable.Comp.Dormant.Contains(id)
            ? _mutation.ActivateMutation(mutatable.AsNullable(), id, user: user, predicted: false)
            : _mutation.AddMutation(mutatable.AsNullable(), id, user: user, predicted: false);

        message = ok
            ? "Mutação ativada."
            : "A mutação não pôde ser ativada; verifique requisitos, conflitos e instabilidade.";
        return ok;
    }

    private bool Deactivate(EntityUid body, EntityUid user, GeneCatalogEntry entry, out string message)
    {
        var attempted = false;

        if (entry.WegaGeneId is { } wegaId && _dnaModifier.IsStructuralEnzymeActive(body, wegaId))
        {
            attempted = true;
            _dnaModifier.TrySetStructuralGeneActive(body, wegaId, false);
        }

        if (entry.TraumaMutationId is { } traumaId
            && TryResolveMutation(traumaId, out var id, out _)
            && _mutation.GetMutatable(body, force: false) is { } mutatable
            && _mutation.HasMutation(mutatable.AsNullable(), id))
        {
            attempted = true;
            _mutation.RemoveMutation(mutatable.AsNullable(), id, user: user, predicted: false);
        }

        message = attempted
            ? "Gene desativado."
            : "O gene não estava ativo ou não pode ser removido por uma dependência genética.";
        return attempted;
    }

    private bool TryResolveMutation(
        string id,
        out EntProtoId<MutationComponent> mutationId,
        out MutationComponent mutation)
    {
        foreach (var (candidate, component) in _mutation.AllMutations)
        {
            if (!string.Equals(candidate.ToString(), id, StringComparison.Ordinal))
                continue;

            mutationId = candidate;
            mutation = component;
            return true;
        }

        mutationId = default;
        mutation = default!;
        return false;
    }

    private bool TryGetScannedBody(EntityUid console, out EntityUid body)
    {
        body = default;
        if (!TryComp<DnaModifierConsoleComponent>(console, out var consoleComp)
            || !consoleComp.GeneticScannerInRange
            || consoleComp.GeneticScanner is not { } scannerUid
            || !TryComp<MedicalScannerComponent>(scannerUid, out var scanner)
            || scanner.BodyContainer.ContainedEntity is not { } scannedBody)
        {
            return false;
        }

        body = scannedBody;
        return true;
    }

    private bool IsAuthorized(EntityUid console, ICommonSession session)
        => _power.IsPowered(console)
           && session.AttachedEntity is { } actor
           && TryComp<DnaModifierConsoleComponent>(console, out _)
           && _ui.IsUiOpen(console, DnaModifierUiKey.Key, actor);

    private void SendResult(EntityUid console, ICommonSession session, bool success, string message)
        => RaiseNetworkEvent(
            new DnaModifierHybridActivationResultEvent(GetNetEntity(console), success, message),
            session);
}
