using SocietyManagement.Application.Common.Interfaces;

namespace SocietyManagement.Infrastructure.Services;

public class DateTimeService : IDateTime
{
    public DateTime UtcNow => DateTime.UtcNow;
}
