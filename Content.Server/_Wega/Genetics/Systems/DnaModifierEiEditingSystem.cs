// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Linq;
using Content.Server.Power.EntitySystems;
using Content.Shared.Genetics;
using Content.Shared.Genetics.Systems;
using Content.Shared.Genetics.UI;
using Robust.Shared.Player;

namespace Content.Server.Genetics.System;

/// <summary>
/// Server-authoritative ATCG engineering tool for appearance regions stored inside an EI.
/// It encodes Wega UniqueIdentifiersData to a complementary double strand and decodes a valid
/// edited strand back into the same Wega appearance data; it is not Trauma sequencing.
/// </summary>
public sealed class DnaModifierEiEditingSystem : EntitySystem
{
    [Dependency] private readonly DnaClientSystem _dnaClient = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    private readonly Dictionary<(EntityUid Console, EntityUid User), EditSession> _sessions = new();

    private sealed class EditSession
    {
        public int BufferIndex;
        public EiAppearanceRegion Region;
        public string ProfileName = string.Empty;
        public char[] Top = Array.Empty<char>();
        public char[] Bottom = Array.Empty<char>();
        public char[] OriginalTop = Array.Empty<char>();
        public char[] OriginalBottom = Array.Empty<char>();
        public int BondBudget;
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<DnaModifierEiEditRequestEvent>(OnEditRequest);
        SubscribeNetworkEvent<DnaModifierEiSetBaseEvent>(OnSetBase);
        SubscribeNetworkEvent<DnaModifierEiResetEditEvent>(OnReset);
        SubscribeNetworkEvent<DnaModifierEiCommitEditEvent>(OnCommit);
    }

    private void OnEditRequest(DnaModifierEiEditRequestEvent args, EntitySessionEventArgs sessionArgs)
    {
        var console = GetEntity(args.Console);
        var session = sessionArgs.SenderSession;
        if (!TryAuthorize(console, session, out var user)
            || !TryGetProfile(console, args.BufferIndex, out var profile)
            || profile.Identifier == null)
        {
            return;
        }

        var available = GetAvailableRegions(profile.Identifier);
        if (available.Count == 0)
        {
            SendEmpty(console, session, args.BufferIndex, profile.GeneticIdentityName ?? profile.SampleName,
                "Este EI não possui regiões de aparência editáveis.");
            return;
        }

        var region = available.Any(option => option.Region == args.Region)
            ? args.Region
            : available[0].Region;

        if (!TryCreateSession(profile, args.BufferIndex, region, out var edit))
        {
            SendEmpty(console, session, args.BufferIndex, profile.GeneticIdentityName ?? profile.SampleName,
                "A região genética não pôde ser codificada.");
            return;
        }

        _sessions[(console, user)] = edit;
        SendState(console, session, edit, null);
    }

    private void OnSetBase(DnaModifierEiSetBaseEvent args, EntitySessionEventArgs sessionArgs)
    {
        var console = GetEntity(args.Console);
        var session = sessionArgs.SenderSession;
        if (!TryAuthorize(console, session, out var user)
            || !_sessions.TryGetValue((console, user), out var edit)
            || args.PairIndex < 0
            || args.PairIndex >= edit.Top.Length
            || args.Base.Length != 1
            || !IsBase(args.Base[0]))
        {
            return;
        }

        var index = args.PairIndex;
        var previous = args.BottomStrand ? edit.Bottom[index] : edit.Top[index];
        if (args.BottomStrand)
            edit.Bottom[index] = args.Base[0];
        else
            edit.Top[index] = args.Base[0];

        if (GetBondCost(edit) > edit.BondBudget)
        {
            if (args.BottomStrand)
                edit.Bottom[index] = previous;
            else
                edit.Top[index] = previous;

            SendState(console, session, edit,
                "Limite de energia excedido. Pares G-C custam 3 ligações para romper; A-T custam 2.");
            return;
        }

        SendState(console, session, edit, null);
    }

