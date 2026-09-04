using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Visitors;

public partial class NewVisitorPage : ContentPage
{
    private readonly NewVisitorViewModel _viewModel;
    private CancellationTokenSource? _searchDebounce;

    public NewVisitorPage(NewVisitorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Reset();
        await _viewModel.LoadLookupsAsync();
    }

    /// <summary>Debounces search-as-you-type — waits 400ms after the last
    /// keystroke before actually calling the API, so typing a flat number
    /// doesn't fire a request per character.</summary>
    private void OnFlatSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchDebounce?.Cancel();
        _searchDebounce = new CancellationTokenSource();
        var token = _searchDebounce.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(400, token);
                if (!token.IsCancellationRequested)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => _viewModel.SearchFlatsCommand.ExecuteAsync(null));
                }
            }
            catch (TaskCanceledException)
            {
            }
        });
    }

    private void OnFlatSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is FlatDto flat)
        {
            _viewModel.SelectFlatCommand.Execute(flat);
        }
    }
}
