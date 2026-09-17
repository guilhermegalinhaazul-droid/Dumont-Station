// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Stylesheets;
using Content.Shared.Genetics;
using Content.Shared.Genetics.UI;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Wega.Genetics.Ui;

public sealed partial class DnaModifierWindow
{
    /// <summary>
    /// Applies the authoritative hybrid catalog/sequencing state received from the server.
    /// This is intentionally a minimal Wega view over Trauma's existing sequencing data.
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
            Text = "GENES",
            StyleClasses = { StyleNano.StyleClassLabelSecondaryColor },
        });

        foreach (var gene in state.Catalog)
            root.AddChild(CreateHybridGeneRow(gene));

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
            MinWidth = 180,
        });

        var status = new Label
        {
            Text = gene.Active ? "[ativo]" : gene.Discovered ? "[descoberto]" : "[desconhecido]",
            MinWidth = 150,
        };
        status.StyleClasses.Add(gene.Active
            ? StyleNano.StyleClassLabelGreen
            : StyleNano.StyleClassLabelSecondaryColor);
        row.AddChild(status);

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
            Text = $"Sequenciamento: {state.MutationId}",
            StyleClasses = { StyleNano.StyleClassLabelSecondaryColor },
        });

        panel.AddChild(new Label
        {
            Text = "Complete as bases desconhecidas (X) usando A, T, G e C.",
        });

        var grid = new GridContainer
        {
            Columns = 16,
        };

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
        {
            panel.AddChild(new Label
            {
                Text = "Sequência incorreta.",
            });
        }

        var actions = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(0, 6, 0, 0),
        };

        var reset = new Button
        {
            Text = "Resetar",
            Disabled = bases == originalBases,
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
    /// Matches Trauma GeneticsConsoleSystem.CycleBase(..., Next): X → A → C → G → T → X.
    /// The actual target sequence and A↔T / G↔C complementarity remain owned by MutationData.
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
