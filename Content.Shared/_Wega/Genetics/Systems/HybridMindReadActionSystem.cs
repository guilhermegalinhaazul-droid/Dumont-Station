// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.IdentityManagement;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Trauma.Shared.Genetics.Abilities;

namespace Content.Trauma.Shared.Genetics.Abilities;

/// <summary>
/// Dumont-compatible implementation of the imported mind-reader mutation action.
/// The original imported system depended on a Trauma-only mind-message subsystem that is not
/// part of Dumont; the mutation still performs a real server-validated mind read without
/// introducing that unrelated project dependency.
/// </summary>
public sealed partial class MindReadActionSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mob = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MindReadActionComponent, MindReadActionEvent>(OnMindRead);
    }

    private void OnMindRead(Entity<MindReadActionComponent> ent, ref MindReadActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.Performer;
        var target = args.Target;
        var identity = Identity.Name(target, EntityManager);

        if (user == target)
        {
            _popup.PopupEntity(Loc.GetString("MutationMindReader-popup-self"), user, user);
            return;
        }

        if (!_mind.TryGetMind(target, out _, out var mind))
        {
            _popup.PopupEntity(
                Loc.GetString("MutationMindReader-popup-target-mindless", ("target", identity)),
                user,
                user);
            return;
        }

        if (_mob.IsDead(target))
        {
            _popup.PopupEntity(
                Loc.GetString("MutationMindReader-popup-target-dead", ("target", identity)),
                user,
                user);
            return;
        }

        var trueName = mind.CharacterName ?? identity;
        _popup.PopupEntity(
            Loc.GetString("MutationMindReader-popup-true-identity", ("target", identity), ("name", trueName)),
            user,
            user);

        if (Random.Prob(ent.Comp.AlertProb))
            _popup.PopupEntity(Loc.GetString("MutationMindReader-popup-alert"), target, target, PopupType.MediumCaution);
    }
}
