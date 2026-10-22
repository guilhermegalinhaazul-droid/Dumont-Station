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
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Wega.Genetics.Ui;

public sealed partial class DnaModifierWindow
{
    public event Action<BoundUserInterfaceMessage>? OnGeneticMessage;
    private readonly BoxContainer _uniqueGenes = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly BoxContainer _structuralGenes = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly BoxContainer _geneTools = new() { Orientation = BoxContainer.LayoutOrientation.Horizontal };
    // Keep the EU controls aligned with the panel's default left margin.
    private readonly BoxContainer _appearanceEditor = new()
    {
        Orientation = BoxContainer.LayoutOrientation.Vertical,
        Margin = new Thickness(0)
    };
    private readonly BoxContainer _appearancePuzzle = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly BoxContainer _structuralPuzzle = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private string _geneSignature = string.Empty;
    private string _appearanceSignature = string.Empty;
    private int? _puzzleToken;
    private EntityUid? _appearancePreview;
    private EntityUid? _appearancePreviewSource;

    private void SetScannerPreview(EntityUid source)
    {
        if (EnsureAppearancePreview(source) is { } preview)
            SubjectPreview.SetEntity(preview);
        else
            SubjectPreview.SetEntity(source);
    }

    private EntityUid? EnsureAppearancePreview(EntityUid source)
    {
        if (!_entManager.TryGetComponent<HumanoidAppearanceComponent>(source, out var sourceAppearance))
            return null;

        var preview = _appearancePreview ?? default;
        var created = false;
        if (!preview.IsValid() || !_entManager.EntityExists(preview) || _appearancePreviewSource != source)
        {
            if (preview.IsValid() && _entManager.EntityExists(preview))
                _entManager.DeleteEntity(preview);

            var species = _prototypeManager.Index<SpeciesPrototype>(sourceAppearance.Species);
            preview = _entManager.SpawnEntity(species.DollPrototype, MapCoordinates.Nullspace);
            _appearancePreview = preview;
            _appearancePreviewSource = source;
            created = true;
        }

        if (!_entManager.TryGetComponent<HumanoidAppearanceComponent>(preview, out var previewAppearance)
            || !_entManager.TryGetComponent<SpriteComponent>(preview, out var previewSprite))
            return preview;

        // Copy the scanned appearance only when creating a new preview. Re-copying
        // it on every state update would erase unsaved changes made in the editor.
        if (created)
        {
            previewAppearance.Species = sourceAppearance.Species;
            previewAppearance.Sex = sourceAppearance.Sex;
            previewAppearance.Gender = sourceAppearance.Gender;
            previewAppearance.SkinColor = sourceAppearance.SkinColor;
            previewAppearance.EyeColor = sourceAppearance.EyeColor;
            previewAppearance.MarkingSet = new MarkingSet(sourceAppearance.MarkingSet);
            previewAppearance.Height = sourceAppearance.Height;
            previewAppearance.Width = sourceAppearance.Width;
            _entManager.System<HumanoidAppearanceSystem>().UpdateSprite((preview, previewAppearance, previewSprite));
        }
        return preview;
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
        var revealAll = new Button
        {
            Text = "ATIVAR TODOS OS GENES (TESTE)",
            ToolTip = "Revela todos os genes e ativa todos os blocos no corpo escaneado.",
            MinWidth = 220
        };
        revealAll.OnPressed += _ => OnGeneticMessage?.Invoke(new GeneticRevealAllMessage());
        _geneTools.AddChild(revealAll);
        SeContainer.AddChild(_geneTools);
        SeContainer.AddChild(_structuralGenes);
        SeContainer.AddChild(_structuralPuzzle);
    }

