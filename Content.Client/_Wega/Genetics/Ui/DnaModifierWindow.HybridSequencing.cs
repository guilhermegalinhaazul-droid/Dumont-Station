// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Client.Stylesheets;
using Content.Shared.Genetics;
using Content.Shared.Genetics.UI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Wega.Genetics.Ui;

public sealed partial class DnaModifierWindow
{
    private string? _hybridGeneFeedback;
    private bool _hybridGeneFeedbackSuccess;

    public void SetHybridGeneFeedback(string message, bool success)
    {
        _hybridGeneFeedback = message;
        _hybridGeneFeedbackSuccess = success;
    }

    /// <summary>
    /// Main player-facing genetics catalog. Wega's hexadecimal data remains an internal
    /// implementation detail and is not needed to discover or toggle genes here.
    /// </summary>
    public void ApplyHybridSequencingState(DnaModifierHybridSequencingStateEvent state)
    {
        if (state.Console != _console)
            return;

        UiPanel.Visible = true;
        UiContainer.RemoveAllChildren();

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(0, 0, 0, 10),
        };

        root.AddChild(new Label
        {
            Text = "CATÁLOGO DE GENES",
            StyleClasses = { StyleNano.StyleClassLabelBig },
        });

        if (!string.IsNullOrWhiteSpace(_hybridGeneFeedback))
        {
            var feedback = new Label { Text = _hybridGeneFeedback };
            feedback.StyleClasses.Add(_hybridGeneFeedbackSuccess
                ? StyleNano.StyleClassPowerStateGood
                : StyleNano.StyleClassLabelSecondaryColor);
            root.AddChild(feedback);
        }

        var filter = new OptionButton { MinWidth = 180 };
        filter.AddItem("Todos", 0);
        filter.AddItem("Descobertos", 1);
        filter.AddItem("Ativos", 2);
        filter.AddItem("Desconhecidos", 3);
        filter.SelectId(0);
        root.AddChild(filter);

        var search = new LineEdit
        {
            PlaceHolder = "Pesquisar gene...",
            HorizontalExpand = true,
        };
        root.AddChild(search);

        var geneList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(0, 6, 0, 0),
        };
        root.AddChild(geneList);

        var currentFilter = 0;
        var searchText = string.Empty;

        void RenderGenes()
        {
            geneList.RemoveAllChildren();
            foreach (var gene in state.Catalog)
            {
                var matchesFilter = currentFilter switch
                {
                    1 => gene.Discovered,
                    2 => gene.Active,
                    3 => !gene.Discovered,
                    _ => true,
                };

                var matchesSearch = string.IsNullOrWhiteSpace(searchText)
                    || gene.GeneName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || gene.GeneId.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || gene.Origin.Contains(searchText, StringComparison.OrdinalIgnoreCase);

                if (matchesFilter && matchesSearch)
                    geneList.AddChild(CreateHybridGeneRow(gene));
            }
        }

        filter.OnItemSelected += args =>
        {
            filter.SelectId(args.Id);
            currentFilter = args.Id;
            RenderGenes();
        };
        search.OnTextChanged += args =>
        {
            searchText = args.Text.Trim();
            RenderGenes();
        };
        RenderGenes();

        if (state.MutationId != null
            && state.Bases != null
            && state.OriginalBases != null
            && state.Bases.Length == state.OriginalBases.Length)
        {
            root.AddChild(CreateSequencingPanel(state));
        }

        UiContainer.AddChild(root);
    }

    private Control CreateHybridGeneRow(GeneCatalogEntry gene)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(0, 2),
        };

        row.AddChild(new Label
        {
            Text = gene.GeneName,
            MinWidth = 230,
        });

        var statusText = gene.Active
            ? gene.Discovered ? "[ativo]" : "[ativo, não identificado]"
            : gene.Discovered ? "[descoberto]" : "[desconhecido]";

        var status = new Label
        {
            Text = statusText,
            MinWidth = 170,
        };
        status.StyleClasses.Add(gene.Active
            ? StyleNano.StyleClassPowerStateGood
            : StyleNano.StyleClassLabelSecondaryColor);
        row.AddChild(status);

        if (gene.Active || (gene.Discovered && gene.Available))
        {
            var desiredState = !gene.Active;
            var toggle = new Button
            {
                Text = desiredState ? "Ativar" : "Desativar",
            };
            toggle.OnPressed += _ =>
            {
                toggle.Disabled = true;
                _entNetworkManager.SendSystemNetworkMessage(
                    new DnaModifierHybridSetGeneActiveEvent(_console, gene.CanonicalKey, desiredState));
            };
            row.AddChild(toggle);
        }

        if (gene.TraumaMutationId != null && !gene.Discovered)
        {
            var mutationId = gene.TraumaMutationId;
            var sequenceButton = new Button
            {
                Text = gene.CanSequence ? "Sequenciar" : "Sem amostra",
                Disabled = !gene.CanSequence,
            };

            sequenceButton.OnPressed += _ => _entNetworkManager.SendSystemNetworkMessage(
                new DnaModifierHybridSelectGeneEvent(_console, mutationId));
            row.AddChild(sequenceButton);
        }

        return row;
    }

    private Control CreateSequencingPanel(DnaModifierHybridSequencingStateEvent state)
    {
        var bases = state.Bases!;
        var originalBases = state.OriginalBases!;

        var panel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(0, 12, 0, 0),
        };

        panel.AddChild(new Label
        {
            Text = "SEQUENCIAMENTO GENÉTICO",
            StyleClasses = { StyleNano.StyleClassLabelBig },
        });

        panel.AddChild(new Label
        {
            Text = "Complete as bases desconhecidas (X) usando A, T, G e C. A validação ocorre no servidor.",
        });

        var grid = new GridContainer { Columns = 16 };

        for (var i = 0; i < bases.Length; i++)
        {
            var index = i;
            var currentBase = bases[index];
            var originalBase = originalBases[index];
            var button = new Button
            {
                Text = currentBase.ToString(),
                Disabled = originalBase != 'X',
                MinWidth = 32,
            };

            button.OnPressed += _ =>
            {
                var nextBase = CycleBase(currentBase);
                _entNetworkManager.SendSystemNetworkMessage(new DnaModifierHybridSetBaseEvent(
                    _console,
                    (uint) index,
                    nextBase.ToString()));
            };

            grid.AddChild(button);
        }

        panel.AddChild(grid);

        if (state.LastAttemptFailed)
            panel.AddChild(new Label { Text = "Sequência incorreta." });

        var actions = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(0, 6, 0, 0),
        };

        var reset = new Button
        {
            Text = "Resetar",
            Disabled = bases.SequenceEqual(originalBases),
        };
        reset.OnPressed += _ => _entNetworkManager.SendSystemNetworkMessage(
            new DnaModifierHybridResetSequenceEvent(_console));

        var submit = new Button
        {
            Text = "Validar sequência",
            Disabled = bases.Contains('X'),
        };
        submit.OnPressed += _ => _entNetworkManager.SendSystemNetworkMessage(
            new DnaModifierHybridSubmitSequenceEvent(_console));

        actions.AddChild(reset);
        actions.AddChild(submit);
        panel.AddChild(actions);

        return panel;
    }

    /// <summary>
    /// Matches Trauma's sequencer cycle. The target and complementary strand remain owned by
    /// MutationData/ScannedGenomeSystem; the client only proposes edits to originally unknown bases.
    /// </summary>
    private static char CycleBase(char current)
        => current switch
        {
            'A' => 'C',
            'C' => 'G',
            'G' => 'T',
            'T' => 'X',
            'X' => 'A',
            _ => 'X',
        };
}
