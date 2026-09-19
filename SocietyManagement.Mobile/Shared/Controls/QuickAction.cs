using System.Windows.Input;

namespace SocietyManagement.Mobile.Shared.Controls;

/// <summary>Icon-tile + label shortcut — same contract as Web's
/// QuickActionComponent (Icon, Label, Badge, Command), for the Dashboard's
/// quick-action row (§6/§12 of the strategy doc) and any other "jump
/// straight to X" affordance (e.g. an SOS entry point).</summary>
public class QuickAction : ContentView
{
    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon), typeof(string), typeof(QuickAction), string.Empty, propertyChanged: OnChanged);

    public static readonly BindableProperty LabelTextProperty = BindableProperty.Create(
        nameof(LabelText), typeof(string), typeof(QuickAction), string.Empty, propertyChanged: OnChanged);

    public static readonly BindableProperty BadgeProperty = BindableProperty.Create(
        nameof(Badge), typeof(int?), typeof(QuickAction), null, propertyChanged: OnChanged);

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(QuickAction), null);

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string LabelText
    {
        get => (string)GetValue(LabelTextProperty);
        set => SetValue(LabelTextProperty, value);
    }

    public int? Badge
    {
        get => (int?)GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    private readonly Label _iconLabel = new() { FontSize = 20, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
    private readonly Label _badgeLabel = new()
    {
        FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Colors.White,
        HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, IsVisible = false
    };
    private readonly Label _captionLabel = new() { FontSize = 12, HorizontalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.TailTruncation };

    public QuickAction()
    {
        var res = Application.Current!.Resources;

        var badgeHost = new Border
        {
            StrokeThickness = 0, WidthRequest = 18, HeightRequest = 18, Padding = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 9 },
            BackgroundColor = (Color)res["Danger"],
            HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start,
            Content = _badgeLabel
        };

        var tile = new Border
        {
            StrokeThickness = 0, WidthRequest = 48, HeightRequest = 48,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
            BackgroundColor = (Color)res["PrimaryLight"],
            Content = _iconLabel
        };

        var stack = new VerticalStackLayout
        {
            Spacing = 6, WidthRequest = 76, HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new Grid { Children = { tile, badgeHost } },
                _captionLabel
            }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => { if (Command?.CanExecute(null) == true) Command.Execute(null); };
        GestureRecognizers.Add(tap);

        Content = stack;
        Apply();
    }

    private static void OnChanged(BindableObject bindable, object oldValue, object newValue) => ((QuickAction)bindable).Apply();

    private void Apply()
    {
        _iconLabel.Text = Icon;
        _captionLabel.Text = LabelText;
        _badgeLabel.Text = Badge is > 0 ? Badge.ToString() : string.Empty;
        _badgeLabel.IsVisible = Badge is > 0;
    }
}
