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
    private bool _relative = false;
    private string _filter = "";
    private DollyRenderer? Renderer { get; set; }
    private DollyTable? Table { get; set; }
    private DollySideContainer? SidePanel { get; set; }
    private int _delay = 0;
    private bool _isPlaying = false;
    
    protected override async Task OnInitializedAsync()
    {
        _dollies = await DollyService.GetAllDolliesAsync();
        DollyService.DollyChanged += OnDollyChanged;
        
        OSC.OnOscMessageReceived += OnOscMessageReceived;
        KeypressService.OnKeyPress += OnKeyPress;
    }

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
        else if (e.Message.Address.Contains("/dolly/Play"))
        {
            if(e.Message.Arguments[0] is bool isPlaying)
            {
                _isPlaying = isPlaying;
                InvokeAsync(StateHasChanged);
            }
        }

    }

    private void OnDelayChanged(object? sender, int e)
    {
        _delay = e;
        InvokeAsync(StateHasChanged);
    }


    private void Play()
    {
        OSC.SendMessage("/dolly/PlayDelayed", _delay);
    }

    private void Stop()
    {
        OSC.SendMessage("/dolly/Play",false);
    }
    private void OnBlur(FocusEventArgs _) =>
        _delay = Math.Clamp(_delay, 0, 5);
    private void DurationLimitChanged(int value)
    {
        _delay = Math.Clamp(value, 0, 5);
        InvokeAsync(StateHasChanged);
    } 
    private void IncrementDelay() => DurationLimitChanged(_delay + 1);
    private void DecrementDelay() => DurationLimitChanged(_delay - 1);
    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            var dolly = _dollies.FirstOrDefault();
            if (dolly != null)
            {
                SidePanel?.QueueDolly(dolly);
                StateHasChanged();
            }
            
       
        }
    }

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
 
    private async void LoadDolly()
    {
        if (_selectedDolly == null) return;
        if (_relative)
        {
            var path = _selectedDolly.GetRelativeDollyKeyframes();
            if (path == null) return;
            OSC.SendMessage("/dolly/Import", path);
            await Task.Delay(100);
            File.Delete(path);
        }
        else
        {
            OSC.SendMessage("/dolly/Import", _selectedDolly.GetDollyFilePath());
        }
    }

    private void OnAddToDirector(Dolly? dolly)
    {
        if(_selectedDolly == null || dolly == null) return;  
        SidePanel?.QueueDolly(_selectedDolly);
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
            DollyService.DollyChanged -= OnDollyChanged;
            OSC.OnOscMessageReceived -= OnOscMessageReceived;
        }
    }

    ~Index()
    {
        Dispose(false);
    }
}