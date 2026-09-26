// SPDX-License-Identifier: AGPL-3.0-or-later
using System.Linq;
using System.Numerics;
using Content.Client.Humanoid;
using Content.Shared.Genetics;
using Content.Shared.Genetics.UI;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Wega.Genetics.Ui;

public sealed partial class DnaModifierWindow
{
    public event Action<BoundUserInterfaceMessage>? OnGeneticMessage;
    private readonly BoxContainer _uniqueGenes = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly BoxContainer _structuralGenes = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly BoxContainer _appearanceEditor = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly BoxContainer _appearancePuzzle = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly BoxContainer _structuralPuzzle = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private string _geneSignature = string.Empty;
    private string _appearanceSignature = string.Empty;
    private int? _puzzleToken;
    private EntityUid? _appearancePreview;
    private EntityUid? _appearancePreviewSource;

    private void SetScannerPreview(EntityUid source)
    {
        if (_appearancePreview is { } preview && _entManager.EntityExists(preview))
        {
            SubjectPreview.SetEntity(preview);
            return;
        }

        SubjectPreview.SetEntity(source);
    }

    private void ClearScannerPreview()
    {
        if (_appearancePreview is { } preview && _entManager.EntityExists(preview))
            _entManager.DeleteEntity(preview);

        _appearancePreview = null;
        _appearancePreviewSource = null;
    }

    private void InitializeSequencingUi()
    {
        UiContainer.Orientation = BoxContainer.LayoutOrientation.Vertical;
        UiContainer.AddChild(_uniqueGenes);
        UiContainer.AddChild(_appearanceEditor);
        UiContainer.AddChild(_appearancePuzzle);
        SeContainer.Orientation = BoxContainer.LayoutOrientation.Vertical;
        SeContainer.AddChild(_structuralGenes);
        SeContainer.AddChild(_structuralPuzzle);
    }

    private void UpdateSequencingUi(DnaModifierBoundUserInterfaceState state)
    {
        UiPanel.Visible = state.AppearanceFields.Count > 0;
        SePanel.Visible = state.Genes.Count > 0;
        GeneticStatusLabel.Text = state.GeneticStatus;
        var signature = string.Join('|', state.Genes.Select(g => $"{g.Number}:{g.Name}:{g.Discovered}:{g.Active}"));
        if (_geneSignature != signature)
        {
            _geneSignature = signature;
            _structuralGenes.RemoveAllChildren();
            var grid = new GridContainer { Columns = 5, HorizontalExpand = true };
            foreach (var gene in state.Genes)
            {
                var row = new BoxContainer { Margin = new Thickness(1) };
                row.AddChild(new Label { Text = gene.Number.ToString(), MinWidth = 18 });
                var name = new GeneNameButton(gene.Name) { ToolTip = gene.Name, SetWidth = 110 };
                name.OnPressed += _ => OnGeneticMessage?.Invoke(new GeneticSelectMessage(gene.Number));
                row.AddChild(name);
                var toggle = new Button
                {
                    Text = Loc.GetString(gene.Active ? "dna-gene-on" : "dna-gene-off"),
                    Disabled = !gene.Discovered,
                    ModulateSelfOverride = gene.Active ? Color.FromHex("#40C56C") : Color.FromHex("#E05A5A"),
                    MinWidth = 50,
                    SetWidth = 50
                };
                toggle.OnPressed += _ => OnGeneticMessage?.Invoke(new GeneticToggleMessage(gene.Number));
                row.AddChild(toggle);
                grid.AddChild(row);
            }
            _structuralGenes.AddChild(grid);
            UpdateCombiner(state);
        }
        var appearanceSignature = string.Join('|', state.AppearanceFields) + state.ScannerSpecies;
        if (_appearanceSignature != appearanceSignature)
        {
            _appearanceSignature = appearanceSignature;
            _uniqueGenes.RemoveAllChildren();
            _appearanceEditor.RemoveAllChildren();
            foreach (var group in state.AppearanceFields.GroupBy(AppearanceCategory))
            {
                _uniqueGenes.AddChild(new Label { Text = Loc.GetString("dna-eu-category-" + group.Key), StyleClasses = { "LabelSubText" } });
                BuildAppearanceGroup(_appearanceEditor, group, state);
            }
        }
        if (_puzzleToken == state.Puzzle?.Token)
            return; // Periodic UI updates must not erase the player's answers.
        _puzzleToken = state.Puzzle?.Token;
        _appearancePuzzle.RemoveAllChildren();
        _structuralPuzzle.RemoveAllChildren();
        if (state.Puzzle is { } puzzle)
        {
            var parent = puzzle.Appearance ? _appearancePuzzle : _structuralPuzzle;
            parent.AddChild(BuildPuzzle(puzzle));
            Tabs.CurrentTab = puzzle.Appearance ? 0 : 3;
        }
    }

