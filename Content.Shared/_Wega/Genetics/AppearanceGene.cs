// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;

namespace Content.Shared.Genetics;

/// <summary>Lists and accesses appearance fields without reflection.</summary>
public static class AppearanceGene
{
    public const string Name = nameof(UniqueIdentifiersData.EntityName);

    private static readonly string[] ColorFields =
    {
        nameof(UniqueIdentifiersData.HairColorR), nameof(UniqueIdentifiersData.HairColorG), nameof(UniqueIdentifiersData.HairColorB),
        nameof(UniqueIdentifiersData.SecondaryHairColorR), nameof(UniqueIdentifiersData.SecondaryHairColorG), nameof(UniqueIdentifiersData.SecondaryHairColorB),
        nameof(UniqueIdentifiersData.BeardColorR), nameof(UniqueIdentifiersData.BeardColorG), nameof(UniqueIdentifiersData.BeardColorB),
        nameof(UniqueIdentifiersData.SkinTone), nameof(UniqueIdentifiersData.FurColorR), nameof(UniqueIdentifiersData.FurColorG), nameof(UniqueIdentifiersData.FurColorB),
        nameof(UniqueIdentifiersData.HeadAccessoryColorR), nameof(UniqueIdentifiersData.HeadAccessoryColorG), nameof(UniqueIdentifiersData.HeadAccessoryColorB),
        nameof(UniqueIdentifiersData.HeadMarkingColorR), nameof(UniqueIdentifiersData.HeadMarkingColorG), nameof(UniqueIdentifiersData.HeadMarkingColorB),
        nameof(UniqueIdentifiersData.BodyMarkingColorR), nameof(UniqueIdentifiersData.BodyMarkingColorG), nameof(UniqueIdentifiersData.BodyMarkingColorB),
        nameof(UniqueIdentifiersData.TailMarkingColorR), nameof(UniqueIdentifiersData.TailMarkingColorG), nameof(UniqueIdentifiersData.TailMarkingColorB),
        nameof(UniqueIdentifiersData.EyeColorR), nameof(UniqueIdentifiersData.EyeColorG), nameof(UniqueIdentifiersData.EyeColorB),
        nameof(UniqueIdentifiersData.Gender), nameof(UniqueIdentifiersData.HairStyle), nameof(UniqueIdentifiersData.BeardStyle),
        nameof(UniqueIdentifiersData.HeadAccessoryStyle), nameof(UniqueIdentifiersData.HeadMarkingStyle), nameof(UniqueIdentifiersData.BodyMarkingStyle),
        nameof(UniqueIdentifiersData.TailMarkingStyle)
    };

    public static IEnumerable<string> Fields => ColorFields.Append(Name);
    public static bool Exists(string field) => field == Name || Array.IndexOf(ColorFields, field) >= 0;

    public static string[]? Get(UniqueIdentifiersData data, string field) => field switch
    {
        nameof(UniqueIdentifiersData.HairColorR) => data.HairColorR,
        nameof(UniqueIdentifiersData.HairColorG) => data.HairColorG,
        nameof(UniqueIdentifiersData.HairColorB) => data.HairColorB,
        nameof(UniqueIdentifiersData.SecondaryHairColorR) => data.SecondaryHairColorR,
        nameof(UniqueIdentifiersData.SecondaryHairColorG) => data.SecondaryHairColorG,
        nameof(UniqueIdentifiersData.SecondaryHairColorB) => data.SecondaryHairColorB,
        nameof(UniqueIdentifiersData.BeardColorR) => data.BeardColorR,
        nameof(UniqueIdentifiersData.BeardColorG) => data.BeardColorG,
        nameof(UniqueIdentifiersData.BeardColorB) => data.BeardColorB,
        nameof(UniqueIdentifiersData.SkinTone) => data.SkinTone,
        nameof(UniqueIdentifiersData.FurColorR) => data.FurColorR,
        nameof(UniqueIdentifiersData.FurColorG) => data.FurColorG,
        nameof(UniqueIdentifiersData.FurColorB) => data.FurColorB,
        nameof(UniqueIdentifiersData.HeadAccessoryColorR) => data.HeadAccessoryColorR,
        nameof(UniqueIdentifiersData.HeadAccessoryColorG) => data.HeadAccessoryColorG,
        nameof(UniqueIdentifiersData.HeadAccessoryColorB) => data.HeadAccessoryColorB,
        nameof(UniqueIdentifiersData.HeadMarkingColorR) => data.HeadMarkingColorR,
        nameof(UniqueIdentifiersData.HeadMarkingColorG) => data.HeadMarkingColorG,
        nameof(UniqueIdentifiersData.HeadMarkingColorB) => data.HeadMarkingColorB,
        nameof(UniqueIdentifiersData.BodyMarkingColorR) => data.BodyMarkingColorR,
        nameof(UniqueIdentifiersData.BodyMarkingColorG) => data.BodyMarkingColorG,
        nameof(UniqueIdentifiersData.BodyMarkingColorB) => data.BodyMarkingColorB,
        nameof(UniqueIdentifiersData.TailMarkingColorR) => data.TailMarkingColorR,
        nameof(UniqueIdentifiersData.TailMarkingColorG) => data.TailMarkingColorG,
        nameof(UniqueIdentifiersData.TailMarkingColorB) => data.TailMarkingColorB,
        nameof(UniqueIdentifiersData.EyeColorR) => data.EyeColorR,
        nameof(UniqueIdentifiersData.EyeColorG) => data.EyeColorG,
        nameof(UniqueIdentifiersData.EyeColorB) => data.EyeColorB,
        nameof(UniqueIdentifiersData.Gender) => data.Gender,
        nameof(UniqueIdentifiersData.HairStyle) => data.HairStyle,
        nameof(UniqueIdentifiersData.BeardStyle) => data.BeardStyle,
        nameof(UniqueIdentifiersData.HeadAccessoryStyle) => data.HeadAccessoryStyle,
        nameof(UniqueIdentifiersData.HeadMarkingStyle) => data.HeadMarkingStyle,
        nameof(UniqueIdentifiersData.BodyMarkingStyle) => data.BodyMarkingStyle,
        nameof(UniqueIdentifiersData.TailMarkingStyle) => data.TailMarkingStyle,
        _ => null
    };

