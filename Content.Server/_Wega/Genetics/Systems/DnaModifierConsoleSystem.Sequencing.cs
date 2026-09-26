// SPDX-License-Identifier: AGPL-3.0-or-later
// Sequencing, round discovery and recipes adapted from Trauma Station genetics.
using System.Linq;
using Content.Server.Medical.Components;
using Content.Server.Radio.EntitySystems;
using Content.Server.Research.Systems;
using Content.Shared.Damage;
using Content.Shared.GameTicking;
using Content.Shared.Genetics;
using Content.Shared.Genetics.Systems;
using Content.Shared.Genetics.UI;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Research.Components;

namespace Content.Server.Genetics.System;

public sealed partial class DnaModifierConsoleSystem
{
    [Dependency] private readonly StructuralEnzymesIndexerSystem _geneIndex = default!;
    [Dependency] private readonly MarkingPrototypesIndexerSystem _appearanceIndex = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly RadioSystem _radio = default!;

    // Answers and undiscovered identities stay on the server.
    private readonly Dictionary<string, string> _geneAnswers = new();
    private readonly HashSet<string> _discoveredGenes = new();
    private readonly Dictionary<(EntityUid, string), string> _geneClues = new();
    private readonly Dictionary<EntityUid, PendingSequence> _pendingSequences = new();
    private readonly Dictionary<EntityUid, string> _geneticStatus = new();
    private int _sequenceToken;

    private sealed class PendingSequence
    {
        public required EntityUid Subject;
        public required EntityUid User;
        public required GeneticPuzzleState State;
        public required string Answer;
        public List<EnzymesPrototypeInfo>? Structural;
        public string? Gene;
        public string? Field;
        public UniqueIdentifiersData? Appearance;
    }

