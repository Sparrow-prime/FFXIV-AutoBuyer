namespace DailyRoutines.Common.RemoteInteraction.Models;

public readonly record struct RemoteCommandSnapshot<TResult>
(
    bool       IsCompleted,
    bool       IsSucceeded,
    TResult?   Result,
    Exception? Error,
    DateTime   StartedAtUTC,
    DateTime?  CompletedAtUTC
);
