using ClaimsModule.Application.Common.Interfaces;

namespace ClaimsModule.Infrastructure.Services;

public class DateTimeService : IDateTime
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