    private void BuildAppearanceGroup(BoxContainer parent, IEnumerable<string> fields,
        DnaModifierBoundUserInterfaceState state)
    {
        var group = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        foreach (var field in fields)
        {
            var label = Loc.GetString("dna-eu-" + field);
            var row = new BoxContainer { Margin = new Thickness(1), SeparationOverride = 6 };
            row.AddChild(new Label { Text = label, MinWidth = 220, ToolTip = label });

            Func<string> selected;
            if (state.AppearanceOptions.TryGetValue(field, out var markings))
            {
                var list = new ItemList { MinSize = new Vector2(220, 64), MaxSize = new Vector2(320, 96) };
                var selectedValue = markings.Count > 0 ? markings[0] : string.Empty;
                foreach (var id in markings)
                {
                    var item = new ItemList.Item(list) { Text = id.Length == 0 ? Loc.GetString("dna-eu-none") : id, Metadata = id };
                    if (id.Length > 0 && _prototypeManager.TryIndex<MarkingPrototype>(id, out var marking))
                        item.Icon = _entManager.System<SpriteSystem>().Frame0(marking.Sprites[0]);
                    list.Add(item);
                }
                list.OnItemSelected += args =>
                {
                    if (args.ItemList[args.ItemIndex].Metadata is string id)
                    {
                        selectedValue = id;
                        PreviewAppearance(field, id);
                    }
                };
                selected = () => selectedValue;
                row.AddChild(list);
            }
            else if (field == nameof(UniqueIdentifiersData.Gender))
            {
                var options = new OptionButton { MinWidth = 220 };
                options.AddItem(Loc.GetString("dna-eu-female"), 0);
                options.AddItem(Loc.GetString("dna-eu-male"), 1);
                options.AddItem(Loc.GetString("dna-eu-neuter"), 2);
                selected = () => options.SelectedId.ToString();
                options.OnItemSelected += args => PreviewAppearance(field, args.Id.ToString());
                row.AddChild(options);
            }
            else if (field == AppearanceGene.Name)
            {
                var input = new LineEdit { MinWidth = 220 };
                input.Text = state.ScannerBodyInfo ?? string.Empty;
                selected = () => input.Text;
                input.OnTextChanged += args => PreviewAppearance(field, args.Text);
                row.AddChild(input);
            }
            else
            {
                var max = field == nameof(UniqueIdentifiersData.SkinTone) ? 100 : 255;
                var slider = new Slider { MinValue = 0, MaxValue = max, SetWidth = 220 };
                var value = new Label { MinWidth = 35, Text = ReadAppearanceValue(state, field, max).ToString() };
                slider.Value = int.Parse(value.Text);
                slider.OnValueChanged += args =>
                {
                    value.Text = ((int) args.Value).ToString();
                    PreviewAppearance(field, value.Text);
                };
                selected = () => ((int) slider.Value).ToString();
                row.AddChild(slider);
                row.AddChild(value);
            }

            var apply = new Button { Text = Loc.GetString("dna-eu-sequence"), MinWidth = 120 };
            apply.OnPressed += _ =>
            {
                var value = selected();
                PreviewAppearance(field, value);
                OnGeneticMessage?.Invoke(new GeneticAppearanceMessage(field, value));
            };
            row.AddChild(apply);
            group.AddChild(row);
        }
        parent.AddChild(group);
    }

