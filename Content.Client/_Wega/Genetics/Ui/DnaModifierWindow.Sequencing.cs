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
using Robust.Shared.Timing;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Wega.Genetics.Ui;

public sealed partial class DnaModifierWindow
{
    public event Action<BoundUserInterfaceMessage>? OnGeneticMessage;
    private readonly BoxContainer _uniqueGenes = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly BoxContainer _structuralGenes = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly BoxContainer _geneTools = new() { Orientation = BoxContainer.LayoutOrientation.Horizontal };
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
        if (!preview.IsValid() || !_entManager.EntityExists(preview) || _appearancePreviewSource != source)
        {
            if (preview.IsValid() && _entManager.EntityExists(preview))
                _entManager.DeleteEntity(preview);

            var species = _prototypeManager.Index<SpeciesPrototype>(sourceAppearance.Species);
            preview = _entManager.SpawnEntity(species.DollPrototype, MapCoordinates.Nullspace);
            _appearancePreview = preview;
            _appearancePreviewSource = source;
        }

        if (!_entManager.TryGetComponent<HumanoidAppearanceComponent>(preview, out var previewAppearance)
            || !_entManager.TryGetComponent<SpriteComponent>(preview, out var previewSprite))
            return preview;

        previewAppearance.Species = sourceAppearance.Species;
        previewAppearance.Sex = sourceAppearance.Sex;
        previewAppearance.Gender = sourceAppearance.Gender;
        previewAppearance.SkinColor = sourceAppearance.SkinColor;
        previewAppearance.EyeColor = sourceAppearance.EyeColor;
        previewAppearance.MarkingSet = new MarkingSet(sourceAppearance.MarkingSet);
        previewAppearance.Height = sourceAppearance.Height;
        previewAppearance.Width = sourceAppearance.Width;
        _entManager.System<HumanoidAppearanceSystem>().UpdateSprite((preview, previewAppearance, previewSprite));
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
                row.AddChild(new Label { Text = $"Bloco {gene.Number}", MinWidth = 48 });
                var name = new GeneNameButton(gene.Name) { ToolTip = gene.Name, SetWidth = 110 };
                name.OnPressed += _ => OnGeneticMessage?.Invoke(new GeneticSelectMessage(gene.Number));
                row.AddChild(name);
                var toggle = new Button
                {
                    Text = Loc.GetString(gene.Active ? "dna-gene-on" : "dna-gene-off"),
                    Disabled = !gene.Discovered,
                    ModulateSelfOverride = gene.Active ? new Color(64, 197, 108) : new Color(224, 90, 90),
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
        var appearanceSignature = string.Join("|", state.AppearanceFields) + state.ScannerSpecies;
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
                var isScale = field is nameof(UniqueIdentifiersData.Height) or nameof(UniqueIdentifiersData.Width);
                var max = field == nameof(UniqueIdentifiersData.SkinTone) ? 100 : isScale ? 200 : 255;
                var min = isScale ? 50 : 0;
                var slider = new Slider { MinValue = min, MaxValue = max, SetWidth = 220 };
                var value = new Label { MinWidth = 35, Text = ReadAppearanceValue(state, field, max).ToString() };
                slider.Value = int.Parse(value.Text);
                var colorPreview = new Button { Text = string.Empty, SetWidth = 30, SetHeight = 30, Disabled = true };
                UpdateColorButton(colorPreview, state, field, value.Text);
                slider.OnValueChanged += args =>
                {
                    value.Text = ((int) args.Value).ToString();
                    UpdateColorButton(colorPreview, state, field, value.Text);
                    PreviewAppearance(field, value.Text);
                };
                selected = () => ((int) slider.Value).ToString();
                row.AddChild(slider);
                row.AddChild(value);
                if (IsColorField(field))
                    row.AddChild(colorPreview);
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

        var preview = EnsureAppearancePreview(sourceEntity);
        if (preview is not { } previewEntity)
            return;

        if (!_entManager.TryGetComponent<HumanoidAppearanceComponent>(previewEntity, out var appearance) ||
            !_entManager.TryGetComponent<SpriteComponent>(previewEntity, out var sprite))
            return;

        appearance.Species = sourceAppearance.Species;
        appearance.Sex = sourceAppearance.Sex;
        appearance.Gender = sourceAppearance.Gender;
        appearance.SkinColor = sourceAppearance.SkinColor;
        appearance.EyeColor = sourceAppearance.EyeColor;
        appearance.MarkingSet = new MarkingSet(sourceAppearance.MarkingSet);
        appearance.Height = sourceAppearance.Height;
        appearance.Width = sourceAppearance.Width;

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

        var encoded = value[0];
        if (field is nameof(UniqueIdentifiersData.SkinTone)
            or nameof(UniqueIdentifiersData.Height) or nameof(UniqueIdentifiersData.Width))
            return Math.Clamp(int.TryParse(encoded, out var decimalValue) ? decimalValue : 0, 0, max);

        return Math.Clamp(int.TryParse(encoded, System.Globalization.NumberStyles.HexNumber, null, out var hexValue)
            ? hexValue
            : 0, 0, max);
    }

