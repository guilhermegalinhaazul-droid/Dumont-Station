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

    private void UpdateEiState(DnaModifierBoundUserInterfaceState state)
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
        {
            root.AddChild(new Label
            {
                Text = $"Espécie de origem: {profile.SpeciesId}",
            });
        }

        var wegaGenes = profile.Info?.Count ?? 0;
        var activeTrauma = profile.TraumaActiveMutationNumbers?.Count ?? 0;
        var dormantTrauma = profile.TraumaDormantMutationNumbers?.Count ?? 0;

        root.AddChild(new Label
        {
            Text = $"Genes Wega: {wegaGenes} | Trauma ativos: {activeTrauma} | Trauma dormentes: {dormantTrauma}",
        });

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
        root.AddChild(apply);

        return root;
    }
}
