namespace DailyRoutines.Common.RemoteInteraction.Models;

public readonly record struct RemoteCommandHandle<TResult>
(
    Guid ID
);