    private void PreviewAppearance(string field, string value)
    {
        if (_lastUpdate?.ScannerBody is not { } netBody || !_entManager.TryGetEntity(netBody, out EntityUid? source) || source is not { } sourceEntity ||
            !_entManager.TryGetComponent<HumanoidAppearanceComponent>(sourceEntity, out var sourceAppearance))
            return;

        EntityUid preview = default;

        if (_appearancePreview is { } existingPreview)
            preview = existingPreview;

        if (!preview.IsValid() || !_entManager.EntityExists(preview) || _appearancePreviewSource != sourceEntity)
        {
            if (preview.IsValid() && _entManager.EntityExists(preview))
                _entManager.DeleteEntity(preview);

            var prototype = _prototypeManager.Index<SpeciesPrototype>(sourceAppearance.Species);
            preview = _entManager.SpawnEntity(prototype.DollPrototype, MapCoordinates.Nullspace);
            _appearancePreview = preview;
            _appearancePreviewSource = sourceEntity;
        }

        if (!_entManager.TryGetComponent<HumanoidAppearanceComponent>(preview, out var appearance) ||
            !_entManager.TryGetComponent<SpriteComponent>(preview, out var sprite))
            return;

        appearance.Species = sourceAppearance.Species;
        appearance.Sex = sourceAppearance.Sex;
        appearance.Gender = sourceAppearance.Gender;
        appearance.SkinColor = sourceAppearance.SkinColor;
        appearance.EyeColor = sourceAppearance.EyeColor;
        appearance.MarkingSet = new MarkingSet(sourceAppearance.MarkingSet);

        if (field.EndsWith("Style") && value.Length > 0 && _prototypeManager.TryIndex<MarkingPrototype>(value, out var marking))
        {
            var category = field switch
            {
                nameof(UniqueIdentifiersData.HairStyle) => MarkingCategories.Hair,
                nameof(UniqueIdentifiersData.BeardStyle) => MarkingCategories.FacialHair,
                nameof(UniqueIdentifiersData.HeadAccessoryStyle) => MarkingCategories.HeadTop,
                nameof(UniqueIdentifiersData.HeadMarkingStyle) => MarkingCategories.Head,
                nameof(UniqueIdentifiersData.BodyMarkingStyle) => MarkingCategories.Chest,
                nameof(UniqueIdentifiersData.TailMarkingStyle) => MarkingCategories.Tail,
                _ => MarkingCategories.Special
            };
            appearance.MarkingSet.RemoveCategory(category);
            appearance.MarkingSet.AddBack(category, marking.AsMarking());
        }
        else if (field == nameof(UniqueIdentifiersData.Gender) && int.TryParse(value, out var gender))
        {
            appearance.Gender = (Gender) Math.Clamp(gender, 0, 2);
            appearance.Sex = gender == 0 ? Sex.Female : gender == 1 ? Sex.Male : Sex.Unsexed;
        }

        ApplyColorPreview(state: _lastUpdate, appearance, field, value);

        _entManager.System<HumanoidAppearanceSystem>().UpdateSprite((preview, appearance, sprite));
        SetScannerPreview(sourceEntity);
    }