    private void UpdateSequencingUi(DnaModifierBoundUserInterfaceState state)
    {
        _geneTools.Visible = !state.Portable;
        _appearanceEditor.Visible = true;
        UiPanel.Visible = state.AppearanceFields.Count > 0;
        SePanel.Visible = state.Genes.Count > 0;
        GeneticStatusLabel.Text = state.GeneticStatus;
        var signature = string.Join("|", state.Genes.Select(g => $"{g.Number}:{g.Name}:{g.Discovered}:{g.Active}"));
        if (_geneSignature != signature)
        {
            _geneSignature = signature;
            _structuralGenes.RemoveAllChildren();
            var grid = new GridContainer { Columns = 5, HorizontalExpand = true };
            foreach (var gene in state.Genes)
            {
                var row = new BoxContainer { Margin = new Thickness(1) };
                // Keep the gene name button directly beside its block number.
                row.AddChild(new Label { Text = $"{gene.Number}", MinWidth = 26 });
                var name = new GeneNameButton(gene.Name) { ToolTip = gene.Name, SetWidth = 105 };
                // Sequencing an unknown gene is a research action; on the
                // portable analyzer it never activates or edits the scanned target.
                name.Disabled = gene.Discovered;
                name.OnPressed += _ => OnGeneticMessage?.Invoke(new GeneticSelectMessage(gene.Number));
                row.AddChild(name);
                var toggle = new Button
                {
                    Text = Loc.GetString(gene.Active ? "dna-gene-on" : "dna-gene-off"),
                    Disabled = state.Portable || !gene.Discovered,
                    ModulateSelfOverride = gene.Active ? new Color(64, 197, 108) : new Color(224, 90, 90),
                    MinWidth = 50,
                    SetWidth = 50,
                    // Keep a small visual gap between the gene name and its
                    // activation toggle without changing either control.
                    Margin = new Thickness(5, 0, 0, 0)
                };
                if (!state.Portable)
                    toggle.OnPressed += _ => OnGeneticMessage?.Invoke(new GeneticToggleMessage(gene.Number));
                row.AddChild(toggle);
                grid.AddChild(row);
            }
            _structuralGenes.AddChild(grid);
            UpdateCombiner(state);
        }
        var appearanceValues = state.Unique is { } unique
            ? string.Join("|", state.AppearanceFields.Select(field =>
                $"{field}:{string.Join(string.Empty, AppearanceGene.Get(unique, field) ?? Array.Empty<string>())}"))
            : string.Empty;
        var appearanceSignature = $"{state.ScannerBody}:{state.ScannerSpecies}:{appearanceValues}";
        if (_appearanceSignature != appearanceSignature)
        {
            _appearanceSignature = appearanceSignature;
            _uniqueGenes.RemoveAllChildren();
            _appearanceEditor.RemoveAllChildren();
            foreach (var group in state.AppearanceFields.GroupBy(AppearanceCategory))
            {
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
            Tabs.CurrentTab = puzzle.Appearance ? 0 : 1;
        }
    }

    private void BuildAppearanceGroup(BoxContainer parent, IEnumerable<string> fields,
        DnaModifierBoundUserInterfaceState state)
    {
        const float sequenceButtonWidth = 180f;
        // Keep the slider rows at the same height as the sequence button. If
        // the controls are allowed to measure independently, a wrapped button
        // label can make only that row taller and stretch its slider.
        const float appearanceControlHeight = 32f;
        var group = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true
        };
        var colorControls = new Dictionary<string, Slider>();
        var colorGroups = new HashSet<string>(StringComparer.Ordinal);

        void RefreshColorGroup(string field)
        {
            if (!IsRgbColorField(field))
                return;

            var prefix = field.Substring(0, field.Length - 1);
            int Channel(char channel)
            {
                var channelField = prefix + channel;
                return colorControls.TryGetValue(channelField, out var slider)
                    ? (int) slider.Value
                    : ReadAppearanceValue(state, channelField, 255);
            }

            var color = new Color((byte) Channel('R'), (byte) Channel('G'), (byte) Channel('B'));
            foreach (var pair in colorControls)
            {
                if (!pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                // Keep the original slider style intact and only recolor its
                // existing green fill region.
                if (pair.Value is GeneColorSlider colorSlider)
                    colorSlider.SetFillColor(color);
            }
        }

        foreach (var field in fields)
        {
            var label = Loc.GetString("dna-eu-" + field);
            var row = new BoxContainer
            {
                Margin = new Thickness(1),
                SeparationOverride = 6,
                HorizontalExpand = true
            };
            // Keep every row on the same four columns. MinWidth alone lets
            // long labels grow and pushes the slider/button columns sideways.
            row.AddChild(new Label
            {
                Text = label,
                // Keep the full characteristic title inside the panel so
                // its first letters are not clipped by the row container.
                Margin = new Thickness(0, 0, 0, 0),
                MinWidth = 220,
                MaxWidth = 220,
                SetWidth = 220,
                ToolTip = label
            });

            Func<string> selected;
            if (state.AppearanceOptions.TryGetValue(field, out var markings))
            {
                var list = new ItemList
                {
                    MinSize = new Vector2(220, 64),
                    MaxSize = new Vector2(220, 96),
                    Margin = new Thickness(15, 0, 0, 0),
                    MouseFilter = _portableMode ? MouseFilterMode.Ignore : MouseFilterMode.Pass
                };
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
                row.AddChild(CreateAppearanceValueSpacer(appearanceControlHeight));
            }
            else if (field == nameof(UniqueIdentifiersData.Gender))
            {
                var options = new OptionButton
                {
                    MinWidth = 220,
                    MaxWidth = 220,
                    SetWidth = 220,
                    Margin = new Thickness(15, 0, 0, 0),
                    Disabled = _portableMode
                };
                options.AddItem(Loc.GetString("dna-eu-female"), 0);
                options.AddItem(Loc.GetString("dna-eu-male"), 1);
                options.AddItem(Loc.GetString("dna-eu-neuter"), 2);
                // OptionButton does not select an item when its popup entry is
                // pressed. Initialize and update it explicitly so the label
                // always reflects the entity's stored sex and the user's
                // current choice.
                options.SelectId(ReadAppearanceValue(state, field, 2));
                selected = () => options.SelectedId.ToString();
                options.OnItemSelected += args =>
                {
                    options.SelectId(args.Id);
                    PreviewAppearance(field, args.Id.ToString());
                };
                row.AddChild(options);
                row.AddChild(CreateAppearanceValueSpacer(appearanceControlHeight));
            }
            else if (field == AppearanceGene.Name)
            {
                var input = new LineEdit
                {
                    MinWidth = 220,
                    MaxWidth = 220,
                    SetWidth = 220,
                    Margin = new Thickness(15, 0, 0, 0),
                    Editable = !_portableMode
                };
                input.Text = state.ScannerBodyInfo ?? string.Empty;
                selected = () => input.Text;
                input.OnTextChanged += args => PreviewAppearance(field, args.Text);
                row.AddChild(input);
                row.AddChild(CreateAppearanceValueSpacer(appearanceControlHeight));
            }
            else
            {
                var isScale = field is nameof(UniqueIdentifiersData.Height) or nameof(UniqueIdentifiersData.Width);
                var max = field == nameof(UniqueIdentifiersData.SkinTone) ? 100 : isScale ? 200 : 255;
                var min = isScale ? 50 : 0;
                var slider = new GeneColorSlider
                {
                    MinValue = min,
                    MaxValue = max,
                    MinWidth = 220,
                    MaxWidth = 220,
                    SetWidth = 220,
                    MinHeight = appearanceControlHeight,
                    MaxHeight = appearanceControlHeight,
                    SetHeight = appearanceControlHeight,
                    Margin = new Thickness(15, 0, 0, 0),
                    Disabled = _portableMode,
                };
                var value = new Label
                {
                    MinWidth = 35,
                    MaxWidth = 35,
                    SetWidth = 35,
                    Text = ReadAppearanceValue(state, field, max).ToString()
                };
                slider.Value = int.Parse(value.Text);
                if (IsRgbColorField(field))
                    colorControls[field] = slider;
                else if (field == nameof(UniqueIdentifiersData.SkinTone))
                    slider.SetFillColor(SkinToneToColor((int) slider.Value));
                slider.OnValueChanged += args =>
                {
                    value.Text = ((int) args.Value).ToString();
                    if (IsRgbColorField(field))
                        RefreshColorGroup(field);
                    else if (field == nameof(UniqueIdentifiersData.SkinTone))
                        slider.SetFillColor(SkinToneToColor((int) args.Value));
                    PreviewAppearance(field, value.Text);
                };
                selected = () => ((int) slider.Value).ToString();
                row.AddChild(slider);
                row.AddChild(value);
            }

            if (!IsRgbColorField(field) || colorGroups.Add(field[..^1]))
            {
                var apply = new Button
                {
                    Text = Loc.GetString("dna-eu-sequence"),
                    MinWidth = sequenceButtonWidth,
                    MaxWidth = sequenceButtonWidth,
                    SetWidth = sequenceButtonWidth,
                    MinHeight = appearanceControlHeight,
                    MaxHeight = appearanceControlHeight,
                    SetHeight = appearanceControlHeight,
                    // Keep the sequence control separated from the value
                    // column without compressing the characteristic label.
                    // Shift only the EU action-button column to the right.
                    Margin = new Thickness(20, 0, 0, 0),
                    Disabled = _portableMode
                };
                if (IsRgbColorField(field))
                {
                    var prefix = field[..^1];
                    apply.OnPressed += _ =>
                    {
                        var fieldsToSequence = new[] { prefix + "R", prefix + "G", prefix + "B" }
                            .Where(colorControls.ContainsKey)
                            .ToArray();
                        var valuesToSequence = fieldsToSequence
                            .Select(channel => ((int) colorControls[channel].Value).ToString())
                            .ToArray();
                        OnGeneticMessage?.Invoke(new GeneticAppearanceGroupMessage(fieldsToSequence, valuesToSequence));
                    };
                }
                else
                {
                    apply.OnPressed += _ =>
                    {
                        var selectedValue = selected();
                        PreviewAppearance(field, selectedValue);
                        OnGeneticMessage?.Invoke(new GeneticAppearanceMessage(field, selectedValue));
                    };
                }
                row.AddChild(apply);
            }
            else
            {
                // Keep the same column width on RGB rows whose channels share
                // the single sequence button from the first channel.
                row.AddChild(new Control
                {
                    MinWidth = sequenceButtonWidth,
                    MaxWidth = sequenceButtonWidth,
                    SetWidth = sequenceButtonWidth,
                    MinHeight = appearanceControlHeight,
                    MaxHeight = appearanceControlHeight,
                    SetHeight = appearanceControlHeight,
                    Margin = new Thickness(20, 0, 0, 0)
                });
            }
            group.AddChild(row);
        }

        foreach (var field in colorControls.Keys.ToArray())
            RefreshColorGroup(field);

        parent.AddChild(group);
    }

    private static Control CreateAppearanceValueSpacer(float height)
    {
        return new Control
        {
            MinWidth = 35,
            MaxWidth = 35,
            SetWidth = 35,
            MinHeight = height,
            MaxHeight = height,
            SetHeight = height
        };
    }

    private void PreviewAppearance(string field, string value)
    {
        if (_lastUpdate?.ScannerBody is not { } netBody || !_entManager.TryGetEntity(netBody, out EntityUid? source) || source is not { } sourceEntity ||
            !_entManager.EntityExists(sourceEntity))
            return;

        var preview = EnsureAppearancePreview(sourceEntity);
        if (preview is not { } previewEntity)
            return;

        if (!_entManager.TryGetComponent<HumanoidAppearanceComponent>(previewEntity, out var appearance) ||
            !_entManager.TryGetComponent<SpriteComponent>(previewEntity, out var sprite))
            return;

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
        else if (field is nameof(UniqueIdentifiersData.Height) or nameof(UniqueIdentifiersData.Width)
                 && int.TryParse(value, out var scale))
        {
            var factor = Math.Clamp(scale / 100f, 0.5f, 2f);
            if (field == nameof(UniqueIdentifiersData.Height))
                appearance.Height = factor;
            else
                appearance.Width = factor;
        }

        ApplyColorPreview(state: _lastUpdate, appearance, field, value);

        _entManager.System<HumanoidAppearanceSystem>().UpdateSprite((previewEntity, appearance, sprite));
        SubjectPreview.SetEntity(previewEntity);
    }

    private static void ApplyColorPreview(DnaModifierBoundUserInterfaceState? state,
        HumanoidAppearanceComponent appearance, string field, string value)
    {
        if (state?.Unique is null || !int.TryParse(value, out var channel))
            return;

        if (field == nameof(UniqueIdentifiersData.SkinTone))
        {
            appearance.SkinColor = SkinToneToColor(Math.Clamp(channel, 0, 100));
            return;
        }

        channel = Math.Clamp(channel, 0, 255);
        if (field is nameof(UniqueIdentifiersData.EyeColorR) or nameof(UniqueIdentifiersData.EyeColorG)
            or nameof(UniqueIdentifiersData.EyeColorB))
        {
            var color = appearance.EyeColor;
            var eyeR = field.EndsWith("R") ? channel : color.RByte;
            var eyeG = field.EndsWith("G") ? channel : color.GByte;
            var eyeB = field.EndsWith("B") ? channel : color.BByte;
            appearance.EyeColor = new Color((byte) eyeR, (byte) eyeG, (byte) eyeB);
            return;
        }

        if (field is nameof(UniqueIdentifiersData.FurColorR) or nameof(UniqueIdentifiersData.FurColorG)
            or nameof(UniqueIdentifiersData.FurColorB))
        {
            var color = appearance.SkinColor;
            var furR = field.EndsWith("R") ? channel : color.RByte;
            var furG = field.EndsWith("G") ? channel : color.GByte;
            var furB = field.EndsWith("B") ? channel : color.BByte;
            appearance.SkinColor = new Color((byte) furR, (byte) furG, (byte) furB);
            return;
        }

        var category = field switch
        {
            nameof(UniqueIdentifiersData.HairColorR) or nameof(UniqueIdentifiersData.HairColorG) or nameof(UniqueIdentifiersData.HairColorB)
                => MarkingCategories.Hair,
            nameof(UniqueIdentifiersData.SecondaryHairColorR) or nameof(UniqueIdentifiersData.SecondaryHairColorG) or nameof(UniqueIdentifiersData.SecondaryHairColorB)
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
        var colorIndex = field.StartsWith("SecondaryHairColor", StringComparison.Ordinal) ? 1 : 0;
        if (colorIndex >= marking.MarkingColors.Count)
        {
            colorIndex = 0;
        }
        var existing = marking.MarkingColors[colorIndex];
        var markingR = field.EndsWith("R") ? channel : existing.RByte;
        var markingG = field.EndsWith("G") ? channel : existing.GByte;
        var markingB = field.EndsWith("B") ? channel : existing.BByte;
        marking.SetColor(colorIndex, new Color((byte) markingR, (byte) markingG, (byte) markingB));
    }

    private static int ReadAppearanceValue(DnaModifierBoundUserInterfaceState state, string field, int max)
    {
        if (state.Unique is null || AppearanceGene.Get(state.Unique, field) is not { Length: > 0 } value)
            return 0;

        var encoded = value[0];
        if (field == nameof(UniqueIdentifiersData.Gender))
        {
            if (value.Length < 3)
                return 0;

            var gender = (TryHexDigit(value[0]), TryHexDigit(value[1]), TryHexDigit(value[2]));
            return gender switch
            {
                ( <= 0x5, <= 0x7, <= 0x3) => 0,
                ( < 0x8, <= 0x7, < 0x9) => 1,
                ( >= 0x8, >= 0x7, >= 0x9) => 2,
                _ => 0
            };
        }

        if (field is nameof(UniqueIdentifiersData.SkinTone)
            or nameof(UniqueIdentifiersData.Height) or nameof(UniqueIdentifiersData.Width))
        {
            if (field == nameof(UniqueIdentifiersData.SkinTone) && value.Length >= 3)
            {
                var tone = value[0] == "1"
                    ? 100
                    : TryHexDigit(value[1]) * 10 + TryHexDigit(value[2]);
                return Math.Clamp(tone, 0, max);
            }

            return Math.Clamp(int.TryParse(encoded, out var decimalValue) ? decimalValue : 0, 0, max);
        }

        // RGB channels are stored as three hexadecimal nibbles (the first two
        // represent the 0-255 channel value, the third is reserved metadata).
        var hex = value.Length >= 2 ? string.Concat(value[0], value[1]) : encoded;
        return Math.Clamp(int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var hexValue)
            ? hexValue
            : 0, 0, max);
    }

    private static bool IsColorField(string field)
        => field.Contains("Color", StringComparison.Ordinal) || field == nameof(UniqueIdentifiersData.SkinTone);

    private static bool IsRgbColorField(string field)
        => IsColorField(field) && field.Length > 0 &&
           field[^1] is 'R' or 'G' or 'B' && field != nameof(UniqueIdentifiersData.SkinTone);

    private static int TryHexDigit(string value)
        => int.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var result)
            ? result
            : 0;

