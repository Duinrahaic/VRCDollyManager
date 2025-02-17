using System.Reactive;
using Avalonia.ReactiveUI;
using VRCDollyManager.Client.ViewModels;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using ReactiveUI;

namespace VRCDollyManager.Client.Views;

public partial class ClientWindow : ReactiveWindow<ClientWindowViewModel>
{
    public ClientWindow()
    {
        var rootComponents = new RootComponentsCollection
        {
            new RootComponent("#app", typeof(Main), null)
        };

        Resources.Add("services", App.AppHost!.Services);
        Resources.Add("rootComponents", rootComponents);

        InitializeComponent();

        this.WhenActivated(d => d(ViewModel!.ExitInteraction.RegisterHandler(DoExitAsync)));
    }

    private async Task DoExitAsync(InteractionContext<Unit, Unit> ic)
    {
        Close();
        await Task.CompletedTask;
        ic.SetOutput(Unit.Default);
    }
}