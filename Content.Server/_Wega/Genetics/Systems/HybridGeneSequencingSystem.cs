// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Medical.Components;
using Content.Shared.Genetics;
using Content.Trauma.Shared.Genetics.Mutations;
using Robust.Shared.Player;

namespace Content.Server.Genetics.System;

/// <summary>
/// Bridges the Wega genetics console to Trauma's existing MutationData and ScannedGenomeSystem.
/// It owns only the temporary console selection; DNA generation, difficulty and discovery remain
/// owned by the Trauma mutation systems.
/// </summary>
public sealed class HybridGeneSequencingSystem : EntitySystem
{
    [Dependency] private readonly HybridGeneCatalogSystem _catalog = default!;
    [Dependency] private readonly MutationSystem _mutation = default!;
    [Dependency] private readonly ScannedGenomeSystem _scannedGenome = default!;

    private readonly Dictionary<(EntityUid Console, ICommonSession Session), Selection> _selections = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<DnaModifierHybridCatalogRequestEvent>(OnCatalogRequest);
        SubscribeNetworkEvent<DnaModifierHybridSelectGeneEvent>(OnSelectGene);
        SubscribeNetworkEvent<DnaModifierHybridSetBaseEvent>(OnSetBase);
        SubscribeNetworkEvent<DnaModifierHybridResetSequenceEvent>(OnResetSequence);
        SubscribeNetworkEvent<DnaModifierHybridSubmitSequenceEvent>(OnSubmitSequence);
    }

    private void OnCatalogRequest(DnaModifierHybridCatalogRequestEvent args, EntitySessionEventArgs sessionArgs)
    {
        SendState(GetEntity(args.Console), sessionArgs.SenderSession);
    }

    private void OnSelectGene(DnaModifierHybridSelectGeneEvent args, EntitySessionEventArgs sessionArgs)
    {
        var console = GetEntity(args.Console);
        var session = sessionArgs.SenderSession;
        var key = (console, session);

        if (!TryGetScannedBody(console, out var body)
            || !TryResolveMutation(args.MutationId, out var mutationId)
            || _mutation.GetRoundData(mutationId) is not { } data)
        {
            _selections.Remove(key);
            SendState(console, session);
            return;
        }

        if (data.Discovered)
        {
            _selections.Remove(key);
            SendState(console, session);
            return;
        }

        if (!PrepareGenome(body) || !TryFindSequence(body, mutationId, out var sequenceIndex, out _))
        {
            _selections.Remove(key);
            SendState(console, session);
            return;
        }

        _selections[key] = new Selection(body, mutationId, sequenceIndex);
        SendState(console, session);
    }

    private void OnSetBase(DnaModifierHybridSetBaseEvent args, EntitySessionEventArgs sessionArgs)
    {
        var console = GetEntity(args.Console);
        var session = sessionArgs.SenderSession;
        if (args.Base.Length != 1 || !"XATGC".Contains(args.Base[0]))
            return;

        if (!TryGetSelection(console, session, out var selection, out var sequence))
        {
            SendState(console, session);
            return;
        }

        var index = (int) args.Index;
        if (index < 0
            || index >= sequence.Bases.Length
            || index >= sequence.OriginalBases.Length
            || sequence.OriginalBases[index] != 'X')
        {
            return;
        }

        var bases = sequence.Bases.ToCharArray();
        bases[index] = args.Base[0];
        sequence.Bases = new string(bases);
        selection.LastAttemptFailed = false;
        SendState(console, session);
    }

    private void OnResetSequence(DnaModifierHybridResetSequenceEvent args, EntitySessionEventArgs sessionArgs)
    {
        var console = GetEntity(args.Console);
        var session = sessionArgs.SenderSession;
        if (!TryGetSelection(console, session, out var selection, out var sequence))
        {
            SendState(console, session);
            return;
        }

        sequence.Bases = sequence.OriginalBases;
        selection.LastAttemptFailed = false;
        SendState(console, session);
    }

    private void OnSubmitSequence(DnaModifierHybridSubmitSequenceEvent args, EntitySessionEventArgs sessionArgs)
    {
        var console = GetEntity(args.Console);
        var session = sessionArgs.SenderSession;
        var key = (console, session);

        if (!TryGetSelection(console, session, out var selection, out var sequence)
            || _mutation.GetRoundData(selection.Mutation) is not { } data)
        {
            SendState(console, session);
            return;
        }

        // Discovery remains the real per-round MutationData state. No parallel Wega discovery flag
        // is created, and successful sequencing does not activate the mutation on the organism.
        if (data.Discovered)
        {
            _selections.Remove(key);
            SendState(console, session);
            return;
        }

        if (sequence.Bases != data.Bases)
        {
            selection.LastAttemptFailed = true;
            SendState(console, session);
            return;
        }

        data.Discovered = true;
        _selections.Remove(key);
        SendState(console, session);
    }

    private bool TryGetSelection(
        EntityUid console,
        ICommonSession session,
        out Selection selection,
        out Sequence sequence)
    {
        sequence = default!;
        var key = (console, session);
        if (!_selections.TryGetValue(key, out var found))
        {
            selection = default!;
            return false;
        }

        selection = found;
        if (!TryGetScannedBody(console, out var body) || body != selection.Body)
        {
            _selections.Remove(key);
            return false;
        }

        var current = _scannedGenome.GetSequence(body, selection.SequenceIndex);
        if (current == null || current.Mutation != selection.Mutation)
        {
            _selections.Remove(key);
            return false;
        }

        sequence = current;
        return true;
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

    private bool PrepareGenome(EntityUid body)
    {
        if (!_mutation.IsMutatable(body))
            return false;

        _scannedGenome.ScanGenome(body);
        return true;
    }

    private bool TryResolveMutation(string id, out EntProtoId<MutationComponent> mutationId)
    {
        foreach (var candidate in _mutation.AllMutations.Keys)
        {
            if (!string.Equals(candidate.ToString(), id, StringComparison.Ordinal))
                continue;

            mutationId = candidate;
            return true;
        }

        mutationId = default;
        return false;
    }

    private bool TryFindSequence(
        EntityUid body,
        EntProtoId<MutationComponent> mutationId,
        out uint sequenceIndex,
        out Sequence sequence)
    {
        for (uint index = 0; ; index++)
        {
            var current = _scannedGenome.GetSequence(body, index);
            if (current == null)
                break;

            if (current.Mutation != mutationId)
                continue;

            sequenceIndex = index;
            sequence = current;
            return true;
        }

        sequenceIndex = 0;
        sequence = default!;
        return false;
    }

    private void SendState(EntityUid console, ICommonSession session)
    {
        EntityUid? body = null;
        if (TryGetScannedBody(console, out var scannedBody) && PrepareGenome(scannedBody))
            body = scannedBody;

        var catalog = _catalog.BuildCatalog(body);
        string? mutationId = null;
        string? bases = null;
        string? originalBases = null;
        var failed = false;
        var key = (console, session);

        if (_selections.TryGetValue(key, out var selection))
        {
            if (body != selection.Body
                || _mutation.GetRoundData(selection.Mutation)?.Discovered == true)
            {
                _selections.Remove(key);
            }
            else
            {
                // Reuse Trauma's client-facing SequenceState projection instead of creating a
                // second DNA model for the Wega UI.
                var states = new List<SequenceState>();
                _scannedGenome.AddSequenceStates(selection.Body, states);
                if (selection.SequenceIndex < (uint) states.Count)
                {
                    var state = states[(int) selection.SequenceIndex];
                    mutationId = selection.Mutation.ToString();
                    bases = state.Bases;
                    originalBases = state.OriginalBases;
                    failed = selection.LastAttemptFailed;
                }
                else
                {
                    _selections.Remove(key);
                }
            }
        }

        RaiseNetworkEvent(new DnaModifierHybridSequencingStateEvent(
            GetNetEntity(console),
            catalog,
            mutationId,
            bases,
            originalBases,
            failed), session);
    }

    private sealed class Selection
    {
        public readonly EntityUid Body;
        public readonly EntProtoId<MutationComponent> Mutation;
        public readonly uint SequenceIndex;
        public bool LastAttemptFailed;

        public Selection(EntityUid body, EntProtoId<MutationComponent> mutation, uint sequenceIndex)
        {
            Body = body;
            Mutation = mutation;
            SequenceIndex = sequenceIndex;
        }
    }
}
