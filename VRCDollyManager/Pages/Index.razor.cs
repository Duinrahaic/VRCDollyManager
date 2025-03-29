using Blazicons;
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
    private Modal? About { get; set; } = null!;
    private Modal? Settings { get; set; } = null!;
    private DollyRenderer? Renderer { get; set; }
    private DollyTable? Table { get; set; }
    private DollySideContainer? SidePanel { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _dollies = await DollyService.GetAllDolliesAsync();
        DollyService.DollyChanged += OnDollyChanged;
 
    }

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

    private async void OnDollyChanged(object? sender, DollyChangedEventArgs e)
    {
        Refresh();
    }
    private void OpenAbout()
    {
        About?.OpenModal();
    }

    private void OpenSettings()
    {
        Settings?.OpenModal();
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
        if (disposing) DollyService.DollyChanged -= OnDollyChanged;
    }

    ~Index()
    {
        Dispose(false);
    }
}