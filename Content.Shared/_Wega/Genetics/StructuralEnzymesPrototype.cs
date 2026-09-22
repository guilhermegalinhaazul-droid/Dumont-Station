using Content.Shared.Genetics.Systems;
using Content.Shared.Damage;
using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Genetics;

[Prototype, Access(typeof(SharedDnaModifierSystem))]
public sealed partial class StructuralEnzymesPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; set; } = string.Empty;

    [DataField] public string Name = string.Empty;
    [DataField] public string Abbreviation = string.Empty;
    [DataField] public int Difficulty = 8;
    [DataField] public HashSet<string> Replaces = new();
    [DataField] public HashSet<string> Conflicts = new();
    [DataField] public HashSet<string> Required = new();
    [DataField] public HashSet<string> Removes = new();

    // Trauma's data-driven passive mutation values, applied through the existing DNA system.
    [DataField] public float MeleeMultiplier = 1f;
    [DataField] public DamageModifierSet? DamageModifiers;
    [DataField] public float MetabolismBonus;
    [DataField] public float BloodRefreshMultiplier = 1f;
    [DataField] public float BleedMultiplier = 1f;
    [DataField] public Vector2 Scale = Vector2.One;
    [DataField] public float ColdOffset;
    [DataField] public float HeatOffset;
    [DataField] public float Shivering = 1f;
    [DataField] public float Sweating = 1f;
    [DataField] public float MetabolismHeat = 1f;
    [DataField] public float HeatRegulation = 1f;
    [DataField] public List<EntProtoId> Actions = new();

    [DataField("message")]
    public string Message { get; set; } = default!;

    [DataField("addComponent")]
    public ComponentRegistry? AddComponent { get; private set; } = default!;

    [DataField("costInstability")]
    public int CostInstability { get; set; } = 0;

    [DataField("chanceAssimilation")]
    public float ChanceAssimilation { get; set; } = 1.0f;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public EnzymesType TypeDeviation = default!;
}

[Serializable, NetSerializable]
public enum EnzymesType : byte
{
    Disease,
    Minor,
    Intermediate,
    Base,
}
