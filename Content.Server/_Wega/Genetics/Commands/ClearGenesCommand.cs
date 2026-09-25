// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Server.Genetics.System;
using Content.Shared.Administration;
using Content.Shared.Genetics;
using Robust.Shared.Console;

namespace Content.Server.Genetics.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class ClearGenesCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "genetics_clear";
    public string Description => "Remove todos os genes aplicados de uma entidade.";
    public string Help => "genetics_clear [UID da entidade]";

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHintOptions(
                CompletionHelper.Components<DnaModifierComponent>(args[0]),
                "<entidade com DNA>")
            : CompletionResult.Empty;
    }

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteError("Uso: genetics_clear [UID da entidade]");
            return;
        }

        EntityUid? target = shell.Player?.AttachedEntity;
        if (args.Length == 1 && (!_entityManager.TryParseNetEntity(args[0], out target) || !_entityManager.EntityExists(target)))
        {
            shell.WriteError($"A entidade '{args[0]}' não foi encontrada.");
            return;
        }

        if (target is not { Valid: true })
        {
            shell.WriteError("Nenhuma entidade selecionada. Informe um UID.");
            return;
        }

        if (!_entityManager.TryGetComponent<DnaModifierComponent>(target.Value, out var dna))
        {
            shell.WriteError("A entidade não possui componente de DNA.");
            return;
        }

        var removed = dna.AppliedGenes.Count;
        _entityManager.System<DnaModifierSystem>().ClearAppliedGenes((target.Value, dna));
        shell.WriteLine($"{removed} gene(s) removido(s) de {target.Value}.");
    }
}