    private static Color SkinToneToColor(int toneValue)
        => SkinColor.HumanSkinTone(Math.Clamp(toneValue, 0, 100));

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
        var submit = new Button { Text = Loc.GetString("dna-sequence-submit"), Disabled = true, Visible = !puzzle.ReadOnly };
        var buttons = new List<Button>();
        for (var i = 0; i < answer.Length; i++)
        {
            var index = i;
            var button = new Button { Text = answer[i].ToString(), Disabled = puzzle.ReadOnly || puzzle.Original[i] != 'X', MinWidth = 32 };
            void UpdateButtonColor()
            {
                button.ModulateSelfOverride = answer[index] switch
                {
                    'A' or 'T' => new Color(27, 150, 56),
                    'G' or 'C' => new Color(28, 113, 177),
                    _ => null
                };
            }
            void Cycle(int direction)
            {
                const string bases = "XACGT";
                answer[index] = bases[(bases.IndexOf(answer[index]) + direction + bases.Length) % bases.Length];
                button.Text = answer[index].ToString();
                UpdateButtonColor();
                submit.Disabled = answer.Contains('X');
            }
            UpdateButtonColor();
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
        var reset = new Button { Text = Loc.GetString("dna-sequence-reset"), Visible = !puzzle.ReadOnly };
        reset.OnPressed += _ =>
        {
            // Keep bases already filled by the player; only unfinished gaps remain X.
            for (var i = 0; i < buttons.Count; i++)
            {
                buttons[i].Text = answer[i].ToString();
                buttons[i].ModulateSelfOverride = answer[i] switch
                {
                    'A' or 'T' => new Color(27, 150, 56),
                    'G' or 'C' => new Color(28, 113, 177),
                    _ => null
                };
            }
            submit.Disabled = answer.Contains('X');
        };
        if (!puzzle.ReadOnly)
            container.AddChild(new BoxContainer { Children = { submit, reset } });
        return container;
    }