    private void OnReset(DnaModifierEiResetEditEvent args, EntitySessionEventArgs sessionArgs)
    {
        var console = GetEntity(args.Console);
        var session = sessionArgs.SenderSession;
        if (!TryAuthorize(console, session, out var user)
            || !_sessions.TryGetValue((console, user), out var edit))
        {
            return;
        }

        edit.Top = (char[]) edit.OriginalTop.Clone();
        edit.Bottom = (char[]) edit.OriginalBottom.Clone();
        SendState(console, session, edit, "Região restaurada ao EI armazenado.");
    }

    private void OnCommit(DnaModifierEiCommitEditEvent args, EntitySessionEventArgs sessionArgs)
    {
        var console = GetEntity(args.Console);
        var session = sessionArgs.SenderSession;
        if (!TryAuthorize(console, session, out var user)
            || !_sessions.TryGetValue((console, user), out var edit)
            || !AllPairsValid(edit)
            || GetBondCost(edit) > edit.BondBudget
            || !TryGetProfile(console, edit.BufferIndex, out var stored)
            || stored.Identifier == null
            || !TryDecode(edit.Top, out var hex))
        {
            return;
        }

        var updated = (EnzymeInfo) stored.Clone();
        if (updated.Identifier == null || !TrySetRegion(updated.Identifier, edit.Region, hex))
        {
            SendState(console, session, edit, "O EI não aceita o tamanho resultante para esta região.");
            return;
        }

        if (!TryComp<DnaClientComponent>(console, out var client)
            || !_dnaClient.TryReplaceBuffer((console, client), edit.BufferIndex, updated))
        {
            SendState(console, session, edit, "Falha ao salvar a edição no armazenamento Wega.");
            return;
        }

        // Rebuild from the committed profile so the new DNA is the baseline for further edits.
        if (TryCreateSession(updated, edit.BufferIndex, edit.Region, out var committed))
        {
            _sessions[(console, user)] = committed;
            SendState(console, session, committed, "EI atualizado. A alteração será aplicada ao organismo quando este EI for utilizado.");
        }
    }

    private bool TryCreateSession(
        EnzymeInfo profile,
        int bufferIndex,
        EiAppearanceRegion region,
        out EditSession edit)
    {
        edit = default!;
        if (profile.Identifier == null
            || GetRegion(profile.Identifier, region) is not { Length: > 0 } hex
            || !TryEncode(hex, out var top, out var bottom))
        {
            return false;
        }

        edit = new EditSession
        {
            BufferIndex = bufferIndex,
            Region = region,
            ProfileName = profile.GeneticIdentityName ?? profile.SampleName,
            Top = top,
            Bottom = bottom,
            OriginalTop = (char[]) top.Clone(),
            OriginalBottom = (char[]) bottom.Clone(),
            // Generic energy budget: two hydrogen bonds per base-pair in the region.
            // Editing G-C consumes more of the same budget than A-T.
            BondBudget = top.Length * 2,
        };
        return true;
    }

    private void SendState(EntityUid console, ICommonSession session, EditSession edit, string? feedback)
    {
        var pairs = new List<EiBasePairState>(edit.Top.Length);
        for (var i = 0; i < edit.Top.Length; i++)
        {
            var bonds = GetHydrogenBonds(edit.Top[i], edit.Bottom[i]);
            pairs.Add(new EiBasePairState
            {
                Index = i,
                Top = edit.Top[i].ToString(),
                Bottom = edit.Bottom[i].ToString(),
                HydrogenBonds = bonds,
                Valid = bonds > 0,
                Changed = edit.Top[i] != edit.OriginalTop[i] || edit.Bottom[i] != edit.OriginalBottom[i],
            });
        }

        var regions = TryGetProfile(console, edit.BufferIndex, out var profile) && profile.Identifier != null
            ? GetAvailableRegions(profile.Identifier)
            : new List<EiAppearanceRegionOption>();

        RaiseNetworkEvent(new DnaModifierEiEditingStateEvent(
            GetNetEntity(console),
            edit.BufferIndex,
            edit.ProfileName,
            edit.Region,
            regions,
            pairs,
            GetBondCost(edit),
            edit.BondBudget,
            AllPairsValid(edit),
            feedback), session);
    }

