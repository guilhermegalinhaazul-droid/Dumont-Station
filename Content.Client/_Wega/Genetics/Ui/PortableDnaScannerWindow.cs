using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.Genetics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Wega.Genetics.Ui;

/// <summary>Portable reader, limited to inspection and sample/disk management.</summary>
public sealed class PortableDnaScannerWindow : FancyWindow
{
    private readonly Label _subject = new();
    private readonly Label _unique = new();
    private readonly Label _dna = new();
    private readonly BoxContainer _genes = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly Label _diskStatus = new();
    private readonly BoxContainer _saveButtons = new() { Orientation = BoxContainer.LayoutOrientation.Horizontal };
    private readonly Button _load = new() { Text = Loc.GetString("dna-portable-load-disk") };
    private readonly Button _clear = new() { Text = Loc.GetString("dna-portable-clear") };

    public event Action<PortableDnaSampleKind>? OnSave;
    public event Action? OnLoad;
    public event Action? OnClear;

    public PortableDnaScannerWindow()
    {
        Title = Loc.GetString("dna-portable-title");
        MinSize = new Vector2(760, 560);
        _load.OnPressed += _ => OnLoad?.Invoke();
        _clear.OnPressed += _ => OnClear?.Invoke();

        var tabs = new TabContainer { VerticalExpand = true };
        tabs.AddChild(BuildSampleTab());
        tabs.AddChild(BuildDiskTab());
        tabs.SetTabTitle(0, Loc.GetString("dna-portable-tab-sample"));
        tabs.SetTabTitle(1, Loc.GetString("dna-portable-tab-disk"));

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        root.AddChild(new Label { Text = Loc.GetString("dna-portable-read-only"), StyleClasses = { "LabelSubText" } });
        root.AddChild(tabs);
        AddChild(root);
    }

    private Control BuildSampleTab()
    {
        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8, Margin = new Thickness(8) };
        root.AddChild(new Label { Text = Loc.GetString("dna-portable-subject"), StyleClasses = { "LabelBig" } });
        root.AddChild(_subject);
        root.AddChild(new Label { Text = Loc.GetString("dna-portable-unique") });
        root.AddChild(_unique);
        root.AddChild(new Label { Text = Loc.GetString("dna-portable-bases") });
        root.AddChild(_dna);
        root.AddChild(new Label { Text = Loc.GetString("dna-portable-enzymes"), StyleClasses = { "LabelBig" } });
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_genes);
        root.AddChild(scroll);
        return root;
    }

    private Control BuildDiskTab()
    {
        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8, Margin = new Thickness(8) };
        root.AddChild(new Label { Text = Loc.GetString("dna-portable-disk-title"), StyleClasses = { "LabelBig" } });
        root.AddChild(_diskStatus);
        root.AddChild(new Label { Text = Loc.GetString("dna-portable-disk-help") });
        foreach (var kind in Enum.GetValues<PortableDnaSampleKind>())
        {
            var save = new Button { Text = Loc.GetString(kind switch
            {
                PortableDnaSampleKind.Unique => "dna-portable-save-unique",
                PortableDnaSampleKind.Structural => "dna-portable-save-structural",
                _ => "dna-portable-save-both"
            }) };
            var selected = kind;
            save.OnPressed += _ => OnSave?.Invoke(selected);
            _saveButtons.AddChild(save);
        }
        root.AddChild(_saveButtons);
        root.AddChild(_load);
        root.AddChild(_clear);
        return root;
    }

    public void UpdateState(PortableDnaScannerState state)
    {
        _subject.Text = string.IsNullOrWhiteSpace(state.SubjectName) ? Loc.GetString("dna-portable-no-sample") : state.SubjectName;
        var unique = state.Sample?.Identifier;
        _unique.Text = unique == null ? Loc.GetString("dna-portable-no-data") : $"ID: {unique.ID}\n{unique.EntityName ?? state.SubjectName}";
        _dna.Text = string.IsNullOrWhiteSpace(state.SubjectDna) ? Loc.GetString("dna-portable-no-data") : state.SubjectDna;
        _diskStatus.Text = state.HasDisk ? Loc.GetString("dna-portable-disk-present") : Loc.GetString("dna-portable-disk-missing");
        _genes.RemoveAllChildren();
        foreach (var gene in state.Sample?.Info ?? new List<EnzymesPrototypeInfo>())
            _genes.AddChild(new Label { Text = $"{gene.Order}: {gene.EnzymesPrototypeId} — {(gene.Active ? Loc.GetString("dna-portable-active") : Loc.GetString("dna-portable-inactive"))}" });

        _load.Disabled = !state.HasDisk;
        _clear.Disabled = state.Sample == null;
        foreach (var child in _saveButtons.Children)
            if (child is Button button)
                button.Disabled = state.Sample == null || !state.HasDisk;
    }
}
