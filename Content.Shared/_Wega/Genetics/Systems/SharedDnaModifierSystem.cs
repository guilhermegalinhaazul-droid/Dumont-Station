using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared.Genetics;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Random;

namespace Content.Shared.Genetics.Systems;

public abstract partial class SharedDnaModifierSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <summary>
    /// Creates a detached genetic profile suitable for carrying with blood
    /// and forensic samples.
    /// </summary>
    public EnzymeInfo? CreateGeneticSample(EntityUid uid)
    {
        if (!TryComp<DnaModifierComponent>(uid, out var modifier))
            return null;

        return new EnzymeInfo
        {
            SampleName = MetaData(uid).EntityName,
            Identifier = modifier.UniqueIdentifiers?.Clone(modifier.UniqueIdentifiers),
            Info = modifier.EnzymesPrototypes?.Select(info => (EnzymesPrototypeInfo) info.Clone()).ToList()
        };
    }

    public void TrySaveInDisk(EntityUid disk, EnzymeInfo enzyme)
    {
        if (!TryComp(disk, out DnaModifierDiskComponent? comp))
            return;

        if (comp.Data != null)
            return;

        comp.Data = (EnzymeInfo)enzyme.Clone();
        if (TryComp(disk, out MetaDataComponent? meta))
            _metaData.SetEntityName(disk, Loc.GetString("dna-disk-name") + " " + $"({enzyme.SampleName})");

        Dirty(disk, comp);
        return;
    }

    public bool TryGetDataFromDisk(EntityUid disk, [NotNullWhen(true)] out EnzymeInfo? data)
    {
        data = null;
        if (!TryComp(disk, out DnaModifierDiskComponent? comp))
            return false;

        if (comp.Data == null)
            return false;

        data = comp.Data;
        return true;
    }

    public bool TryClearDiskData(EntityUid disk)
    {
        if (!TryComp(disk, out DnaModifierDiskComponent? comp))
            return false;

        if (comp.Data == null)
            return false;

        comp.Data = null;
        return true;
    }

    public Color GetFirstMarkingColor(IReadOnlyList<Marking> markings)
    {
        if (markings.Count > 0 && markings[0].MarkingColors.Count > 0)
        {
            return markings[0].MarkingColors[0];
        }
        return Color.White;
    }

    public string[] ConvertColorToHexArray(Color color)
    {
        int r = (int)(color.R * 255);
        int g = (int)(color.G * 255);
        int b = (int)(color.B * 255);

        string rHex = r.ToString("X2");
        string gHex = g.ToString("X2");
        string bHex = b.ToString("X2");

        return new[]
        {
            rHex[0].ToString(),
            rHex[1].ToString(),
            "0",
            gHex[0].ToString(),
            gHex[1].ToString(),
            "0",
            bHex[0].ToString(),
            bHex[1].ToString(),
            "0"
        };
    }

    public string[] ConvertSkinToneToHexArray(Color skinColor)
    {
        // Store the same 0-100 tone used by character customization. Scaling
        // that range to 0-99 made some samples one shade darker and made tone
        // 100 impossible to preserve.
        var tone = Math.Clamp((int)Math.Round(SkinColor.HumanSkinToneFromColor(skinColor),
            MidpointRounding.AwayFromZero), 0, 100);

        return tone == 100
            ? new[] { "1", "0", "0" }
            : new[] { "0", (tone / 10).ToString(), (tone % 10).ToString() };
    }

    public float HumanSkinToneFromColor(Color color) => SkinColor.HumanSkinToneFromColor(color);

    public Color ConvertSkinToneToColor(string[] skinTone)
    {
        Color defaultColor = Color.FromHsv(new Vector4(0.07f, 0.2f, 1f, 1f));
        if (skinTone == null || skinTone.Length != 3)
            return defaultColor;

        try
        {
            bool isMaxTone = skinTone[0] == "1";
            if (!TryParseHexDigit(skinTone[1], out int tens))
                tens = 0;
            if (!TryParseHexDigit(skinTone[2], out int units))
                units = 0;

            int toneValue = isMaxTone ? 100 : Math.Clamp(tens * 10 + units, 0, 99);

            return SkinColor.HumanSkinTone(toneValue);
        }
        catch
        {
            return defaultColor;
        }
    }

    private bool TryParseHexDigit(string digit, out int value)
    {
        value = 0;
        if (string.IsNullOrEmpty(digit) || digit.Length != 1)
            return false;

        char c = digit[0];
        if (char.IsDigit(c))
        {
            value = c - '0';
            return true;
        }
        else if (c >= 'A' && c <= 'F')
        {
            value = 10 + (c - 'A');
            return true;
        }
        else if (c >= 'a' && c <= 'f')
        {
            value = 10 + (c - 'a');
            return true;
        }

        return false;
    }

    public string[] GenerateRandomGenderHexValue(int minHex, int maxHex)
    {
        var value = _random.Next(minHex, maxHex + 1);
        var hexString = value.ToString("X3");
        return new[]
        {
            hexString[0].ToString(),
            hexString[1].ToString(),
            hexString[2].ToString()
        };
    }

    public string[] GenerateTripleHexValues(byte min0, byte max0, byte min1, byte max1, byte min2, byte max2)
    {
        return new[]
        {
            _random.Next(min0, max0).ToString("X1"),
            _random.Next(min1, max1).ToString("X1"),
            _random.Next(min2, max2).ToString("X1")
        };
    }

    public string[] GenerateRandomHexValues()
    {
        return new[]
        {
            _random.Next(0, 16).ToString("X1"),
            _random.Next(0, 16).ToString("X1"),
            _random.Next(0, 16).ToString("X1")
        };
    }

    public string[] GenerateRandomToneValues()
    {
        int toneValue = _random.Next(0, 101);

        int normalizedTone = (int)Math.Round(toneValue / 100f * 99);
        normalizedTone = Math.Clamp(normalizedTone, 0, 99);

        int digit1 = normalizedTone / 10;
        int digit2 = normalizedTone % 10;

        int firstHex = (toneValue == 100) ? 1 : 0;

        return new[]
        {
            firstHex.ToString("X1"),
            digit1.ToString("X1"),
            digit2.ToString("X1")
        };
    }
}

