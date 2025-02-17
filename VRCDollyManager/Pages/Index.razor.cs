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
    private Virtualize<Dolly>? _virtualizeRef;
    private Dolly? _selectedDolly = null;
    private bool _onRefresh = false;
    private bool _relative = false;
    private string _filter = "";
    private uint _dollyCount = 0;
    private Modal? About { get; set; } = null!;
    private Modal? Edit { get; set; } = null!;
    private Modal? Preview { get; set; } = null!;
    private Modal? Settings { get; set; } = null!;
    private DollyRenderer? Renderer { get; set; }


    protected override async Task OnInitializedAsync()
    {
        _dollies = await DollyService.GetAllDolliesAsync();
        _dollyCount = (uint)_dollies.Count;
        DollyService.DollyChanged += OnDollyChanged;
    }

    private async ValueTask<ItemsProviderResult<Dolly>> LoadDollyData(ItemsProviderRequest request)
    {
        var query = _dollies.AsQueryable();
        await Task.CompletedTask;
        if (!string.IsNullOrWhiteSpace(_filter))
            query = query.Where(dolly =>
                (!string.IsNullOrEmpty(dolly.Name) &&
                 dolly.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(dolly.Alias) &&
                 dolly.Alias.Contains(_filter, StringComparison.OrdinalIgnoreCase)) ||
                dolly.Tags.Any(tag => tag.Contains(_filter, StringComparison.OrdinalIgnoreCase)));
        _dollyCount = (uint)query.Count(); // Get the total filtered count
        var filteredItems = query.Skip(request.StartIndex).Take(request.Count).ToList();
        return new ItemsProviderResult<Dolly>(filteredItems, query.Count());
    }

    private async Task OnFilterUpdate(string filter)
    {
        _filter = filter;
        if (_virtualizeRef == null) return;
        await _virtualizeRef.RefreshDataAsync();
    }


    private async void Refresh()
    {
        _onRefresh = true;
        await DollyService.SyncFileSystemWithDatabaseAsync();
        _dollies = await DollyService.GetAllDolliesAsync();
        if (EditDollyModel != null)
            _selectedDolly = _dollies.FirstOrDefault(dolly => dolly.Name == EditDollyModel.Name);
        if (_virtualizeRef == null) return;
        await _virtualizeRef.RefreshDataAsync();
        await InvokeAsync(StateHasChanged);
        _onRefresh = false;
        await InvokeAsync(StateHasChanged);
    }

    private async void OnSelect(Dolly dolly)
    {
        if (_selectedDolly == dolly)
        {
            _selectedDolly = null;
            if (Renderer == null) return;
            await Renderer.ClearScene();
        }
        else
        {
            _selectedDolly = await DollyService.GetDollyByNameAsync(dolly.Name);
            if (Renderer == null || _selectedDolly == null) return;
            await Renderer.LoadDollyPath(_selectedDolly);
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

    private void OpenPreview()
    {
        Preview?.OpenModal();
    }

    private async void DownloadVDM()
    {
        if(_selectedDolly == null) return;
        await JS.DownloadJsonAsync("VDM_Dolly.json", _selectedDolly);
    }
    private async void DownloadDolly()
    {
        if(_selectedDolly == null) return;
        await JS.DownloadJsonAsync(_selectedDolly.Name, _selectedDolly.KeyFrames);
    }
    
    
    
    private Dolly? EditDollyModel { get; set; } = null;

    private void OpenEdit()
    {
        EditDollyModel = _selectedDolly?.Clone();
        Edit?.OpenModal();
    }

    private void UpdateEditDolly()
    {
        if (EditDollyModel == null) return;
        DollyService.UpdateDollyAsync(EditDollyModel);
        CloseEdit();
    }

    private void DeleteDolly()
    {
        if (EditDollyModel == null) return;
        DollyService.RemoveDollyAsync(EditDollyModel.Name);
        Renderer?.ClearScene();
        CloseEdit();
    }
    
    private void LoadDolly()
    {
        if (_selectedDolly == null) return;
        
        OSC.SendMessage("/dolly/Import", _selectedDolly.GetCameraKeyFramesToString());
    }


    private void CloseEdit()
    {
        Edit?.CloseModal();
        EditDollyModel = null;
        Refresh();
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