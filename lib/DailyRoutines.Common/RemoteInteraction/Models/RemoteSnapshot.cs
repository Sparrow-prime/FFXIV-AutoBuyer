using DailyRoutines.Common.RemoteInteraction.Enums;

namespace DailyRoutines.Common.RemoteInteraction.Models;

public readonly record struct RemoteSnapshot<T>
(
    RemoteSnapshotStatus Status,
    T?                   Value,
    DateTime             UpdatedAtUTC,
    Exception?           Error
)
{
    public bool HasValue => Status is RemoteSnapshotStatus.Ready or RemoteSnapshotStatus.Refreshing;
}
