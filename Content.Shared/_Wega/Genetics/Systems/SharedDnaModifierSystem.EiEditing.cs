// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Genetics.Systems;

public abstract partial class SharedDnaModifierSystem
{
    /// <summary>
    /// Applies a validated EI appearance region through the system that owns write access
    /// to UniqueIdentifiersData.
    /// </summary>
    public bool TrySetUniqueIdentifierRegion(
        UniqueIdentifiersData data,
        EiAppearanceRegion region,
        string[] value)
    {
        switch (region)
        {
            case EiAppearanceRegion.HairColor:
                return SetEiTriple(value, data.HairColorR.Length, data.HairColorG.Length, data.HairColorB.Length,
                    (a, b, c) => { data.HairColorR = a; data.HairColorG = b; data.HairColorB = c; });
            case EiAppearanceRegion.SecondaryHairColor:
                return SetEiTriple(value, data.SecondaryHairColorR.Length, data.SecondaryHairColorG.Length, data.SecondaryHairColorB.Length,
                    (a, b, c) => { data.SecondaryHairColorR = a; data.SecondaryHairColorG = b; data.SecondaryHairColorB = c; });
            case EiAppearanceRegion.BeardColor:
                return SetEiTriple(value, data.BeardColorR.Length, data.BeardColorG.Length, data.BeardColorB.Length,
                    (a, b, c) => { data.BeardColorR = a; data.BeardColorG = b; data.BeardColorB = c; });
            case EiAppearanceRegion.SkinTone:
                return SetEiSingle(value, data.SkinTone.Length, v => data.SkinTone = v);
            case EiAppearanceRegion.FurColor:
                return SetEiTriple(value, data.FurColorR.Length, data.FurColorG.Length, data.FurColorB.Length,
                    (a, b, c) => { data.FurColorR = a; data.FurColorG = b; data.FurColorB = c; });
            case EiAppearanceRegion.HeadAccessoryColor:
                return SetEiTriple(value, data.HeadAccessoryColorR.Length, data.HeadAccessoryColorG.Length, data.HeadAccessoryColorB.Length,
                    (a, b, c) => { data.HeadAccessoryColorR = a; data.HeadAccessoryColorG = b; data.HeadAccessoryColorB = c; });
            case EiAppearanceRegion.HeadMarkingColor:
                return SetEiTriple(value, data.HeadMarkingColorR.Length, data.HeadMarkingColorG.Length, data.HeadMarkingColorB.Length,
                    (a, b, c) => { data.HeadMarkingColorR = a; data.HeadMarkingColorG = b; data.HeadMarkingColorB = c; });
            case EiAppearanceRegion.BodyMarkingColor:
                return SetEiTriple(value, data.BodyMarkingColorR.Length, data.BodyMarkingColorG.Length, data.BodyMarkingColorB.Length,
                    (a, b, c) => { data.BodyMarkingColorR = a; data.BodyMarkingColorG = b; data.BodyMarkingColorB = c; });
            case EiAppearanceRegion.TailMarkingColor:
                return SetEiTriple(value, data.TailMarkingColorR.Length, data.TailMarkingColorG.Length, data.TailMarkingColorB.Length,
                    (a, b, c) => { data.TailMarkingColorR = a; data.TailMarkingColorG = b; data.TailMarkingColorB = c; });
            case EiAppearanceRegion.EyeColor:
                return SetEiTriple(value, data.EyeColorR.Length, data.EyeColorG.Length, data.EyeColorB.Length,
                    (a, b, c) => { data.EyeColorR = a; data.EyeColorG = b; data.EyeColorB = c; });
            case EiAppearanceRegion.Gender:
                return SetEiSingle(value, data.Gender.Length, v => data.Gender = v);
            case EiAppearanceRegion.BeardStyle:
                return SetEiSingle(value, data.BeardStyle.Length, v => data.BeardStyle = v);
            case EiAppearanceRegion.HairStyle:
                return SetEiSingle(value, data.HairStyle.Length, v => data.HairStyle = v);
            case EiAppearanceRegion.HeadAccessoryStyle:
                return SetEiSingle(value, data.HeadAccessoryStyle.Length, v => data.HeadAccessoryStyle = v);
            case EiAppearanceRegion.HeadMarkingStyle:
                return SetEiSingle(value, data.HeadMarkingStyle.Length, v => data.HeadMarkingStyle = v);
            case EiAppearanceRegion.BodyMarkingStyle:
                return SetEiSingle(value, data.BodyMarkingStyle.Length, v => data.BodyMarkingStyle = v);
            case EiAppearanceRegion.TailMarkingStyle:
                return SetEiSingle(value, data.TailMarkingStyle.Length, v => data.TailMarkingStyle = v);
            default:
                return false;
        }
    }

    private static bool SetEiSingle(string[] source, int length, Action<string[]> assign)
    {
        if (source.Length != length)
            return false;

        assign((string[]) source.Clone());
        return true;
    }

    private static bool SetEiTriple(
        string[] source,
        int firstLength,
        int secondLength,
        int thirdLength,
        Action<string[], string[], string[]> assign)
    {
        if (source.Length != firstLength + secondLength + thirdLength)
            return false;

        var first = new string[firstLength];
        var second = new string[secondLength];
        var third = new string[thirdLength];
        Array.Copy(source, 0, first, 0, firstLength);
        Array.Copy(source, firstLength, second, 0, secondLength);
        Array.Copy(source, firstLength + secondLength, third, 0, thirdLength);
        assign(first, second, third);
        return true;
    }
}
