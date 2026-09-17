// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Genetics;

/// <summary>
/// Client request for the authoritative hybrid mutation-recipe state.
/// </summary>
[Serializable, NetSerializable]
public sealed class DnaModifierHybridCombinationRequestEvent : EntityEventArgs
{
    public NetEntity Console { get; }

    public DnaModifierHybridCombinationRequestEvent(NetEntity console)
    {
        Console = console;
    }
}

/// <summary>
/// Client request to execute one of the real Trauma mutation recipes.
/// The server resolves and validates the recipe again before doing anything.
/// </summary>
[Serializable, NetSerializable]
public sealed class DnaModifierHybridCombineRequestEvent : EntityEventArgs
{
    public NetEntity Console { get; }
    public string RecipeId { get; }

    public DnaModifierHybridCombineRequestEvent(NetEntity console, string recipeId)
    {
        Console = console;
        RecipeId = recipeId;
    }
}

/// <summary>
/// Serializable projection of a MutationRecipePrototype for the Wega UI.
/// It is display state only; recipes remain owned by MutationSystem/prototypes.
/// </summary>
[Serializable, NetSerializable]
public sealed class HybridGeneCombinationRecipeState
{
    public string RecipeId { get; set; } = string.Empty;
    public List<string> RequiredMutationIds { get; set; } = new();
    public List<string> RequiredGeneNames { get; set; } = new();
    public string ResultMutationId { get; set; } = string.Empty;
    public string ResultGeneName { get; set; } = string.Empty;
    public bool CanCombine { get; set; }
    public string? UnavailableReason { get; set; }
}

/// <summary>
/// Server-authoritative recipe state for the existing Wega genetics window.
/// </summary>
[Serializable, NetSerializable]
public sealed class DnaModifierHybridCombinationStateEvent : EntityEventArgs
{
    public NetEntity Console { get; }
    public List<HybridGeneCombinationRecipeState> Recipes { get; }
    public string? Feedback { get; }

    public DnaModifierHybridCombinationStateEvent(
        NetEntity console,
        List<HybridGeneCombinationRecipeState> recipes,
        string? feedback)
    {
        Console = console;
        Recipes = recipes;
        Feedback = feedback;
    }
}

/// <summary>
/// Minimal do-after payload for Wega's adapter over Trauma mutation combination.
/// Mutation IDs themselves are resolved from the recipe on completion.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class DnaModifierHybridCombineDoAfterEvent : DoAfterEvent
{
    public NetEntity Body;
    public string RecipeId;

    public DnaModifierHybridCombineDoAfterEvent(NetEntity body, string recipeId)
    {
        Body = body;
        RecipeId = recipeId;
    }

    public override DoAfterEvent Clone()
        => new DnaModifierHybridCombineDoAfterEvent(Body, RecipeId);
}
