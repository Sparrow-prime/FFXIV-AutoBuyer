namespace DailyRoutines.Common.RemoteInteraction.Abstractions;

public interface IRemoteQueryHandler<in TKey, TValue>
    where TKey : notnull
{
    abstract static TimeSpan TTL { get; }

    abstract static TimeSpan FailureTTL { get; }

    abstract static ValueTask<TValue> FetchAsync(TKey key, CancellationToken cancellationToken);
}