    private void UpdateCombiner(DnaModifierBoundUserInterfaceState state)
    {
        CombineContainer.RemoveAllChildren();

        var title = new Label
        {
            Text = Loc.GetString("dna-combine-title"),
            StyleClasses = { "LabelBig" }
        };
        CombineContainer.AddChild(title);
        CombineContainer.AddChild(new Label
        {
            Text = Loc.GetString("dna-combine-instructions"),
            StyleClasses = { "LabelSubText" },
            HorizontalExpand = true
        });

        var known = state.Genes.Where(g => g.Discovered).ToList();
        if (known.Count < 2)
        {
            // Do not present empty selectors before the round has revealed
            // enough genes to make a combination possible.
            CombineContainer.AddChild(new Label
            {
                Text = Loc.GetString("dna-combine-requires-discovered"),
                StyleClasses = { "LabelSubText" }
            });
            return;
        }

        var first = new OptionButton();
        var second = new OptionButton();
        first.MinWidth = 250;
        second.MinWidth = 250;
        foreach (var gene in known)
        {
            first.AddItem($"{gene.Number} — {gene.Name}", gene.Number);
            second.AddItem($"{gene.Number} — {gene.Name}", gene.Number);
        }
        first.OnItemSelected += args => first.SelectId(args.Id);
        second.OnItemSelected += args => second.SelectId(args.Id);

        var firstColumn = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        firstColumn.AddChild(new Label { Text = Loc.GetString("dna-combine-first") });
        firstColumn.AddChild(first);
        var secondColumn = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        secondColumn.AddChild(new Label { Text = Loc.GetString("dna-combine-second") });
        secondColumn.AddChild(second);

        var combine = new Button
        {
            Text = Loc.GetString("dna-combine-action"),
            Disabled = known.Count < 2,
            MinWidth = 170,
            VerticalAlignment = VAlignment.Center
        };
        combine.OnPressed += _ => OnGeneticMessage?.Invoke(new GeneticCombineMessage(first.SelectedId, second.SelectedId));

        var controls = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 12,
            HorizontalExpand = true
        };
        controls.AddChild(firstColumn);
        controls.AddChild(secondColumn);
        controls.AddChild(combine);
        CombineContainer.AddChild(controls);
    }

    private sealed class GeneColorSlider : Slider
    {
        private Color? _fillColor;

        public void SetFillColor(Color color)
        {
            _fillColor = color;
            ApplyFillColor();
        }

        protected override void StylePropertiesChanged()
        {
            base.StylePropertiesChanged();
            ApplyFillColor();
        }

        private void ApplyFillColor()
        {
            if (_fillColor is not { } color ||
                !TryGetStyleProperty<StyleBox>(StylePropertyFill, out var style) ||
                style is not StyleBoxTexture texture)
                return;

            // Clone the stylesheet texture style so texture, margins, borders,
            // sizing and every other original property remain unchanged.
            FillStyleBoxOverride = new StyleBoxTexture(texture) { Modulate = color };
        }
    }

    private sealed class GeneNameButton : Button
    {
        private const float NameButtonWidth = 105f;
        private const float ScrollSpeed = 20f;
        private const float PauseAtEdge = 0.25f;
        // Keep the text viewport a fraction of a pixel inside the button
        // content box while retaining the original button dimensions.
        // The viewport itself clips every glyph, including antialiased accents.
        private const float SafetyMargin = 0.2f;

        private float _scrollOffset;
        private float _pause;
        private int _direction = -1;
        private float _labelOriginX;
        private float _textWidth;
        private float _overflow;
        private float _lastViewportWidth;
        private bool _metricsReady;
        private bool _hovered;

        public GeneNameButton(string name)
        {
            Text = name;
            SetWidth = NameButtonWidth;
            MaxWidth = NameButtonWidth;
            ToolTip = name;

            // Keep the original button style and clip the original label
            // directly through the button's content bounds.
            RectClipContent = true;
            // Let the button's content clipping be the final boundary. The
            // Label control's own ClipText mode can measure its content as
            // empty in this dynamic layout, hiding both gene names and ???.
            Label.ClipText = false;
            Label.HorizontalAlignment = HAlignment.Left;
            Label.HorizontalExpand = false;
            Label.Measure(Vector2Helpers.Infinity);
            _textWidth = Label.DesiredSize.X;

            Label.Visible = true;
            OnResized += RefreshMetrics;

            OnMouseEntered += _ =>
            {
                _hovered = true;
                RefreshMetrics();
                ResetScroll();
                if (_overflow > 0f)
                    _pause = PauseAtEdge;
            };
            OnMouseExited += _ =>
            {
                _hovered = false;
                ResetScroll();
            };
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            RefreshMetrics();

            // Long names remain aligned to the left while idle and only
            // scroll when the pointer is over this button.
            if (!_hovered || _overflow <= 0f)
            {
                ResetScroll();
                return;
            }

            if (_pause > 0f)
            {
                _pause -= args.DeltaSeconds;
            }
            else
            {
                _scrollOffset += _direction * ScrollSpeed * args.DeltaSeconds;
                if (_scrollOffset <= -_overflow)
                {
                    _scrollOffset = -_overflow;
                    _direction = 1;
                    _pause = PauseAtEdge;
                }
                else if (_scrollOffset >= 0f)
                {
                    _scrollOffset = 0f;
                    _direction = -1;
                    _pause = PauseAtEdge;
                }
            }

            SetLabelPosition();
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            var arranged = base.ArrangeOverride(finalSize);

            // ContainerButton normally arranges every child to the content
            // width. That makes a long label itself as narrow as the button,
            // so centering its text centers only the clipped prefix. Give the
            // label its measured width, then let the button's clip rectangle
            // provide the visible viewport for both short and long names.
            Label.Measure(Vector2Helpers.Infinity);
            var textWidth = Label.DesiredSize.X;
            if (textWidth <= 0f)
                return arranged;

            if (Label.MinWidth < textWidth)
                Label.MinWidth = textWidth;

            // ContainerButton initially measures its label using the button's
            // narrow content width. Preserve the full measured text width so
            // long names still have a real overflow distance to animate.
            var contentBox = GetButtonContentBox();
            var viewportWidth = MathF.Max(1f, contentBox.Width - SafetyMargin * 2f);

            // ArrangeOverride is the first reliable point at which the button
            // has its final size. Initialize the metrics here so names are
            // visible immediately, instead of waiting for mouse hover to run
            // RefreshMetrics.
            if (!_metricsReady || Math.Abs(viewportWidth - _lastViewportWidth) > 0.5f)
            {
                var overflow = MathF.Max(0f, textWidth - viewportWidth);
                _labelOriginX = GetTextOrigin(contentBox.Left, viewportWidth, textWidth, overflow);
                _scrollOffset = 0f;
                _direction = -1;
                _pause = overflow > 0f ? PauseAtEdge : 0f;
            }

            _textWidth = textWidth;
            _lastViewportWidth = viewportWidth;
            _overflow = MathF.Max(0f, textWidth - viewportWidth);
            _metricsReady = true;
            var origin = _metricsReady
                ? _labelOriginX
                : GetTextOrigin(contentBox.Left, viewportWidth, textWidth, _overflow);
            Label.Arrange(UIBox2.FromDimensions(
                new Vector2(origin, contentBox.Top),
                new Vector2(textWidth, contentBox.Height)));
            // Arrange() establishes the child bounds, while the layout
            // helper applies the actual position used by the renderer. Do
            // this during the initial layout as well as on hover so the name
            // is visible before the pointer enters the button.
            SetLabelPosition();
            return arranged;
        }

        private void RefreshMetrics()
        {
            Label.Measure(Vector2Helpers.Infinity);
            var textWidth = Label.DesiredSize.X;
            var contentBox = GetButtonContentBox();
            var viewportWidth = MathF.Max(1f, contentBox.Width - SafetyMargin * 2f);
            if (textWidth <= 0f || contentBox.Width <= 1f)
                return;

            var currentOverflow = MathF.Max(0f, textWidth - viewportWidth);

            if (!_metricsReady)
            {
                _labelOriginX = GetTextOrigin(contentBox.Left, viewportWidth, textWidth, currentOverflow);
                _metricsReady = true;
            }
            else if (Math.Abs(viewportWidth - _lastViewportWidth) > 0.5f)
            {
                // Layout changes can move the label while it is idle. Reposition
                // the text and recalculate the scroll distance.
                _labelOriginX = GetTextOrigin(contentBox.Left, viewportWidth, textWidth, currentOverflow);
                _scrollOffset = 0f;
                _direction = -1;
                _pause = _hovered && currentOverflow > 0f ? PauseAtEdge : 0f;
            }

            _textWidth = textWidth;
            _lastViewportWidth = viewportWidth;
            // The usable width excludes the safety margin at both edges. The
            // animation endpoint therefore stops before glyph bounds can reach
            // the button border.
            _overflow = MathF.Max(0f, _textWidth - viewportWidth);

            if (_overflow <= 0f)
                ResetScroll();
            else if (!_hovered)
                SetLabelPosition();
        }

        private static float GetTextOrigin(float contentLeft, float viewportWidth, float textWidth, float overflow)
        {
            if (overflow > 0f)
                return contentLeft + SafetyMargin;

            return contentLeft + SafetyMargin + (viewportWidth - textWidth) / 2f;
        }

        private void ResetScroll()
        {
            _scrollOffset = 0f;
            _direction = -1;
            _pause = 0f;
            if (_metricsReady)
                SetLabelPosition();
        }

        private void SetLabelPosition()
        {
            LayoutContainer.SetPosition(Label, new Vector2(
                _labelOriginX + _scrollOffset,
                Label.Position.Y));
        }

        private UIBox2 GetButtonContentBox()
        {
            if (TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var style))
                return style.GetContentBox(UIBox2.FromDimensions(Vector2.Zero, Size), 1f);

            return UIBox2.FromDimensions(Vector2.Zero, Size);
        }
    }
}