    private static void ApplyColorPreview(DnaModifierBoundUserInterfaceState? state,
        HumanoidAppearanceComponent appearance, string field, string value)
    {
        if (state?.Unique is null || !int.TryParse(value, out var channel))
            return;

        channel = Math.Clamp(channel, 0, 255);
        if (field is nameof(UniqueIdentifiersData.EyeColorR) or nameof(UniqueIdentifiersData.EyeColorG)
            or nameof(UniqueIdentifiersData.EyeColorB))
        {
            var color = appearance.EyeColor;
            var eyeR = field.EndsWith("R") ? channel : color.RByte;
            var eyeG = field.EndsWith("G") ? channel : color.GByte;
            var eyeB = field.EndsWith("B") ? channel : color.BByte;
            appearance.EyeColor = new Color(eyeR, eyeG, eyeB);
            return;
        }

        var category = field switch
        {
            nameof(UniqueIdentifiersData.HairColorR) or nameof(UniqueIdentifiersData.HairColorG) or nameof(UniqueIdentifiersData.HairColorB)
                => MarkingCategories.Hair,
            nameof(UniqueIdentifiersData.BeardColorR) or nameof(UniqueIdentifiersData.BeardColorG) or nameof(UniqueIdentifiersData.BeardColorB)
                => MarkingCategories.FacialHair,
            nameof(UniqueIdentifiersData.HeadAccessoryColorR) or nameof(UniqueIdentifiersData.HeadAccessoryColorG) or nameof(UniqueIdentifiersData.HeadAccessoryColorB)
                => MarkingCategories.HeadTop,
            nameof(UniqueIdentifiersData.HeadMarkingColorR) or nameof(UniqueIdentifiersData.HeadMarkingColorG) or nameof(UniqueIdentifiersData.HeadMarkingColorB)
                => MarkingCategories.Head,
            nameof(UniqueIdentifiersData.BodyMarkingColorR) or nameof(UniqueIdentifiersData.BodyMarkingColorG) or nameof(UniqueIdentifiersData.BodyMarkingColorB)
                => MarkingCategories.Chest,
            nameof(UniqueIdentifiersData.TailMarkingColorR) or nameof(UniqueIdentifiersData.TailMarkingColorG) or nameof(UniqueIdentifiersData.TailMarkingColorB)
                => MarkingCategories.Tail,
            _ => (MarkingCategories?) null
        };
        if (category is not { } markingCategory || !appearance.MarkingSet.TryGetCategory(markingCategory, out var markings)
            || markings.Count == 0)
            return;

        var marking = markings[0];
        var existing = marking.MarkingColors.FirstOrDefault();
        var markingR = field.EndsWith("R") ? channel : existing.RByte;
        var markingG = field.EndsWith("G") ? channel : existing.GByte;
        var markingB = field.EndsWith("B") ? channel : existing.BByte;
        marking.SetColor(0, new Color(markingR, markingG, markingB));
    }

    private static int ReadAppearanceValue(DnaModifierBoundUserInterfaceState state, string field, int max)
    {
        if (state.Unique is null || AppearanceGene.Get(state.Unique, field) is not { Length: > 0 } value)
            return 0;

        var encoded = string.Concat(value);
        if (field == nameof(UniqueIdentifiersData.SkinTone))
            return Math.Clamp(int.TryParse(encoded, out var decimalValue) ? decimalValue : 0, 0, max);

        return Math.Clamp(int.TryParse(encoded, System.Globalization.NumberStyles.HexNumber, null, out var hexValue)
            ? hexValue
            : 0, 0, max);
    }

    private void ShowAppearanceEditor(string field, DnaModifierBoundUserInterfaceState state)
    {
        _appearanceEditor.RemoveAllChildren();
        _appearanceEditor.AddChild(new Label { Text = Loc.GetString("dna-eu-" + field) });
        var row = new BoxContainer();
        Func<string> selected;
        if (state.AppearanceOptions.TryGetValue(field, out var markings))
        {
            var options = new OptionButton();
            for (var i = 0; i < markings.Count; i++)
            {
                var id = markings[i];
                options.AddItem(id.Length == 0 ? Loc.GetString("dna-eu-none") : id, i);
            }
            options.OnItemSelected += args => options.SelectId(args.Id);
            row.AddChild(options);
            selected = () => markings[options.SelectedId];
        }
        else if (field == nameof(UniqueIdentifiersData.Gender))
        {
            var options = new OptionButton();
            options.AddItem(Loc.GetString("dna-eu-female"), 0);
            options.AddItem(Loc.GetString("dna-eu-male"), 1);
            options.AddItem(Loc.GetString("dna-eu-neuter"), 2);
            options.OnItemSelected += args => options.SelectId(args.Id);
            row.AddChild(options);
            selected = () => options.SelectedId.ToString();
        }
        else
        {
            var input = new LineEdit { MinWidth = 200 };
            input.PlaceHolder = field == AppearanceGene.Name ? state.ScannerBodyInfo ?? string.Empty :
                field == nameof(UniqueIdentifiersData.SkinTone) ? "0–100" : "0–255";
            row.AddChild(input);
            selected = () => input.Text;
        }
        var apply = new Button { Text = Loc.GetString("dna-eu-sequence") };
        apply.OnPressed += _ => OnGeneticMessage?.Invoke(new GeneticAppearanceMessage(field, selected()));
        row.AddChild(apply);
        _appearanceEditor.AddChild(row);
    }

