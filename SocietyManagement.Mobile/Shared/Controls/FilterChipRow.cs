namespace SocietyManagement.Mobile.Shared.Controls;

public record FilterChipOption(string Value, string Label, int? Count = null);

/// <summary>Consolidates the hand-written filter-chip rows this app had
/// (Announcements' All/Unread/Saved) into one control instead of a second
/// copy appearing the next time a screen needs one — single-select,
/// two-way bindable SelectedValue, rendered with the existing ChipButton
/// style (no new visual design).</summary>
public class FilterChipRow : ScrollView
{
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource), typeof(IEnumerable<FilterChipOption>), typeof(FilterChipRow), null, propertyChanged: OnChanged);

    public static readonly BindableProperty SelectedValueProperty = BindableProperty.Create(
        nameof(SelectedValue), typeof(string), typeof(FilterChipRow), null, BindingMode.TwoWay, propertyChanged: OnChanged);

    private readonly HorizontalStackLayout _row = new() { Spacing = 8 };

    public IEnumerable<FilterChipOption>? ItemsSource
    {
        get => (IEnumerable<FilterChipOption>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public string? SelectedValue
    {
        get => (string?)GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    public FilterChipRow()
    {
        Orientation = ScrollOrientation.Horizontal;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Never;
        Content = _row;
        Rebuild();
    }

    private static void OnChanged(BindableObject bindable, object oldValue, object newValue) => ((FilterChipRow)bindable).Rebuild();

    private void Rebuild()
    {
        _row.Children.Clear();
        if (ItemsSource is null || Application.Current is not { } app) return;

        foreach (var opt in ItemsSource)
        {
            var isActive = opt.Value == SelectedValue;
            var button = new Button
            {
                Text = opt.Count is > 0 ? $"{opt.Label} ({opt.Count})" : opt.Label,
                Style = (Style)app.Resources["ChipButton"]
            };
            if (isActive)
            {
                button.BackgroundColor = (Color)app.Resources["PrimaryLight"];
                button.TextColor = (Color)app.Resources["PrimaryDarkText"];
            }

            var value = opt.Value;
            button.Clicked += (_, _) => SelectedValue = value;
            _row.Children.Add(button);
        }
    }
}
