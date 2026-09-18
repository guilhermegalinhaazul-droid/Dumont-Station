// SPDX-FileCopyrightText: 2025 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Wega.Genetics.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Genetics;
using Content.Shared.Genetics.UI;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Wega.Genetics.Ui;

[UsedImplicitly]
public sealed class DnaModifierBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private DnaModifierWindow? _window;
    private HybridGeneSequencingClientSystem? _hybridSequencing;

    public DnaModifierBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DnaModifierWindow>();
        _hybridSequencing = EntMan.System<HybridGeneSequencingClientSystem>();

        _window.EjectButtonDisk.OnPressed += _ => SendMessage(
            new ItemSlotButtonPressedEvent(SharedDnaModifier.DiskSlotName));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window is not { Disposed: false })
            return;

        var dnaState = (DnaModifierBoundUserInterfaceState) state;
        _window.UpdateState(dnaState);

        // The legacy Wega BUI state still refreshes independently. Request the authoritative
        // hybrid catalog immediately afterwards so the minimal sequencing view stays in sync.
        _hybridSequencing?.BindWindow(_window, dnaState.Console);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _window != null)
            _hybridSequencing?.UnbindWindow(_window);

        base.Dispose(disposing);
    }
}