    private void SendEmpty(
        EntityUid console,
        ICommonSession session,
        int bufferIndex,
        string profileName,
        string feedback)
    {
        RaiseNetworkEvent(new DnaModifierEiEditingStateEvent(
            GetNetEntity(console),
            bufferIndex,
            profileName,
            EiAppearanceRegion.HairColor,
            new(),
            new(),
            0,
            0,
            false,
            feedback), session);
    }

    private bool TryAuthorize(EntityUid console, ICommonSession session, out EntityUid user)
    {
        user = default;
        return _power.IsPowered(console)
            && session.AttachedEntity is { } actor
            && (user = actor).IsValid()
            && TryComp<DnaModifierConsoleComponent>(console, out _)
            && TryComp<DnaClientComponent>(console, out _)
            && _ui.IsUiOpen(console, DnaModifierUiKey.Key, actor);
    }

    private bool TryGetProfile(EntityUid console, int index, out EnzymeInfo profile)
    {
        profile = default!;
        return index is >= 1 and <= 3
            && TryComp<DnaClientComponent>(console, out var client)
            && _dnaClient.TryGetBufferData((console, client), index, out var data)
            && data.IsFullGeneticProfile
            && data.Identifier != null
            && (profile = data) != null;
    }

    private static bool TryEncode(string[] hex, out char[] top, out char[] bottom)
    {
        var topList = new List<char>(hex.Length * 2);
        foreach (var digit in hex)
        {
            if (digit.Length != 1
                || !int.TryParse(digit, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
                || value is < 0 or > 15)
            {
                top = Array.Empty<char>();
                bottom = Array.Empty<char>();
                return false;
            }

            topList.Add(ValueToBase((value >> 2) & 0x3));
            topList.Add(ValueToBase(value & 0x3));
        }

        top = topList.ToArray();
        bottom = top.Select(Complement).ToArray();
        return true;
    }

    private static bool TryDecode(char[] top, out string[] hex)
    {
        if (top.Length == 0 || top.Length % 2 != 0)
        {
            hex = Array.Empty<string>();
            return false;
        }

        hex = new string[top.Length / 2];
        for (var i = 0; i < hex.Length; i++)
        {
            var high = BaseToValue(top[i * 2]);
            var low = BaseToValue(top[i * 2 + 1]);
            if (high < 0 || low < 0)
                return false;

            hex[i] = ((high << 2) | low).ToString("X1", CultureInfo.InvariantCulture);
        }

        return true;
    }

    private static int GetBondCost(EditSession edit)
    {
        var cost = 0;
        for (var i = 0; i < edit.Top.Length; i++)
        {
            if (edit.Top[i] == edit.OriginalTop[i] && edit.Bottom[i] == edit.OriginalBottom[i])
                continue;

            cost += GetHydrogenBonds(edit.OriginalTop[i], edit.OriginalBottom[i]);
        }
        return cost;
    }

    private static bool AllPairsValid(EditSession edit)
    {
        for (var i = 0; i < edit.Top.Length; i++)
        {
            if (GetHydrogenBonds(edit.Top[i], edit.Bottom[i]) == 0)
                return false;
        }
        return true;
    }

    private static int GetHydrogenBonds(char top, char bottom)
    {
        if (Complement(top) != bottom)
            return 0;

        return top is 'A' or 'T' ? 2 : 3;
    }

    private static char Complement(char value)
        => value switch
        {
            'A' => 'T',
            'T' => 'A',
            'G' => 'C',
            'C' => 'G',
            _ => '\0',
        };

    private static bool IsBase(char value) => value is 'A' or 'T' or 'G' or 'C';

    private static char ValueToBase(int value)
        => value switch
        {
            0 => 'A',
            1 => 'T',
            2 => 'G',
            3 => 'C',
            _ => 'A',
        };

    private static int BaseToValue(char value)
        => value switch
        {
            'A' => 0,
            'T' => 1,
            'G' => 2,
            'C' => 3,
            _ => -1,
        };

    private static List<EiAppearanceRegionOption> GetAvailableRegions(UniqueIdentifiersData data)
    {
        var result = new List<EiAppearanceRegionOption>();
        foreach (var region in Enum.GetValues<EiAppearanceRegion>())
        {
            if (GetRegion(data, region) is not { Length: > 0 })
                continue;

            result.Add(new EiAppearanceRegionOption
            {
                Region = region,
                Name = RegionName(region),
            });
        }

        return result;
    }

    private static string[] GetRegion(UniqueIdentifiersData data, EiAppearanceRegion region)
        => region switch
        {
            EiAppearanceRegion.HairColor => Combine(data.HairColorR, data.HairColorG, data.HairColorB),
            EiAppearanceRegion.SecondaryHairColor => Combine(data.SecondaryHairColorR, data.SecondaryHairColorG, data.SecondaryHairColorB),
            EiAppearanceRegion.BeardColor => Combine(data.BeardColorR, data.BeardColorG, data.BeardColorB),
            EiAppearanceRegion.SkinTone => data.SkinTone.ToArray(),
            EiAppearanceRegion.FurColor => Combine(data.FurColorR, data.FurColorG, data.FurColorB),
            EiAppearanceRegion.HeadAccessoryColor => Combine(data.HeadAccessoryColorR, data.HeadAccessoryColorG, data.HeadAccessoryColorB),
            EiAppearanceRegion.HeadMarkingColor => Combine(data.HeadMarkingColorR, data.HeadMarkingColorG, data.HeadMarkingColorB),
            EiAppearanceRegion.BodyMarkingColor => Combine(data.BodyMarkingColorR, data.BodyMarkingColorG, data.BodyMarkingColorB),
            EiAppearanceRegion.TailMarkingColor => Combine(data.TailMarkingColorR, data.TailMarkingColorG, data.TailMarkingColorB),
            EiAppearanceRegion.EyeColor => Combine(data.EyeColorR, data.EyeColorG, data.EyeColorB),
            EiAppearanceRegion.Gender => data.Gender.ToArray(),
            EiAppearanceRegion.BeardStyle => data.BeardStyle.ToArray(),
            EiAppearanceRegion.HairStyle => data.HairStyle.ToArray(),
            EiAppearanceRegion.HeadAccessoryStyle => data.HeadAccessoryStyle.ToArray(),
            EiAppearanceRegion.HeadMarkingStyle => data.HeadMarkingStyle.ToArray(),
            EiAppearanceRegion.BodyMarkingStyle => data.BodyMarkingStyle.ToArray(),
            EiAppearanceRegion.TailMarkingStyle => data.TailMarkingStyle.ToArray(),
            _ => Array.Empty<string>(),
        };

    private static bool TrySetRegion(UniqueIdentifiersData data, EiAppearanceRegion region, string[] value)
    {
        switch (region)
        {
            case EiAppearanceRegion.HairColor:
                return SetTriple(value, ref data.HairColorR, ref data.HairColorG, ref data.HairColorB);
            case EiAppearanceRegion.SecondaryHairColor:
                return SetTriple(value, ref data.SecondaryHairColorR, ref data.SecondaryHairColorG, ref data.SecondaryHairColorB);
            case EiAppearanceRegion.BeardColor:
                return SetTriple(value, ref data.BeardColorR, ref data.BeardColorG, ref data.BeardColorB);
            case EiAppearanceRegion.SkinTone:
                return SetSingle(value, ref data.SkinTone);
            case EiAppearanceRegion.FurColor:
                return SetTriple(value, ref data.FurColorR, ref data.FurColorG, ref data.FurColorB);
            case EiAppearanceRegion.HeadAccessoryColor:
                return SetTriple(value, ref data.HeadAccessoryColorR, ref data.HeadAccessoryColorG, ref data.HeadAccessoryColorB);
            case EiAppearanceRegion.HeadMarkingColor:
                return SetTriple(value, ref data.HeadMarkingColorR, ref data.HeadMarkingColorG, ref data.HeadMarkingColorB);
            case EiAppearanceRegion.BodyMarkingColor:
                return SetTriple(value, ref data.BodyMarkingColorR, ref data.BodyMarkingColorG, ref data.BodyMarkingColorB);
            case EiAppearanceRegion.TailMarkingColor:
                return SetTriple(value, ref data.TailMarkingColorR, ref data.TailMarkingColorG, ref data.TailMarkingColorB);
            case EiAppearanceRegion.EyeColor:
                return SetTriple(value, ref data.EyeColorR, ref data.EyeColorG, ref data.EyeColorB);
            case EiAppearanceRegion.Gender:
                return SetSingle(value, ref data.Gender);
            case EiAppearanceRegion.BeardStyle:
                return SetSingle(value, ref data.BeardStyle);
            case EiAppearanceRegion.HairStyle:
                return SetSingle(value, ref data.HairStyle);
            case EiAppearanceRegion.HeadAccessoryStyle:
                return SetSingle(value, ref data.HeadAccessoryStyle);
            case EiAppearanceRegion.HeadMarkingStyle:
                return SetSingle(value, ref data.HeadMarkingStyle);
            case EiAppearanceRegion.BodyMarkingStyle:
                return SetSingle(value, ref data.BodyMarkingStyle);
            case EiAppearanceRegion.TailMarkingStyle:
                return SetSingle(value, ref data.TailMarkingStyle);
            default:
                return false;
        }
    }

    private static bool SetSingle(string[] source, ref string[] target)
    {
        if (source.Length != target.Length)
            return false;

        target = source.ToArray();
        return true;
    }

    private static bool SetTriple(string[] source, ref string[] first, ref string[] second, ref string[] third)
    {
        var total = first.Length + second.Length + third.Length;
        if (source.Length != total)
            return false;

        var offset = 0;
        first = source.Skip(offset).Take(first.Length).ToArray();
        offset += first.Length;
        second = source.Skip(offset).Take(second.Length).ToArray();
        offset += second.Length;
        third = source.Skip(offset).Take(third.Length).ToArray();
        return true;
    }

    private static string[] Combine(params string[][] arrays)
        => arrays.SelectMany(array => array).ToArray();

    private static string RegionName(EiAppearanceRegion region)
        => region switch
        {
            EiAppearanceRegion.HairColor => "Cor do cabelo",
            EiAppearanceRegion.SecondaryHairColor => "Cor secundária do cabelo",
            EiAppearanceRegion.BeardColor => "Cor da barba",
            EiAppearanceRegion.SkinTone => "Tom de pele",
            EiAppearanceRegion.FurColor => "Cor de pele/pelagem",
            EiAppearanceRegion.HeadAccessoryColor => "Cor do acessório da cabeça",
            EiAppearanceRegion.HeadMarkingColor => "Cor das marcas da cabeça",
            EiAppearanceRegion.BodyMarkingColor => "Cor das marcas do corpo",
            EiAppearanceRegion.TailMarkingColor => "Cor das marcas da cauda",
            EiAppearanceRegion.EyeColor => "Cor dos olhos",
            EiAppearanceRegion.Gender => "Expressão sexual genética",
            EiAppearanceRegion.BeardStyle => "Estilo de barba",
            EiAppearanceRegion.HairStyle => "Estilo de cabelo",
            EiAppearanceRegion.HeadAccessoryStyle => "Acessório da cabeça",
            EiAppearanceRegion.HeadMarkingStyle => "Marcas da cabeça",
            EiAppearanceRegion.BodyMarkingStyle => "Marcas do corpo",
            EiAppearanceRegion.TailMarkingStyle => "Marcas da cauda",
            _ => region.ToString(),
        };
}
