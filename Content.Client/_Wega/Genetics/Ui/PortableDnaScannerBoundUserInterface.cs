using Content.Shared.Genetics;
using Robust.Client.UserInterface;

namespace Content.Client._Wega.Genetics.Ui;

public sealed class PortableDnaScannerBoundUserInterface(EntityUid owner, Enum key) : BoundUserInterface(owner, key)
{
    private PortableDnaScannerWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = new PortableDnaScannerWindow();
        _window.OnSave += kind => SendPredictedMessage(new PortableDnaScannerSaveMessage(kind));
        _window.OnClear += () => SendPredictedMessage(new PortableDnaScannerClearMessage());
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is PortableDnaScannerState scanner)
            _window?.UpdateState(scanner);
    }
}
