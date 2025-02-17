using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;

namespace VRCDollyManager.Client.ViewModels;

public class ClientWindowViewModel : ViewModelBase
{
    public ClientWindowViewModel()
    {
        if (!Design.IsDesignMode)
        {
        }

        ExitCommand = ReactiveCommand.CreateFromTask(OnExit);
        ExitInteraction = new Interaction<Unit, Unit>();
    }

    public ReactiveCommand<Unit, Unit> ExitCommand { get; }
    public Interaction<Unit, Unit> ExitInteraction { get; }

    private async Task OnExit()
    {
        await ExitInteraction.Handle(Unit.Default);
    }
}