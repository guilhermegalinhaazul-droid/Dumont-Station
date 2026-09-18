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
    private DnaModifierBoundUserInterfaceState? _lastEiState;
    private DnaModifierEiEditingStateEvent? _eiEditingState;

    private void InitializeEiUi()
    {
        EiCopyBuffer1Button.OnPressed += _ => CopyEiToBuffer(1);
        EiCopyBuffer2Button.OnPressed += _ => CopyEiToBuffer(2);
        EiCopyBuffer3Button.OnPressed += _ => CopyEiToBuffer(3);
    }

    private void CopyEiToBuffer(int index)
    {
        _updateBuffer = true;
        _entNetworkManager.SendSystemNetworkMessage(
            new DnaModifierEiCopyRequestEvent(_console, index));
    }

    public void ApplyEiEditingState(DnaModifierEiEditingStateEvent state)
    {
        if (state.Console != _console)
            return;

        _eiEditingState = state;
        if (_lastEiState != null)
            RenderEiState(_lastEiState);
    }

    private void UpdateEiState(DnaModifierBoundUserInterfaceState state)
    {
        _lastEiState = state;
        RenderEiState(state);
    }

    private void RenderEiState(DnaModifierBoundUserInterfaceState state)
    {
        var hasSubject = !string.IsNullOrWhiteSpace(state.ScannerBodyInfo);
        var canCapture = hasSubject && (state.Unique != null || state.Enzymes != null);

        EiCurrentProfileLabel.Text = hasSubject
            ? string.IsNullOrWhiteSpace(state.ScannerSpecies)
                ? state.ScannerBodyInfo
                : $"{state.ScannerBodyInfo} ({state.ScannerSpecies})"
            : "Nenhum organismo compatível no scanner.";

        EiCopyBuffer1Button.Disabled = !canCapture || HasBuffer(state, 1);
        EiCopyBuffer2Button.Disabled = !canCapture || HasBuffer(state, 2);
        EiCopyBuffer3Button.Disabled = !canCapture || HasBuffer(state, 3);

        EiProfilesContainer.RemoveAllChildren();

        for (var index = 1; index <= 3; index++)
        {
            if (!state.Buffers.TryGetValue(index, out var data)
                || data == null
                || !data.IsFullGeneticProfile)
            {
                EiProfilesContainer.AddChild(new Label
                {
                    Text = $"Buffer {index}: sem EI armazenado.",
                    StyleClasses = { StyleNano.StyleClassLabelSecondaryColor },
                });
                continue;
            }

            EiProfilesContainer.AddChild(CreateEiProfileRow(index, data, hasSubject));
        }

        if (_eiEditingState != null
            && state.Buffers.TryGetValue(_eiEditingState.BufferIndex, out var editingProfile)
            && editingProfile?.IsFullGeneticProfile == true)
        {
            EiProfilesContainer.AddChild(CreateEiEditingPanel(_eiEditingState));
        }
    }

    private static bool HasBuffer(DnaModifierBoundUserInterfaceState state, int index)
        => state.Buffers.TryGetValue(index, out var data) && data != null;

    private Control CreateEiProfileRow(int index, EnzymeInfo profile, bool hasReceiver)
    {
        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(0, 6),
        };

        var identity = string.IsNullOrWhiteSpace(profile.GeneticIdentityName)
            ? "Identidade desconhecida"
            : profile.GeneticIdentityName;

        root.AddChild(new Label
        {
            Text = $"Buffer {index} — EI de {identity}",
            StyleClasses = { StyleNano.StyleClassLabelBig },
        });

        root.AddChild(new Label
        {
            Text = $"Amostra: {profile.SampleName}",
            StyleClasses = { StyleNano.StyleClassLabelSecondaryColor },
        });

        if (!string.IsNullOrWhiteSpace(profile.SpeciesId))
            root.AddChild(new Label { Text = $"Espécie de origem: {profile.SpeciesId}" });

        var wegaGenes = profile.Info?.Count ?? 0;
        var activeTrauma = profile.TraumaActiveMutationNumbers?.Count ?? 0;
        var dormantTrauma = profile.TraumaDormantMutationNumbers?.Count ?? 0;

        root.AddChild(new Label
        {
            Text = $"Genes Wega: {wegaGenes} | Trauma ativos: {activeTrauma} | Trauma dormentes: {dormantTrauma}",
        });

        var actions = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(0, 4),
        };

        var apply = new Button
        {
            Text = hasReceiver ? "Aplicar EI ao organismo no scanner" : "Insira um receptor no scanner",
            Disabled = !hasReceiver,
        };
        apply.OnPressed += _ =>
        {
            apply.Disabled = true;
            _entNetworkManager.SendSystemNetworkMessage(
                new DnaModifierEiApplyRequestEvent(_console, index));
        };
        actions.AddChild(apply);

        if (profile.Identifier != null)
        {
            var edit = new Button { Text = "Editar genética ATCG" };
            edit.OnPressed += _ => _entNetworkManager.SendSystemNetworkMessage(
                new DnaModifierEiEditRequestEvent(_console, index, EiAppearanceRegion.HairColor));
            actions.AddChild(edit);
        }

        root.AddChild(actions);
        return root;
    }

    private Control CreateEiEditingPanel(DnaModifierEiEditingStateEvent state)
    {
        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(0, 12),
        };

        root.AddChild(new Label
        {
            Text = $"ENGENHARIA GENÉTICA — EI de {state.ProfileName}",
            StyleClasses = { StyleNano.StyleClassLabelBig },
        });

        root.AddChild(new Label
        {
            Text = "A-T possui 2 ligações de hidrogênio; G-C possui 3. Edite as duas fitas e mantenha todos os pares complementares.",
        });

        if (!string.IsNullOrWhiteSpace(state.Feedback))
            root.AddChild(new Label { Text = state.Feedback });

        if (state.Regions.Count == 0)
            return root;

        var regions = new OptionButton { MinWidth = 260 };
        foreach (var option in state.Regions)
            regions.AddItem(option.Name, (int) option.Region);

        regions.SelectId((int) state.Region);
        regions.OnItemSelected += args =>
        {
            regions.SelectId(args.Id);
            _entNetworkManager.SendSystemNetworkMessage(new DnaModifierEiEditRequestEvent(
                _console,
                state.BufferIndex,
                (EiAppearanceRegion) args.Id));
        };
        root.AddChild(regions);

        root.AddChild(new Label
        {
            Text = $"Energia de ruptura usada: {state.BondCost}/{state.BondBudget} ligações-H",
            StyleClasses = { StyleNano.StyleClassLabelSecondaryColor },
        });

        var grid = new GridContainer { Columns = 4 };
        grid.AddChild(new Label { Text = "Par" });
        grid.AddChild(new Label { Text = "Fita 1" });
        grid.AddChild(new Label { Text = "Ligações" });
        grid.AddChild(new Label { Text = "Fita 2" });

        foreach (var pair in state.Pairs)
        {
            grid.AddChild(new Label { Text = (pair.Index + 1).ToString() });

            var top = new Button
            {
                Text = pair.Top,
                MinWidth = 42,
            };
            top.OnPressed += _ => _entNetworkManager.SendSystemNetworkMessage(
                new DnaModifierEiSetBaseEvent(
                    _console,
                    pair.Index,
                    false,
                    CycleEiBase(pair.Top)));
            grid.AddChild(top);

            var bond = new Label
            {
                Text = pair.Valid
                    ? pair.HydrogenBonds == 2 ? "A=T • 2H" : "G≡C • 3H"
                    : "ROMPIDO",
                MinWidth = 80,
            };
            bond.StyleClasses.Add(pair.Valid
                ? StyleNano.StyleClassLabelSecondaryColor
                : StyleNano.StyleClassLabelRed);
            grid.AddChild(bond);

            var bottom = new Button
            {
                Text = pair.Bottom,
                MinWidth = 42,
            };
            bottom.OnPressed += _ => _entNetworkManager.SendSystemNetworkMessage(
                new DnaModifierEiSetBaseEvent(
                    _console,
                    pair.Index,
                    true,
                    CycleEiBase(pair.Bottom)));
            grid.AddChild(bottom);
        }

        root.AddChild(grid);

        var actions = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(0, 6),
        };

        var reset = new Button { Text = "Restaurar região" };
        reset.OnPressed += _ => _entNetworkManager.SendSystemNetworkMessage(
            new DnaModifierEiResetEditEvent(_console));
        actions.AddChild(reset);

        var commit = new Button
        {
            Text = "Gravar região no EI",
            Disabled = !state.CanCommit,
        };
        commit.OnPressed += _ =>
        {
            commit.Disabled = true;
            _entNetworkManager.SendSystemNetworkMessage(
                new DnaModifierEiCommitEditEvent(_console));
        };
        actions.AddChild(commit);

        root.AddChild(actions);
        return root;
    }

    private static string CycleEiBase(string value)
        => value switch
        {
            "A" => "T",
            "T" => "G",
            "G" => "C",
            "C" => "A",
            _ => "A",
        };
}
