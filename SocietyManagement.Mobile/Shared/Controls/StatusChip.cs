namespace SocietyManagement.Mobile.Shared.Controls;

public enum ChipStatus { Success, Warning, Danger, Neutral, Info }

/// <summary>Thin wrapper around the existing chipSuccess/Warning/Danger/
/// Neutral/Info + chipTextX style pairs (Resources/Styles/Styles.xaml) —
/// every screen today hand-writes that Border+Label pair itself (bills,
/// fines, complaints, visitors); this gives it one reusable control instead
/// of a fourth copy. No new visual design — Style/Apply below only ever
/// looks up the styles that already exist.</summary>
public class StatusChip : Border
{
    public static readonly BindableProperty StatusProperty = BindableProperty.Create(
        nameof(Status), typeof(ChipStatus), typeof(StatusChip), ChipStatus.Neutral, propertyChanged: OnChanged);

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(StatusChip), string.Empty, propertyChanged: OnChanged);

    private readonly Label _label = new();

    public ChipStatus Status
    {
        get => (ChipStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public StatusChip()
    {
        Content = _label;
        Apply();
    }

    private static void OnChanged(BindableObject bindable, object oldValue, object newValue) => ((StatusChip)bindable).Apply();

    private void Apply()
    {
        if (Application.Current is not { } app) return;
        Style = (Style)app.Resources[$"chip{Status}"];
        _label.Style = (Style)app.Resources[$"chipText{Status}"];
        _label.Text = Text;
    }
}
