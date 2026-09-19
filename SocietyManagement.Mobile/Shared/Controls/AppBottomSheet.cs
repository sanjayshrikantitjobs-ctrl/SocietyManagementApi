using CommunityToolkit.Maui.Extensions;
using Microsoft.Maui.Controls.Shapes;

namespace SocietyManagement.Mobile.Shared.Controls;

/// <summary>Generic bottom-anchored sheet built on CommunityToolkit.Maui's
/// Popup infrastructure (modal backdrop, dismiss-on-tap-outside, lifecycle)
/// rather than a hand-rolled sliding overlay/gesture recognizer — any View
/// can be dropped in as content. First consumer is Step 5's Festival Detail
/// tab overflow; also the natural fit for any future multi-choice action
/// sheet beyond what Shell's own DisplayActionSheet covers.</summary>
public static class AppBottomSheet
{
    public static Task ShowAsync(Page anchor, View content, string? title = null)
    {
        var res = Application.Current!.Resources;

        var stack = new VerticalStackLayout { Spacing = 12, Padding = new Thickness(20, 18) };

        stack.Children.Add(new BoxView
        {
            HeightRequest = 4, WidthRequest = 40, CornerRadius = 2,
            Color = (Color)res["BorderColor"],
            HorizontalOptions = LayoutOptions.Center
        });

        if (!string.IsNullOrWhiteSpace(title))
        {
            stack.Children.Add(new Label { Text = title, Style = (Style)res["SectionTitle"] });
        }

        stack.Children.Add(content);

        var sheet = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(20, 20, 0, 0) },
            StrokeThickness = 0,
            BackgroundColor = (Color)res[Application.Current.RequestedTheme == AppTheme.Dark ? "Gray900" : "Surface"],
            Content = stack,
            VerticalOptions = LayoutOptions.End,
            HorizontalOptions = LayoutOptions.Fill
        };

        // Dismiss-by-tapping-outside is CommunityToolkit.Maui's own default
        // for this overload — no options object needed to get it.
        return anchor.ShowPopupAsync(sheet);
    }
}
