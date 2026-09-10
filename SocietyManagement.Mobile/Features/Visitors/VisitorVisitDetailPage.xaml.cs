using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Visitors;

public partial class VisitorVisitDetailPage : ContentPage, IQueryAttributable
{
    public VisitorVisitDetailPage()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("visit", out var visit) && visit is VisitorVisitDto dto)
            BindingContext = dto;
    }
}
