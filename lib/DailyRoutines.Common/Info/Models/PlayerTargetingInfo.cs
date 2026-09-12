namespace DailyRoutines.Common.Info.Models;

public readonly record struct PlayerTargetingInfo
(
    IPlayerCharacter Player,
    DateTime         TargetingStartTime,
    int              TargetingDurationSeconds,
    bool             IsNew = false
);
