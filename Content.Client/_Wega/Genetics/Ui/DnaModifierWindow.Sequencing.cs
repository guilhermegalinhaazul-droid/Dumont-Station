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
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
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

    private bool SetScannerPreview(EntityUid source)
    {
        if (EnsureAppearancePreview(source) is not { } preview)
        {
            ClearScannerPreview();
            return false;
        }

        SubjectPreview.SetEntity(preview);
        return true;
    }

    private EntityUid? EnsureAppearancePreview(EntityUid source)
    {
        if (!_entManager.TryGetComponent<HumanoidAppearanceComponent>(source, out var sourceAppearance))
            return null;

        var preview = _appearancePreview ?? default;
        var created = false;
        if (!preview.IsValid() || !_entManager.EntityExists(preview) || _appearancePreviewSource != source)
        {
            ClearScannerPreview();
            var species = _prototypeManager.Index<SpeciesPrototype>(sourceAppearance.Species);
            preview = _entManager.SpawnEntity(species.DollPrototype, MapCoordinates.Nullspace);
            _appearancePreview = preview;
            _appearancePreviewSource = source;
            created = true;
        }

        if (!_entManager.TryGetComponent<HumanoidAppearanceComponent>(preview, out var previewAppearance) ||
            !_entManager.TryGetComponent<SpriteComponent>(preview, out var previewSprite))
            return null;

        if (created)
        {
            // This display-only entity is copied once per scanned subject.
            // Never copy later appearance updates, so sequencing cannot recolor it.
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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            ClearScannerPreview();
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
        var appearanceFields = fields.ToArray();
        var colorButtonFields = appearanceFields
            .Where(IsRgbColorField)
            .GroupBy(field => field[..^1], StringComparer.Ordinal)
            .ToDictionary(colorGroup => colorGroup.Key,
                colorGroup => colorGroup.OrderBy(field => field[^1] switch { 'R' => 0, 'G' => 1, _ => 2 })
                    .ElementAt(colorGroup.Count() / 2), StringComparer.Ordinal);
        var colorControls = new Dictionary<string, Slider>();
        var dimensionControls = new Dictionary<string, Slider>();
        var dimensions = GetDimensionMetrics(state);

        float MinWidthForHeight(float height) => Math.Max(dimensions.MinWidth, height / dimensions.SizeRatio);
        float MaxWidthForHeight(float height) => Math.Min(dimensions.MaxWidth, height * dimensions.SizeRatio);
        float MinHeightForWidth(float width) => Math.Max(dimensions.MinHeight, width / dimensions.SizeRatio);
        float MaxHeightForWidth(float width) => Math.Min(dimensions.MaxHeight, width * dimensions.SizeRatio);

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

        foreach (var field in appearanceFields)
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
                row.AddChild(input);
                row.AddChild(CreateAppearanceValueSpacer(appearanceControlHeight));
            }
            else if (field is nameof(UniqueIdentifiersData.Height) or nameof(UniqueIdentifiersData.Width))
            {
                var isHeight = field == nameof(UniqueIdentifiersData.Height);
                var ratio = ReadAppearanceValue(state, field, 200) / 100f;
                var currentHeight = ReadAppearanceValue(state, nameof(UniqueIdentifiersData.Height), 200) / 100f;
                var currentWidth = ReadAppearanceValue(state, nameof(UniqueIdentifiersData.Width), 200) / 100f;
                var minRatio = isHeight ? dimensions.MinHeight : dimensions.MinWidth;
                var maxRatio = isHeight ? dimensions.MaxHeight : dimensions.MaxWidth;
                if (isHeight)
                {
                    minRatio = MinHeightForWidth(currentWidth);
                    maxRatio = MaxHeightForWidth(currentWidth);
                }
                else
                {
                    minRatio = MinWidthForHeight(currentHeight);
                    maxRatio = MaxWidthForHeight(currentHeight);
                }
                var minValue = isHeight
                    ? dimensions.AverageHeight * minRatio
                    : dimensions.BaseMass * (currentHeight + minRatio) / 2f;
                var maxValue = isHeight
                    ? dimensions.AverageHeight * maxRatio
                    : dimensions.BaseMass * (currentHeight + maxRatio) / 2f;
                var physicalValue = isHeight
                    ? dimensions.AverageHeight * ratio
                    : dimensions.BaseMass * (currentHeight + ratio) / 2f;
                var slider = new GeneColorSlider
                {
                    MinValue = minValue,
                    MaxValue = Math.Max(minValue + 1, maxValue),
                    MinWidth = 220,
                    MaxWidth = 220,
                    SetWidth = 220,
                    MinHeight = appearanceControlHeight,
                    MaxHeight = appearanceControlHeight,
                    SetHeight = appearanceControlHeight,
                    Margin = new Thickness(15, 0, 0, 0),
                    Disabled = _portableMode
                };
                slider.Value = Math.Clamp(physicalValue, slider.MinValue, slider.MaxValue);
                var value = new Label
                {
                    MinWidth = 45,
                    MaxWidth = 45,
                    SetWidth = 45,
                    Text = $"{(int) MathF.Round(physicalValue)} {(isHeight ? "cm" : "kg") }"
                };
                dimensionControls[field] = slider;
                slider.OnValueChanged += args =>
                {
                    value.Text = $"{(int) MathF.Round(args.Value)} {(isHeight ? "cm" : "kg")}";
                    if (isHeight && dimensionControls.TryGetValue(nameof(UniqueIdentifiersData.Width), out var weightSlider))
                    {
                        var heightRatio = args.Value / dimensions.AverageHeight;
                        var widthMin = dimensions.BaseMass * (heightRatio + MinWidthForHeight(heightRatio)) / 2f;
                        var widthMax = dimensions.BaseMass * (heightRatio + MaxWidthForHeight(heightRatio)) / 2f;
                        weightSlider.MinValue = widthMin;
                        weightSlider.MaxValue = Math.Max(widthMin + 1, widthMax);
                    }
                    else if (!isHeight)
                    {
                        var selectedHeightSlider = dimensionControls[nameof(UniqueIdentifiersData.Height)];
                        var selectedHeightRatio = selectedHeightSlider.Value / dimensions.AverageHeight;
                        var widthRatio = 2f * args.Value / dimensions.BaseMass - selectedHeightRatio;
                        var heightMin = dimensions.AverageHeight * MinHeightForWidth(widthRatio);
                        var heightMax = dimensions.AverageHeight * MaxHeightForWidth(widthRatio);
                        selectedHeightSlider.MinValue = heightMin;
                        selectedHeightSlider.MaxValue = Math.Max(heightMin + 1, heightMax);
                    }
                };
                selected = () =>
                {
                    var selectedHeight = dimensionControls[nameof(UniqueIdentifiersData.Height)].Value / dimensions.AverageHeight;
                    var selectedWeight = dimensionControls[nameof(UniqueIdentifiersData.Width)].Value;
                    var widthRatio = 2f * selectedWeight / dimensions.BaseMass - selectedHeight;
                    var ratioValue = isHeight ? selectedHeight : widthRatio;
                    return Math.Clamp((int) MathF.Round(ratioValue * 100f),
                        (int) MathF.Ceiling(minRatio * 100f), (int) MathF.Floor(maxRatio * 100f)).ToString("D3");
                };
                row.AddChild(slider);
                row.AddChild(value);
            }
            else
            {
                var max = field == nameof(UniqueIdentifiersData.SkinTone) ? 100 : 255;
                var min = 0;
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
                };
                selected = () => ((int) slider.Value).ToString();
                row.AddChild(slider);
                row.AddChild(value);
            }

            var hasSequenceButton = !IsRgbColorField(field)
                ? field != nameof(UniqueIdentifiersData.Width)
                : colorButtonFields.TryGetValue(field[..^1], out var centerField) && centerField == field;
            if (hasSequenceButton)
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
                    HorizontalAlignment = Control.HAlignment.Center,
                    VerticalAlignment = Control.VAlignment.Center,
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
                        if (field == nameof(UniqueIdentifiersData.Height))
                        {
                            var fieldsToSequence = new[]
                            {
                                nameof(UniqueIdentifiersData.Height), nameof(UniqueIdentifiersData.Width)
                            };
                            OnGeneticMessage?.Invoke(new GeneticAppearanceGroupMessage(fieldsToSequence,
                                fieldsToSequence.Select(dimension =>
                                {
                                    var heightRatio = dimensionControls[nameof(UniqueIdentifiersData.Height)].Value / dimensions.AverageHeight;
                                    var weight = dimensionControls[nameof(UniqueIdentifiersData.Width)].Value;
                                    var widthRatio = Math.Clamp(2f * weight / dimensions.BaseMass - heightRatio,
                                        MinWidthForHeight(heightRatio), MaxWidthForHeight(heightRatio));
                                    heightRatio = Math.Clamp(heightRatio, MinHeightForWidth(widthRatio), MaxHeightForWidth(widthRatio));
                                    var ratio = dimension == nameof(UniqueIdentifiersData.Height) ? heightRatio : widthRatio;
                                    return Math.Clamp((int) MathF.Round(ratio * 100f), 50, 200).ToString("D3");
                                }).ToArray()));
                            return;
                        }
                        if (field == nameof(UniqueIdentifiersData.Width))
                            return;
                        var selectedValue = selected();
                        OnGeneticMessage?.Invoke(new GeneticAppearanceMessage(field, selectedValue));
                    };
                }
                var actionSlot = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    HorizontalExpand = true,
                    VerticalAlignment = Control.VAlignment.Center,
                    MinHeight = appearanceControlHeight,
                    MaxHeight = appearanceControlHeight,
                    SetHeight = appearanceControlHeight
                };
                actionSlot.AddChild(apply);
                row.AddChild(actionSlot);
            }
            else
            {
                // Keep the same column width on RGB rows whose channels share
                // the single sequence button centered on the middle channel.
                row.AddChild(new Control
                {
                    HorizontalExpand = true,
                    MinHeight = appearanceControlHeight,
                    MaxHeight = appearanceControlHeight,
                    SetHeight = appearanceControlHeight
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

    private (float AverageHeight, float MinHeight, float MaxHeight,
        float MinWidth, float MaxWidth, float SizeRatio, float BaseMass) GetDimensionMetrics(DnaModifierBoundUserInterfaceState state)
    {
        const float defaultMass = 71f;
        var defaults = (176.1f, 0.8f, 1.2f, 0.85f, 1.15f, 1.2f, defaultMass);
        if (state.ScannerBody is not { } netBody || !_entManager.TryGetEntity(netBody, out EntityUid? subject) ||
            subject is not { } subjectEntity || !_entManager.TryGetComponent<HumanoidAppearanceComponent>(subjectEntity, out var appearance) ||
            !_prototypeManager.TryIndex(appearance.Species, out SpeciesPrototype? species) || species == null)
            return defaults;

        var baseMass = defaultMass;
        if (_prototypeManager.Index(species.Prototype).TryGetComponent<FixturesComponent>(out var fixtures, _entManager.ComponentFactory) &&
            fixtures.Fixtures.TryGetValue("fix1", out var fixture))
            baseMass = FixtureSystem.GetMassData(fixture.Shape, fixture.Density).Mass;

        return (species.AverageHeight, species.MinHeight, species.MaxHeight,
            species.MinWidth, species.MaxWidth, species.SizeRatio, baseMass);
    }

    private static int ReadAppearanceValue(DnaModifierBoundUserInterfaceState state, string field, int max)
    {
        if (state.Unique is null || AppearanceGene.Get(state.Unique, field) is not { Length: > 0 } value)
            return field is nameof(UniqueIdentifiersData.Height) or nameof(UniqueIdentifiersData.Width) ? 100 : 0;

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

        if (field == nameof(UniqueIdentifiersData.SkinTone))
        {
            var tone = value.Length >= 3 && value[0] == "1"
                ? 100
                : value.Length >= 3 ? TryHexDigit(value[1]) * 10 + TryHexDigit(value[2]) : 0;
            return Math.Clamp(tone, 0, max);
        }

        if (field is nameof(UniqueIdentifiersData.Height) or nameof(UniqueIdentifiersData.Width))
            return Math.Clamp(int.TryParse(string.Concat(value), out var scale) ? scale : 100, 50, 200);

        var encoded = value.Length >= 2 ? string.Concat(value[0], value[1]) : value[0];
        return Math.Clamp(int.TryParse(encoded, System.Globalization.NumberStyles.HexNumber, null, out var parsed)
            ? parsed : 0, 0, max);
    }

    private static bool IsColorField(string field)
        => field.Contains("Color", StringComparison.Ordinal) || field == nameof(UniqueIdentifiersData.SkinTone);

    private static bool IsRgbColorField(string field)
        => IsColorField(field) && field.Length > 0 && (field[^1] is 'R' or 'G' or 'B')
           && field != nameof(UniqueIdentifiersData.SkinTone);

    private static int TryHexDigit(string value)
        => int.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var digit) ? digit : 0;

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
        private readonly bool _centerName;

        public GeneNameButton(string name)
        {
            Text = name;
            // Count displayed text elements so an accented letter, including
            // a decomposed letter-plus-combining-accent, is still one character.
            _centerName = System.Globalization.StringInfo.ParseCombiningCharacters(name).Length <= 13;
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
            if (_centerName || !_hovered || _overflow <= 0f)
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
                _labelOriginX = GetTextOrigin(contentBox.Left, viewportWidth, textWidth);
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
                : GetTextOrigin(contentBox.Left, viewportWidth, textWidth);
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
                _labelOriginX = GetTextOrigin(contentBox.Left, viewportWidth, textWidth);
                _metricsReady = true;
            }
            else if (Math.Abs(viewportWidth - _lastViewportWidth) > 0.5f)
            {
                // Layout changes can move the label while it is idle. Reposition
                // the text and recalculate the scroll distance.
                _labelOriginX = GetTextOrigin(contentBox.Left, viewportWidth, textWidth);
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

        private float GetTextOrigin(float contentLeft, float viewportWidth, float textWidth)
        {
            return _centerName
                ? contentLeft + SafetyMargin + (viewportWidth - textWidth) / 2f
                : contentLeft + SafetyMargin;
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
