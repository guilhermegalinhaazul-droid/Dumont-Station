using Content.Client._Dumont.Overlays;
using Content.Shared.Genetics;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client._Wega.Genetics;

public sealed class XrayVisionGenSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    private MesonVisionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new MesonVisionOverlay();

        SubscribeLocalEvent<XrayVisionGenComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<XrayVisionGenComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<XrayVisionGenComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnInit(Entity<XrayVisionGenComponent> ent, ref ComponentInit args) => Refresh();
    private void OnShutdown(Entity<XrayVisionGenComponent> ent, ref ComponentShutdown args) => Refresh();
    private void OnState(Entity<XrayVisionGenComponent> ent, ref AfterAutoHandleStateEvent args) => Refresh();

    private void Refresh()
    {
        if (_players.LocalEntity is not { } player || !TryComp<XrayVisionGenComponent>(player, out var component))
        {
            _overlays.RemoveOverlay(_overlay);
            return;
        }

        _overlay.Range = component.Range;
        _overlays.AddOverlay(_overlay);
    }
}
