namespace ShalimarApp.Features.Crm;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class FixedClock : IClock
{
    public DateTimeOffset UtcNow { get; }

    public FixedClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }
}