    private void InitializeSequencing()
    {
        SubscribeLocalEvent<DnaModifierConsoleComponent, GeneticSelectMessage>(OnSelectGene);
        SubscribeLocalEvent<DnaModifierConsoleComponent, GeneticToggleMessage>(OnToggleGene);
        SubscribeLocalEvent<DnaModifierConsoleComponent, GeneticSubmitMessage>(OnSubmitSequence);
        SubscribeLocalEvent<DnaModifierConsoleComponent, GeneticAppearanceMessage>(OnSelectAppearance);
        SubscribeLocalEvent<DnaModifierConsoleComponent, GeneticBufferAppearanceMessage>(OnSelectBufferAppearance);
        SubscribeLocalEvent<DnaModifierConsoleComponent, GeneticCombineMessage>(OnCombineGenes);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _geneAnswers.Clear();
            _geneClues.Clear();
            _discoveredGenes.Clear();
            _pendingSequences.Clear();
            _geneticStatus.Clear();
        });
    }

    private bool TrySubject(EntityUid console, DnaModifierConsoleComponent component,
        out Entity<DnaModifierComponent> subject)
    {
        subject = default;
        if (!_powerReceiverSystem.IsPowered(console) ||
            component.GeneticScanner is not { } scannerId ||
            !TryComp<MedicalScannerComponent>(scannerId, out var scanner) ||
            !Transform(scannerId).Coordinates.TryDistance(EntityManager, Transform(console).Coordinates, out var distance) ||
            distance > component.MaxDistance ||
            scanner.BodyContainer.ContainedEntity is not { } body ||
            !TryComp<DnaModifierComponent>(body, out var dna))
            return false;
        subject = (body, dna);
        return true;
    }

    private EnzymesPrototypeInfo? FindGene(Entity<DnaModifierComponent> subject, int number)
        => subject.Comp.EnzymesPrototypes?.FirstOrDefault(g => g.Order == number);

    private bool IsDiscovered(string id) => _discoveredGenes.Contains(id);
    // Structural enzyme prototypes use Trauma's original hidden-position
    // difficulty. Active genes get the facilitated three-to-four-gap puzzle.
    private int Difficulty(string id) => _prototypeManager.TryIndex<StructuralEnzymesPrototype>(id, out var proto)
        ? Math.Clamp(proto.Difficulty, 2, GeneticSequence.StructuralPairs * 2)
        : GeneticSequence.StructuralPairs;
    private string GeneName(string id)
    {
        if (id == StructuralEnzymesIndexerSystem.SpeciesGene)
            return Loc.GetString("dna-gene-species");
        if (!_prototypeManager.TryIndex<StructuralEnzymesPrototype>(id, out var proto))
            return id;
        return !string.IsNullOrEmpty(proto.Abbreviation) ? proto.Abbreviation :
            !string.IsNullOrEmpty(proto.Name) ? proto.Name : id;
    }

    private void PopulateSequencingState(Entity<DnaModifierConsoleComponent> console, DnaModifierBoundUserInterfaceState state)
    {
        state.GeneticStatus = _geneticStatus.GetValueOrDefault(console, string.Empty);
        if (!TrySubject(console, console.Comp, out var subject))
        {
            _pendingSequences.Remove(console);
            return;
        }
        foreach (var gene in subject.Comp.EnzymesPrototypes ?? new())
        {
            var known = IsDiscovered(gene.EnzymesPrototypeId);
            state.Genes.Add(new GeneticGeneState
            {
                Number = gene.Order,
                Name = known ? GeneName(gene.EnzymesPrototypeId) : "???",
                Discovered = known,
                Active = _dnaModifier.IsGeneActive(subject, gene)
            });
        }
        if (_pendingSequences.TryGetValue(console, out var pending))
        {
            if (pending.Subject == subject.Owner)
                state.Puzzle = pending.State;
            else
                _pendingSequences.Remove(console);
        }
        if (subject.Comp.UniqueIdentifiers is not { } appearance)
            return;
        state.AppearanceFields = AppearanceGene.Fields.Where(f => f == AppearanceGene.Name ||
            AppearanceGene.Get(appearance, f)?.Length == 3).ToList();
        foreach (var field in state.AppearanceFields.Where(f => f.EndsWith("Style")))
            state.AppearanceOptions[field] = GetMarkingOptions(subject, field).Select(m => m.MarkingPrototypeId).Prepend(string.Empty).ToList();
    }

    private void OnSelectGene(EntityUid uid, DnaModifierConsoleComponent component, GeneticSelectMessage args)
    {
        if (!TrySubject(uid, component, out var subject) || FindGene(subject, args.Number) is not { } gene || IsDiscovered(gene.EnzymesPrototypeId))
            return;
        var id = gene.EnzymesPrototypeId;
        if (!_geneAnswers.TryGetValue(id, out var answer))
            _geneAnswers[id] = answer = GeneticSequence.Generate(_random, GeneticSequence.StructuralPairs);
        if (!_geneClues.TryGetValue((subject, id), out var clue))
        {
            clue = gene.Active
                ? GeneticSequence.HideStructuralFacilitated(_random, answer)
                : GeneticSequence.HideStructural(_random, answer, Difficulty(id));
            _geneClues[(subject, id)] = clue;
        }
        _pendingSequences[uid] = new PendingSequence
        {
            Subject = subject, User = args.Actor, Gene = id, Answer = answer,
            State = new GeneticPuzzleState { Token = ++_sequenceToken, Title = $"{gene.Order} — ???", Original = clue }
        };
        UpdateUserInterface(uid, component);
    }

    private void OnToggleGene(EntityUid uid, DnaModifierConsoleComponent component, GeneticToggleMessage args)
    {
        if (_timing.CurTime < component.LastGeneToggleTime + TimeSpan.FromSeconds(5))
            return;
        if (!TrySubject(uid, component, out var subject) || FindGene(subject, args.Number) is not { } gene || !IsDiscovered(gene.EnzymesPrototypeId))
            return;
        component.LastGeneToggleTime = _timing.CurTime;
        _dnaModifier.SetGeneActive(subject, gene, !_dnaModifier.IsGeneActive(subject, gene));
        UpdateUserInterface(uid, component);
    }

    private void OnSubmitSequence(EntityUid uid, DnaModifierConsoleComponent component, GeneticSubmitMessage args)
    {
        if (!TrySubject(uid, component, out var subject) ||
            !_pendingSequences.TryGetValue(uid, out var pending) || pending.Subject != subject.Owner ||
            pending.User != args.Actor || pending.State.Token != args.Token)
            return;
        // Consume attempts before applying damage, rewards or polymorphing. No replay rewards.
        _pendingSequences.Remove(uid);
        if (!GeneticSequence.Validate(pending.State.Original, args.Answer, pending.Answer))
        {
            Spawn("GeneticSequenceSmoke", Transform(uid).Coordinates);
            if (pending.State.Appearance)
                _damage.TryChangeDamage(subject, new DamageSpecifier { DamageDict = { { "Poison", 2 } } },
                    ignoreResistances: true, canBeCancelled: false, ignoreBlockers: true, canMiss: false);
            _geneticStatus[uid] = Loc.GetString("dna-sequence-failed");
            UpdateUserInterface(uid, component);
            return;
        }
        if (pending.Appearance is { } appearance && subject.Comp.UniqueIdentifiers is { } current)
        {
            // Merge only the chosen field, so another edit never gets reverted by a stale snapshot.
            if (pending.Field is { } field)
                AppearanceGene.Copy(appearance, current, field);
            else
                subject.Comp.UniqueIdentifiers = appearance.Clone(appearance);
            _dnaModifier.ChangeDna(subject, 0);
            if (pending.Structural != null)
                _dnaModifier.ChangeDna(subject, new EnzymeInfo { Info = pending.Structural });
            component.LastSubjectInjectTime = _timing.CurTime;
            // Editing a live subject's appearance is intentionally harmful.
            // The cost is applied once per successfully solved appearance puzzle.
            _damage.TryChangeDamage(subject, new DamageSpecifier
            {
                DamageDict = { { "Poison", 2.5 }, { "Genetic", 0.2 } }
            }, ignoreResistances: true, canBeCancelled: false, ignoreBlockers: true, canMiss: false);
            Dirty(subject);
        }
        else if (pending.Gene is { } id && _discoveredGenes.Add(id))
        {
            if (TryComp<ResearchClientComponent>(uid, out var client) && client.Server is { } server)
            {
                var points = Difficulty(id) * 500;
                _research.ModifyServerPoints(server, points);
                _popup.PopupEntity(Loc.GetString("dna-sequence-research-reward", ("points", points)), uid, pending.User);
                _radio.SendRadioMessage(uid,
                    Loc.GetString("dna-sequence-research-radio", ("gene", GeneName(id)), ("points", points)),
                    "Science", uid, escapeMarkup: false);
            }
            var gene = subject.Comp.EnzymesPrototypes?.FirstOrDefault(g => g.EnzymesPrototypeId == id);
            if (gene != null)
                _dnaModifier.SetGeneActive(subject, gene, true);
        }
        _geneticStatus[uid] = Loc.GetString("dna-sequence-success");
        UpdateUserInterface(uid, component);
    }

    private IEnumerable<MarkingPrototypeInfo> GetMarkingOptions(EntityUid subject, string field)
    {
        if (!TryComp<HumanoidAppearanceComponent>(subject, out var appearance))
            return Array.Empty<MarkingPrototypeInfo>();
        var category = field switch
        {
            nameof(UniqueIdentifiersData.HairStyle) => MarkingCategories.Hair,
            nameof(UniqueIdentifiersData.BeardStyle) => MarkingCategories.FacialHair,
            nameof(UniqueIdentifiersData.HeadAccessoryStyle) => MarkingCategories.HeadTop,
            nameof(UniqueIdentifiersData.HeadMarkingStyle) => MarkingCategories.Head,
            nameof(UniqueIdentifiersData.BodyMarkingStyle) => MarkingCategories.Chest,
            nameof(UniqueIdentifiersData.TailMarkingStyle) => MarkingCategories.Tail,
            _ => MarkingCategories.Special
        };
        return _appearanceIndex.GetAllMarkingPrototypes().Where(m =>
        {
            var proto = _prototypeManager.Index<MarkingPrototype>(m.MarkingPrototypeId);
            return proto.MarkingCategory == category &&
                (proto.SpeciesRestrictions == null || proto.SpeciesRestrictions.Contains(appearance.Species));
        });
    }

    private void OnSelectAppearance(EntityUid uid, DnaModifierConsoleComponent component, GeneticAppearanceMessage args)
    {
        if (!TrySubject(uid, component, out var subject) || subject.Comp.UniqueIdentifiers is not { } current || !AppearanceGene.Exists(args.Field))
            return;
        var selected = current.Clone(current);
        if (args.Field == AppearanceGene.Name)
        {
            var name = args.Value.Trim();
            if (name.Length is < 1 or > 64 || name.Any(char.IsControl))
                return;
            selected.EntityName = name;
        }
        else if (args.Field.EndsWith("Style"))
        {
            var marking = GetMarkingOptions(subject, args.Field).FirstOrDefault(m => m.MarkingPrototypeId == args.Value);
            if (marking == null && args.Value.Length != 0)
                return;
            AppearanceGene.Set(selected, args.Field, marking?.HexValue ?? new[] { "0", "0", "0" });
        }
        else
        {
            if (!int.TryParse(args.Value, out var value))
                return;
            string encoded;
            if (args.Field == nameof(UniqueIdentifiersData.Gender))
            {
                if (value is < 0 or > 2) return;
                encoded = value switch { 0 => "573", 1 => "677", _ => "879" };
            }
            else if (args.Field == nameof(UniqueIdentifiersData.SkinTone))
            {
                if (value is < 0 or > 100) return;
                encoded = value == 100 ? "100" : "0" + value.ToString("D2");
            }
            else
            {
                if (value is < 0 or > 255) return;
                encoded = value.ToString("X2") + "0";
            }
            AppearanceGene.Set(selected, args.Field, encoded.Select(c => c.ToString()).ToArray());
        }
        BeginAppearance(uid, component, subject, args.Actor, selected, args.Field);
    }

    private void BeginAppearance(EntityUid uid, DnaModifierConsoleComponent component, EntityUid subject,
        EntityUid user, UniqueIdentifiersData selected, string? field)
    {
        var answer = GeneticSequence.Generate(_random, GeneticSequence.AppearancePairs);
        _pendingSequences[uid] = new PendingSequence
        {
            Subject = subject, User = user, Appearance = selected.Clone(selected), Field = field, Answer = answer,
            State = new GeneticPuzzleState
            {
                Token = ++_sequenceToken, Title = field == null ? Loc.GetString("dna-eu-all") : Loc.GetString("dna-eu-" + field),
                Original = GeneticSequence.HideAppearance(_random, answer), Appearance = true
            }
        };
        UpdateUserInterface(uid, component);
    }

    private void OnSelectBufferAppearance(EntityUid uid, DnaModifierConsoleComponent component, GeneticBufferAppearanceMessage args)
    {
        if (!TrySubject(uid, component, out var subject) ||
            !_dnaClient.TryGetBufferData(uid, args.Buffer, out var sample) || sample.Identifier is not { } appearance ||
            (!AppearanceGene.Exists(args.Field) && args.Field.Length != 0))
            return;
        BeginAppearance(uid, component, subject, args.Actor, appearance, args.Field.Length == 0 ? null : args.Field);
    }

    private void OnCombineGenes(EntityUid uid, DnaModifierConsoleComponent component, GeneticCombineMessage args)
    {
        if (!TrySubject(uid, component, out var subject) ||
            FindGene(subject, args.First) is not { } first || FindGene(subject, args.Second) is not { } second ||
            first == second || !IsDiscovered(first.EnzymesPrototypeId) || !IsDiscovered(second.EnzymesPrototypeId))
            return;
        var ingredients = new HashSet<string> { first.EnzymesPrototypeId, second.EnzymesPrototypeId };
        var recipe = _prototypeManager.EnumeratePrototypes<GeneticRecipePrototype>().FirstOrDefault(r => r.Required.SetEquals(ingredients));
        var result = subject.Comp.EnzymesPrototypes?.FirstOrDefault(g => g.EnzymesPrototypeId == recipe?.Result);
        if (result == null || _dnaModifier.IsGeneActive(subject, result))
        {
            _geneticStatus[uid] = Loc.GetString("dna-combine-failed");
        }
        else
        {
            // Like Trauma, combination activates but does not discover or award science.
            _dnaModifier.SetGeneActive(subject, result, true);
            _geneticStatus[uid] = Loc.GetString("dna-combine-success", ("number", result.Order));
        }
        UpdateUserInterface(uid, component);
    }
}
