// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Wega.Genetics.Ui;
using Content.Shared.Genetics;

namespace Content.Client._Wega.Genetics.Systems;

public sealed class HybridGeneSequencingClientSystem : EntitySystem
{
    [Dependency] private readonly IEntityNetworkManager _network = default!;

    private readonly Dictionary<NetEntity, DnaModifierWindow> _windows = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<DnaModifierHybridSequencingStateEvent>(OnState);
    }

    public void BindWindow(DnaModifierWindow window, NetEntity console)
    {
        foreach (var (boundConsole, boundWindow) in _windows.ToArray())
        {
            if (boundWindow == window && boundConsole != console)
                _windows.Remove(boundConsole);
        }

        _windows[console] = window;
        _network.SendSystemNetworkMessage(new DnaModifierHybridCatalogRequestEvent(console));
    }

    public void UnbindWindow(DnaModifierWindow window)
    {
        foreach (var (console, boundWindow) in _windows.ToArray())
        {
            if (boundWindow == window)
                _windows.Remove(console);
        }
    }

    private void OnState(DnaModifierHybridSequencingStateEvent args)
    {
        if (!_windows.TryGetValue(args.Console, out var window) || window.Disposed)
            return;

        window.ApplyHybridSequencingState(args);
    }
}
