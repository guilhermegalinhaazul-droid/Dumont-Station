// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Stylesheets;
using Content.Shared.Genetics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Wega.Genetics.Ui;

public sealed partial class DnaModifierWindow
{
    /// <summary>
    /// Renders the server-authoritative mutation recipe projection in the existing Wega
    /// "Combinar" tab. No recipe matching or mutation application happens on the client.
    /// </summary>
    public void ApplyHybridCombinationState(DnaModifierHybridCombinationStateEvent state)
    {
        if (state.Console != _console)
            return;

        // XAML order is UI, S.E., Transfer, Combine, Rejuvenator.
        Tabs.SetTabTitle(3, "Combinar");
        Tabs.SetTabTitle(4, Loc.GetString("dna-modifier-tab-rejuvenator"));

        if (Tabs.GetChild(3) is not BoxContainer combineTab)
            return;

        combineTab.RemoveAllChildren();
        combineTab.Orientation = BoxContainer.LayoutOrientation.Vertical;

        combineTab.AddChild(new Label
        {
            Text = "Combinar",
            StyleClasses = { StyleNano.StyleClassLabelBig },
        });

        combineTab.AddChild(new Label
        {
            Text = "As combinações abaixo usam as receitas reais do sistema de mutações.",
            StyleClasses = { StyleNano.StyleClassLabelSecondaryColor },
        });

        if (!string.IsNullOrWhiteSpace(state.Feedback))
        {
            combineTab.AddChild(new Label
            {
                Text = state.Feedback,
            });
        }

        if (state.Recipes.Count == 0)
        {
            combineTab.AddChild(new Label
            {
                Text = "Nenhuma receita genética disponível.",
            });
            return;
        }

        foreach (var recipe in state.Recipes)
            combineTab.AddChild(CreateCombinationRecipeRow(recipe));
    }

    private Control CreateCombinationRecipeRow(HybridGeneCombinationRecipeState recipe)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(0, 6),
        };

        var ingredients = recipe.RequiredGeneNames.Count > 0
            ? string.Join(" + ", recipe.RequiredGeneNames)
            : string.Join(" + ", recipe.RequiredMutationIds);

        row.AddChild(new Label
        {
            Text = $"{ingredients} → {recipe.ResultGeneName}",
        });

        if (!recipe.CanCombine && !string.IsNullOrWhiteSpace(recipe.UnavailableReason))
        {
            row.AddChild(new Label
            {
                Text = recipe.UnavailableReason,
                StyleClasses = { StyleNano.StyleClassLabelSecondaryColor },
            });
        }

        var combine = new Button
        {
            Text = recipe.CanCombine ? "Combinar" : "Indisponível",
            Disabled = !recipe.CanCombine,
        };

        combine.OnPressed += _ => _entNetworkManager.SendSystemNetworkMessage(
            new DnaModifierHybridCombineRequestEvent(_console, recipe.RecipeId));

        row.AddChild(combine);
        return row;
    }
}
