// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Genetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DnaModifierConsoleComponent : Component
{
    public const string ScannerPort = "MedicalScannerSender";

    [ViewVariables]
    public EntityUid? GeneticScanner = null;

    [DataField("maxDistance")]
    public float MaxDistance = 4f;

    public bool GeneticScannerInRange = true;

    public TimeSpan NextUpdate;
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan LastInjectorTime;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan LastSubjectInjectTime;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan InjectorCooldown = TimeSpan.FromSeconds(30);

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan SubjectInjectCooldown = TimeSpan.FromSeconds(30);

    [DataField("clickSound"), ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");

    /// <summary>
    /// Trauma parity: subjects above this genetic-damage threshold cannot be combined.
    /// </summary>
    [DataField]
    public FixedPoint2 MaxGeneticDamage = 90;

    /// <summary>
    /// Trauma parity: time required to complete a mutation combination.
    /// </summary>
    [DataField]
    public TimeSpan CombineDelay = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Trauma parity: damage applied after a successful combination.
    /// </summary>
    [DataField]
    public DamageSpecifier CombineDamage = new()
    {
        DamageDict = new()
        {
            { "Cellular", 10 }
        }
    };
}