    private static bool IsColorField(string field)
        => field.Contains("Color", StringComparison.Ordinal) || field == nameof(UniqueIdentifiersData.SkinTone);

    private static void UpdateColorButton(Button button, DnaModifierBoundUserInterfaceState state,
        string field, string value)
    {
        if (!IsColorField(field) || state.Unique is not { } unique)
            return;

        var channel = int.TryParse(value, out var parsed) ? Math.Clamp(parsed, 0, 255) : 0;
        var red = ReadColorChannel(unique, field, 'R', channel);
        var green = ReadColorChannel(unique, field, 'G', channel);
        var blue = ReadColorChannel(unique, field, 'B', channel);
        button.ModulateSelfOverride = new Color(red / 255f, green / 255f, blue / 255f);
    }

    private static int ReadColorChannel(UniqueIdentifiersData unique, string field, char channel, int edited)
    {
        if (field == nameof(UniqueIdentifiersData.SkinTone))
            return Math.Clamp((int) (edited * 2.55f), 0, 255);

        if (field.Length > 0 && field[field.Length - 1] == channel)
            return edited;

        // Avoid range slicing here: the generated ReadOnlySpan constructor is
        // rejected by the client sandbox type checker.
        var category = field.Substring(0, field.Length - 1) + channel;
        var value = AppearanceGene.Get(unique, category);
        var encoded = value is { Length: >= 2 } ? string.Concat(value[0], value[1]) : null;
        return encoded is not null && int.TryParse(encoded,
            System.Globalization.NumberStyles.HexNumber, null, out var parsed)
            ? parsed
            : 0;
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
        var reset = new Button { Text = Loc.GetString("dna-sequence-reset") };
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
        private const float ScrollSpeed = 14f;
        private const float PauseAtEdge = 1.15f;
        private float _scrollOffset;
        private float _pause;
        private int _direction = -1;
        private float _labelOriginX;
        private bool _originInitialized;

        public GeneNameButton(string name)
        {
            Text = name;
            SetWidth = 110;
            MaxWidth = 110;
            ToolTip = name;

            // Keep the button fixed while allowing its label to move inside it.
            // The button clips the label, so long names never draw over the
            // neighbouring toggle button.
            RectClipContent = true;
            Label.ClipText = false;
            Label.HorizontalAlignment = HAlignment.Left;
            Label.HorizontalExpand = false;
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            // Ten characters is the point at which gene names should use the
            // display marquee, even when a particular font happens to fit.
            if (Label.Text is not { Length: >= 10 })
                return;

            if (!_originInitialized)
            {
                _labelOriginX = Label.Position.X;
                _originInitialized = true;
            }

            var overflow = Label.DesiredSize.X - Label.Size.X;
            if (overflow <= 1f)
                return;

            if (_pause > 0f)
            {
                _pause -= args.DeltaSeconds;
            }
            else
            {
                _scrollOffset += _direction * ScrollSpeed * args.DeltaSeconds;
                if (_scrollOffset <= -overflow)
                {
                    _scrollOffset = -overflow;
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

            LayoutContainer.SetPosition(Label, new Vector2(_labelOriginX + _scrollOffset, Label.Position.Y));
        }
    }
}
