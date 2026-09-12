namespace DailyRoutines.Common.RemoteInteraction.Abstractions;

public interface IRemoteCommandHandler<in TArgs, TResult>
{
    abstract static ValueTask<TResult> ExecuteAsync(TArgs args, CancellationToken cancellationToken);
}
