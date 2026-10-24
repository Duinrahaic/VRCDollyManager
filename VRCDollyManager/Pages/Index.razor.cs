using System.Windows.Forms;
using Blazicons;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using VRCDollyManager.Components;
using VRCDollyManager.Extensions;
using VRCDollyManager.Models;
using VRCDollyManager.Services;

namespace VRCDollyManager.Pages;

public partial class Index : IDisposable
{
    private List<Dolly> _dollies = new();
    private Dolly? _selectedDolly = null;
    private bool _onRefresh = false;
    private string _filter = "";
    private DollyRenderer? Renderer { get; set; }
    private DollyTable? Table { get; set; }
    private DollySideContainer? SidePanel { get; set; }

    private int Delay
    {
        get => Player.Delay;
        set => Player.Delay = value;
    }
    
    protected override async Task OnInitializedAsync()
    {
        _dollies = await DollyService.GetAllDolliesAsync();
        DollyService.DollyChanged += OnDollyChanged;
        
        OSC.OnOscMessageReceived += OnOscMessageReceived;
        KeypressService.OnKeyPress += OnKeyPress;
        Player.StateChanged += OnPlayerStateChanged;
    }

    private void OnPlayerStateChanged(object? sender, EventArgs e) => InvokeAsync(StateHasChanged);

    private void OnKeyPress(object sender, object e)
    {
         Play();
    }

    private bool _delayIncPressed = false;
    private bool _delayDecPressed = false;
    private void OnOscMessageReceived(object? sender, OscSubscriptionEvent e)
    {
        if (e.Message.Address.Contains("VDM"))
        {
            if (e.Message.Address.Contains("VDM_Delay_Inc"))
            {
                if (e.Message.Arguments[0] is bool value)
                {
                    if (value && !_delayIncPressed)
                        IncrementDelay();

                    _delayIncPressed = value;
                }
            }
            else if (e.Message.Address.Contains("VDM_Delay_Dec"))
            {
                if (e.Message.Arguments[0] is bool value)
                {
                    if (value && !_delayDecPressed)
                        DecrementDelay();

                    _delayDecPressed = value;
                }
            }
            else if (e.Message.Address.Contains("VDM_Play"))
            {
                Play();
            }
            else if (e.Message.Address.Contains("VDM_Stop"))
            {
                Stop();
            }
            InvokeAsync(StateHasChanged);
        }
    }

    private void Play() => Player.Play();

    private void Stop()
    {
        ChainPlayer.Stop();
        Player.Stop();
    }

    private void IncrementDelay() => Delay++;
    private void DecrementDelay() => Delay--;

    private Task OnFilterUpdate(string filter)
    {
        _filter = filter;
        return Task.CompletedTask;
    }

    private async Task RefreshButton()
    {
        _onRefresh = true;
        await Task.Delay(1000);
        Refresh();
        await InvokeAsync(StateHasChanged);
        _onRefresh = false;
    }

    private async void Refresh()
    {
        await DollyService.SyncFileSystemWithDatabaseAsync();
        _dollies = await DollyService.GetAllDolliesAsync();
        if (Table != null)
        {
            await Table.RefreshTable();
        }
        await InvokeAsync(StateHasChanged);
    }

    private async void OnSelect(Dolly? dolly)
    {
        if (dolly == null)
        {
            _selectedDolly = null;
        }
        else
        {
            _selectedDolly = await DollyService.GetDollyByNameAsync(dolly.Name);
        }
    }

    private void OnDollyChanged(object? sender, DollyChangedEventArgs e)
    {
        Refresh();
    }
 
    private async Task LoadDolly()
    {
        if (_selectedDolly == null) return;
        await Player.ImportAsync(_selectedDolly);
    }

    private void OnAddToDirector(Dolly? dolly)
    {
        if (dolly == null) return;
        SidePanel?.QueueDolly(dolly);
    }

    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }


    private void ReleaseUnmanagedResources()
    {
        _dollies.Clear();
    }
    

    protected virtual void Dispose(bool disposing)
    {
        ReleaseUnmanagedResources();
        if (disposing)
        {
            KeypressService.OnKeyPress -= OnKeyPress;
            Player.StateChanged -= OnPlayerStateChanged;
            DollyService.DollyChanged -= OnDollyChanged;
            OSC.OnOscMessageReceived -= OnOscMessageReceived;
        }
    }

    ~Index()
    {
        Dispose(false);
    }
}