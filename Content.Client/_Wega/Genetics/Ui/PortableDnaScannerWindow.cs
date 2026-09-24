using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.Genetics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Wega.Genetics.Ui;

public sealed class PortableDnaScannerWindow : FancyWindow
{
    private readonly Label _subject = new();
    private readonly Label _dna = new();
    private readonly BoxContainer _genes = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly Button _save = new() { Text = Loc.GetString("dna-portable-save") };
    private readonly Button _clear = new() { Text = Loc.GetString("dna-portable-clear") };
    public event Action? OnSave;
    public event Action? OnClear;

    public PortableDnaScannerWindow()
    {
        Title = Loc.GetString("dna-portable-title");
        MinSize = new Vector2(680, 480);
        _save.OnPressed += _ => OnSave?.Invoke();
        _clear.OnPressed += _ => OnClear?.Invoke();

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        root.AddChild(new Label { Text = Loc.GetString("dna-portable-subject") });
        root.AddChild(_subject);
        root.AddChild(new Label { Text = Loc.GetString("dna-portable-bases") });
        root.AddChild(_dna);
        root.AddChild(new Label { Text = Loc.GetString("dna-portable-enzymes") });
        var scroll = new ScrollContainer { VerticalExpand = true };
        scroll.AddChild(_genes);
        root.AddChild(scroll);
        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        buttons.AddChild(_save);
        buttons.AddChild(_clear);
        root.AddChild(buttons);
        AddChild(root);
    }

    public void UpdateState(PortableDnaScannerState state)
    {
        _subject.Text = string.IsNullOrWhiteSpace(state.SubjectName) ? Loc.GetString("dna-portable-no-sample") : state.SubjectName;
        _dna.Text = string.IsNullOrWhiteSpace(state.SubjectDna) ? Loc.GetString("dna-portable-no-data") : state.SubjectDna;
        _genes.RemoveAllChildren();
        foreach (var gene in state.Sample?.Info ?? new List<EnzymesPrototypeInfo>())
            _genes.AddChild(new Label { Text = $"{gene.Order}: {gene.EnzymesPrototypeId} — {(gene.Active ? Loc.GetString("dna-portable-active") : Loc.GetString("dna-portable-inactive"))}" });
        _save.Disabled = state.Sample == null || !state.HasDisk;
    }
}
