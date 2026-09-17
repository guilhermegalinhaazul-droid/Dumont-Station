// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Medical.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Administration.Logs;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Genetics;
using Content.Shared.Genetics.UI;
using Content.Trauma.Shared.Genetics.Mutations;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Genetics.System;

/// <summary>
/// Thin Wega adapter over Trauma's existing recipe index and mutation combination APIs.
/// Recipe ownership and actual mutation application remain in MutationSystem.
/// </summary>
public sealed class HybridGeneCombinationSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly HybridGeneCatalogSystem _catalog = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MutationSystem _mutation = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly ScannedGenomeSystem _scannedGenome = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    private readonly HashSet<(EntityUid Console, EntityUid User)> _pendingCombinations = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<DnaModifierHybridCombinationRequestEvent>(OnStateRequest);
        SubscribeNetworkEvent<DnaModifierHybridCombineRequestEvent>(OnCombineRequest);
        SubscribeLocalEvent<DnaModifierConsoleComponent, DnaModifierHybridCombineDoAfterEvent>(OnCombineDoAfter);
        SubscribeLocalEvent<DnaModifierConsoleComponent, DoAfterAttemptEvent<DnaModifierHybridCombineDoAfterEvent>>(OnCombineCheck);
    }

    private void OnStateRequest(DnaModifierHybridCombinationRequestEvent args, EntitySessionEventArgs sessionArgs)
    {
        var console = GetEntity(args.Console);
        if (!IsAuthorized(console, sessionArgs.SenderSession))
            return;

        SendState(console, sessionArgs.SenderSession, null);
    }

    private void OnCombineRequest(DnaModifierHybridCombineRequestEvent args, EntitySessionEventArgs sessionArgs)
    {
        var console = GetEntity(args.Console);
        var session = sessionArgs.SenderSession;
        if (!IsAuthorized(console, session)
            || !TryComp<DnaModifierConsoleComponent>(console, out var consoleComp)
            || session.AttachedEntity is not { } user)
        {
            return;
        }

        var pendingKey = (console, user);
        if (_pendingCombinations.Contains(pendingKey))
        {
            SendState(console, session, "Uma combinação já está em andamento.");
            return;
        }

        if (!TryResolveRecipe(args.RecipeId, out _, out var recipe))
        {
            SendState(console, session, "Receita genética inválida.");
            return;
        }

        if (!TryGetScannedBody(console, out var body)
            || _mutation.GetMutatable(body) is null)
        {
            SendState(console, session, "Insira um organismo mutável no scanner.");
            return;
        }

        _scannedGenome.ScanGenome(body);

        if (!CanUseRecipe(body, recipe, out var reason))
        {
            SendState(console, session, reason);
            return;
        }

        var damage = _mutation.GetGeneticDamage(body) ?? 0;
        if (damage > consoleComp.MaxGeneticDamage)
        {
            SendState(console, session, "O organismo possui dano genético excessivo.");
            return;
        }

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            user,
            consoleComp.CombineDelay,
            new DnaModifierHybridCombineDoAfterEvent(GetNetEntity(body), recipe.ID),
            eventTarget: console,
            target: body,
            used: console)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
        };

        _pendingCombinations.Add(pendingKey);
        if (!_doAfter.TryStartDoAfter(doAfterArgs))
        {
            _pendingCombinations.Remove(pendingKey);
            SendState(console, session, "Não foi possível iniciar a combinação.");
            return;
        }

        SendState(console, session, "Combinação iniciada.");
    }

    private void OnCombineDoAfter(
        Entity<DnaModifierConsoleComponent> console,
        ref DnaModifierHybridCombineDoAfterEvent args)
    {
        if (args.User is { } user)
            _pendingCombinations.Remove((console.Owner, user));

        var session = FindSession(args.User);

        if (args.Cancelled)
        {
            if (session != null)
                SendState(console.Owner, session, "Combinação cancelada.");
            return;
        }

        args.Handled = true;
        var body = GetEntity(args.Body);

        if (!_power.IsPowered(console.Owner)
            || !TryGetScannedBody(console.Owner, out var currentBody)
            || currentBody != body
            || !TryResolveRecipe(args.RecipeId, out _, out var recipe)
            || _mutation.GetMutatable(body) is not { } mutatable)
        {
            if (session != null)
                SendState(console.Owner, session, "O organismo ou a receita não está mais disponível.");
            return;
        }

        _scannedGenome.ScanGenome(body);

        if (!CanUseRecipe(body, recipe, out var reason))
        {
            if (session != null)
                SendState(console.Owner, session, reason);
            return;
        }

        var damage = _mutation.GetGeneticDamage(body) ?? 0;
        if (damage > console.Comp.MaxGeneticDamage)
        {
            if (session != null)
                SendState(console.Owner, session, "O organismo possui dano genético excessivo.");
            return;
        }

        // MutationSystem.Combine.cs remains the single authority for recipe matching.
        var first = recipe.Required[0];
        var second = recipe.Required[1];
        if (_mutation.CombineMutations(first, second) is not { } result || result != recipe.Result)
        {
            if (session != null)
                SendState(console.Owner, session, "A receita não produz mais o resultado esperado.");
            return;
        }

        // AddMutation remains the authority for active-state changes, Required, Conflicts,
        // instability, Target, mutation events and all component-specific behavior.
        if (!_mutation.AddMutation(mutatable.AsNullable(), result, user: args.User, predicted: false))
        {
            if (session != null)
            {
                SendState(
                    console.Owner,
                    session,
                    "Não foi possível aplicar o resultado: ele já pode estar presente, faltar um requisito ou existir um conflito ativo.");
            }
            return;
        }

        _damage.ChangeDamage(body, console.Comp.CombineDamage);

        // Match Trauma's console behavior: a combined result gets a sequence, but its
        // MutationData.Discovered flag is NOT changed here. It must still be sequenced normally.
        _scannedGenome.TryAddSequence(body, result);

        _adminLog.Add(
            LogType.Genetics,
            LogImpact.Medium,
            $"{result} combined from {first} and {second} by {args.User:user} using Wega console {console.Owner}");

        if (session != null)
            SendState(console.Owner, session, "Combinação concluída. O resultado precisa ser descoberto pelo sequenciamento se ainda for desconhecido.");
    }

    private void OnCombineCheck(
        Entity<DnaModifierConsoleComponent> console,
        ref DoAfterAttemptEvent<DnaModifierHybridCombineDoAfterEvent> args)
    {
        var body = GetEntity(args.Event.Body);
        if (!_power.IsPowered(console.Owner)
            || !TryGetScannedBody(console.Owner, out var currentBody)
            || currentBody != body)
        {
            args.Cancel();
        }
    }

    private bool CanUseRecipe(EntityUid body, MutationRecipePrototype recipe, out string reason)
    {
        reason = string.Empty;

        // Trauma's current CombineMutations API explicitly supports two ingredients.
        if (recipe.Required.Count != 2)
        {
            reason = "Esta receita não é suportada pela API de combinação atual.";
            return false;
        }

        var catalog = _catalog.BuildCatalog(body);
        var byMutation = catalog
            .Where(entry => entry.TraumaMutationId != null)
            .ToDictionary(entry => entry.TraumaMutationId!, StringComparer.Ordinal);

        foreach (var required in recipe.Required)
        {
            var id = required.ToString();
            if (!byMutation.TryGetValue(id, out var entry) || !entry.Discovered || !entry.Available)
            {
                reason = "Descubra e disponibilize todos os genes necessários antes de combinar.";
                return false;
            }
        }

        if (_mutation.GetMutatable(body) is not { } mutatable)
        {
            reason = "O organismo escaneado não aceita mutações.";
            return false;
        }

        if (_mutation.HasMutation(mutatable.AsNullable(), recipe.Result))
        {
            reason = "O resultado dessa receita já está ativo no organismo.";
            return false;
        }

        if (_mutation.CombineMutations(recipe.Required[0], recipe.Required[1]) is not { } result
            || result != recipe.Result)
        {
            reason = "A receita não é válida no MutationSystem atual.";
            return false;
        }

        return true;
    }

    private List<HybridGeneCombinationRecipeState> BuildRecipeStates(EntityUid? body)
    {
        var catalog = _catalog.BuildCatalog(body);
        var byMutation = catalog
            .Where(entry => entry.TraumaMutationId != null)
            .ToDictionary(entry => entry.TraumaMutationId!, StringComparer.Ordinal);

        var states = new List<HybridGeneCombinationRecipeState>();
        var recipeIds = _mutation.ResultRecipes.Values
            .SelectMany(recipes => recipes)
            .Distinct()
            .OrderBy(id => id.ToString());

        foreach (var recipeId in recipeIds)
        {
            var recipe = _prototypeManager.Index(recipeId);
            var requiredIds = recipe.Required.Select(id => id.ToString()).ToList();
            var requiredNames = requiredIds
                .Select(id => byMutation.TryGetValue(id, out var entry) ? entry.GeneName : id)
                .ToList();
            var resultId = recipe.Result.ToString();
            var resultName = byMutation.TryGetValue(resultId, out var resultEntry)
                ? resultEntry.GeneName
                : resultId;

            bool canCombine;
            string? unavailableReason;
            if (body is { } target)
            {
                canCombine = CanUseRecipe(target, recipe, out var reason);
                unavailableReason = canCombine ? null : reason;
            }
            else
            {
                canCombine = false;
                unavailableReason = "Insira um organismo mutável no scanner.";
            }

            states.Add(new HybridGeneCombinationRecipeState
            {
                RecipeId = recipe.ID,
                RequiredMutationIds = requiredIds,
                RequiredGeneNames = requiredNames,
                ResultMutationId = resultId,
                ResultGeneName = resultName,
                CanCombine = canCombine,
                UnavailableReason = unavailableReason,
            });
        }

        return states;
    }

    private bool TryResolveRecipe(
        string id,
        out ProtoId<MutationRecipePrototype> recipeId,
        out MutationRecipePrototype recipe)
    {
        foreach (var recipes in _mutation.ResultRecipes.Values)
        {
            foreach (var candidate in recipes)
            {
                if (!string.Equals(candidate.ToString(), id, StringComparison.Ordinal))
                    continue;

                recipeId = candidate;
                recipe = _prototypeManager.Index(candidate);
                return true;
            }
        }

        recipeId = default;
        recipe = default!;
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
    {
        return _power.IsPowered(console)
            && session.AttachedEntity is { } actor
            && TryComp<DnaModifierConsoleComponent>(console, out _)
            && _ui.IsUiOpen(console, DnaModifierUiKey.Key, actor);
    }

    private ICommonSession? FindSession(EntityUid? actor)
    {
        if (actor == null)
            return null;

        return _players.NetworkedSessions.FirstOrDefault(session => session.AttachedEntity == actor);
    }

    private void SendState(EntityUid console, ICommonSession session, string? feedback)
    {
        EntityUid? body = null;
        if (TryGetScannedBody(console, out var scannedBody) && _mutation.IsMutatable(scannedBody))
        {
            _scannedGenome.ScanGenome(scannedBody);
            body = scannedBody;
        }

        RaiseNetworkEvent(
            new DnaModifierHybridCombinationStateEvent(
                GetNetEntity(console),
                BuildRecipeStates(body),
                feedback),
            session);
    }
}