    private static string AppearanceCategory(string field)
    {
        if (field == AppearanceGene.Name || field == nameof(UniqueIdentifiersData.Gender))
            return "identity";
        if (field.EndsWith("Style"))
            return "markings";
        if (field.Contains("Hair") || field.Contains("Beard"))
            return "hair";
        if (field.Contains("Eye") || field.Contains("Skin") || field.Contains("Fur"))
            return "colors";
        return "details";
    }

    // Trauma's two strands, fixed known bases, cycling unknown bases, submit and reset.
    private BoxContainer BuildPuzzle(GeneticPuzzleState puzzle)
    {
        var container = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        container.AddChild(new Label { Text = puzzle.Title });
        var answer = puzzle.Original.ToCharArray();
        var grid = new GridContainer { Columns = answer.Length / 2 };
        var submit = new Button { Text = Loc.GetString("dna-sequence-submit"), Disabled = true };
        var buttons = new List<Button>();
        for (var i = 0; i < answer.Length; i++)
        {
            var index = i;
            var button = new Button { Text = answer[i].ToString(), Disabled = puzzle.Original[i] != 'X', MinWidth = 32 };
            void Cycle(int direction)
            {
                const string bases = "XACGT";
                answer[index] = bases[(bases.IndexOf(answer[index]) + direction + bases.Length) % bases.Length];
                button.Text = answer[index].ToString();
                button.ModulateSelfOverride = answer[index] switch
                {
                    'A' => Color.FromHex("#e45757"),
                    'T' => Color.FromHex("#4f9be8"),
                    'C' => Color.FromHex("#e0b84f"),
                    'G' => Color.FromHex("#55c77a"),
                    _ => Color.White
                };
                submit.Disabled = answer.Contains('X');
            }
            button.OnPressed += _ => Cycle(1);
            button.OnKeyBindDown += args =>
            {
                if (!button.Disabled && args.Function == Robust.Shared.Input.EngineKeyFunctions.UIRightClick)
                    Cycle(-1);
            };
            buttons.Add(button);
            grid.AddChild(button);
        }
        container.AddChild(grid);
        container.AddChild(new Label { Text = Loc.GetString("dna-sequence-instructions") });
        submit.OnPressed += _ =>
        {
            submit.Disabled = true;
            OnGeneticMessage?.Invoke(new GeneticSubmitMessage(puzzle.Token, new string(answer)));
        };
        var reset = new Button { Text = Loc.GetString("dna-sequence-reset") };
        reset.OnPressed += _ =>
        {
            answer = puzzle.Original.ToCharArray();
            for (var i = 0; i < buttons.Count; i++)
            {
                buttons[i].Text = answer[i].ToString();
                buttons[i].ModulateSelfOverride = Color.White;
            }
            submit.Disabled = true;
        };
        container.AddChild(new BoxContainer { Children = { submit, reset } });
        return container;
    }

    private void UpdateCombiner(DnaModifierBoundUserInterfaceState state)
    {
        CombineContainer.RemoveAllChildren();
        CombineContainer.AddChild(new Label { Text = Loc.GetString("dna-combine-instructions") });
        var known = state.Genes.Where(g => g.Discovered).ToList();
        var first = new OptionButton();
        var second = new OptionButton();
        foreach (var gene in known)
        {
            first.AddItem($"{gene.Number} — {gene.Name}", gene.Number);
            second.AddItem($"{gene.Number} — {gene.Name}", gene.Number);
        }
        first.OnItemSelected += args => first.SelectId(args.Id);
        second.OnItemSelected += args => second.SelectId(args.Id);
        var combine = new Button { Text = Loc.GetString("dna-tab-combine"), Disabled = known.Count < 2 };
        combine.OnPressed += _ => OnGeneticMessage?.Invoke(new GeneticCombineMessage(first.SelectedId, second.SelectedId));
        CombineContainer.AddChild(new BoxContainer { Children = { first, second, combine } });
    }

    private sealed class GeneNameButton : Button
    {
        public GeneNameButton(string name)
        {
            Text = name;
            SetWidth = 110;
            MaxWidth = 110;
            ToolTip = name;
        }
    }
}