    public static void Set(UniqueIdentifiersData data, string field, string[] value)
    {
        var copy = (string[]) value.Clone();
        switch (field)
        {
            case nameof(UniqueIdentifiersData.HairColorR): data.HairColorR = copy; break;
            case nameof(UniqueIdentifiersData.HairColorG): data.HairColorG = copy; break;
            case nameof(UniqueIdentifiersData.HairColorB): data.HairColorB = copy; break;
            case nameof(UniqueIdentifiersData.SecondaryHairColorR): data.SecondaryHairColorR = copy; break;
            case nameof(UniqueIdentifiersData.SecondaryHairColorG): data.SecondaryHairColorG = copy; break;
            case nameof(UniqueIdentifiersData.SecondaryHairColorB): data.SecondaryHairColorB = copy; break;
            case nameof(UniqueIdentifiersData.BeardColorR): data.BeardColorR = copy; break;
            case nameof(UniqueIdentifiersData.BeardColorG): data.BeardColorG = copy; break;
            case nameof(UniqueIdentifiersData.BeardColorB): data.BeardColorB = copy; break;
            case nameof(UniqueIdentifiersData.SkinTone): data.SkinTone = copy; break;
            case nameof(UniqueIdentifiersData.FurColorR): data.FurColorR = copy; break;
            case nameof(UniqueIdentifiersData.FurColorG): data.FurColorG = copy; break;
            case nameof(UniqueIdentifiersData.FurColorB): data.FurColorB = copy; break;
            case nameof(UniqueIdentifiersData.HeadAccessoryColorR): data.HeadAccessoryColorR = copy; break;
            case nameof(UniqueIdentifiersData.HeadAccessoryColorG): data.HeadAccessoryColorG = copy; break;
            case nameof(UniqueIdentifiersData.HeadAccessoryColorB): data.HeadAccessoryColorB = copy; break;
            case nameof(UniqueIdentifiersData.HeadMarkingColorR): data.HeadMarkingColorR = copy; break;
            case nameof(UniqueIdentifiersData.HeadMarkingColorG): data.HeadMarkingColorG = copy; break;
            case nameof(UniqueIdentifiersData.HeadMarkingColorB): data.HeadMarkingColorB = copy; break;
            case nameof(UniqueIdentifiersData.BodyMarkingColorR): data.BodyMarkingColorR = copy; break;
            case nameof(UniqueIdentifiersData.BodyMarkingColorG): data.BodyMarkingColorG = copy; break;
            case nameof(UniqueIdentifiersData.BodyMarkingColorB): data.BodyMarkingColorB = copy; break;
            case nameof(UniqueIdentifiersData.TailMarkingColorR): data.TailMarkingColorR = copy; break;
            case nameof(UniqueIdentifiersData.TailMarkingColorG): data.TailMarkingColorG = copy; break;
            case nameof(UniqueIdentifiersData.TailMarkingColorB): data.TailMarkingColorB = copy; break;
            case nameof(UniqueIdentifiersData.EyeColorR): data.EyeColorR = copy; break;
            case nameof(UniqueIdentifiersData.EyeColorG): data.EyeColorG = copy; break;
            case nameof(UniqueIdentifiersData.EyeColorB): data.EyeColorB = copy; break;
            case nameof(UniqueIdentifiersData.Gender): data.Gender = copy; break;
            case nameof(UniqueIdentifiersData.HairStyle): data.HairStyle = copy; break;
            case nameof(UniqueIdentifiersData.BeardStyle): data.BeardStyle = copy; break;
            case nameof(UniqueIdentifiersData.HeadAccessoryStyle): data.HeadAccessoryStyle = copy; break;
            case nameof(UniqueIdentifiersData.HeadMarkingStyle): data.HeadMarkingStyle = copy; break;
            case nameof(UniqueIdentifiersData.BodyMarkingStyle): data.BodyMarkingStyle = copy; break;
            case nameof(UniqueIdentifiersData.TailMarkingStyle): data.TailMarkingStyle = copy; break;
        }
    }

    public static bool Copy(UniqueIdentifiersData source, UniqueIdentifiersData target, string field)
    {
        if (field == Name)
        {
            target.EntityName = source.EntityName;
            return true;
        }
        if (Get(source, field) is not { } value)
            return false;
        Set(target, field, value);
        return true;
    }
}
