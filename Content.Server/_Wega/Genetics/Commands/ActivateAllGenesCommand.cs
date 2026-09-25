// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Server.Genetics.System;
using Content.Shared.Administration;
using Content.Shared.Genetics;
using Robust.Shared.Console;

namespace Content.Server.Genetics.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class ActivateAllGenesCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "genetics_activate_all";
    public string Description => "Activates every available genetic gene on an entity.";
    public string Help => "genetics_activate_all <entity uid>";

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return CompletionResult.Empty;
    }

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError("Usage: genetics_activate_all <entity uid>");
            return;
        }

        if (!_entityManager.TryParseNetEntity(args[0], out EntityUid? target) || !_entityManager.EntityExists(target))
        {
            shell.WriteError($"Entity '{args[0]}' was not found.");
            return;
        }

        if (!_entityManager.TryGetComponent<DnaModifierComponent>(target.Value, out var dna))
        {
            shell.WriteError("The target has no DNA modifier component.");
            return;
        }

        var activated = _entityManager.System<DnaModifierSystem>().ActivateAllGenes((target.Value, dna));
        shell.WriteLine($"Activated {activated} genetic gene(s) on {target.Value}.");
    }
}
