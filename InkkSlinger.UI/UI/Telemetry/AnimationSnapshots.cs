namespace InkkSlinger.UI.Telemetry;

/// <summary>
/// Animation system telemetry snapshots.
/// These snapshots provide telemetry data for animation playback, composition, and lane management.
/// </summary>
internal readonly record struct AnimationTelemetrySnapshot(
    int BeginStoryboardCallCount,
    int StoryboardStartCount,
    int ActiveStoryboardCount,
    int ActiveLaneCount,
    int ActiveStoryboardEntryCount,
    int ComposePassCount,
    int LaneApplicationCount,
    int SinkValueSetCount,
    int ClearedLaneCount,
    double BeginStoryboardMilliseconds,
    double StoryboardStartMilliseconds,
    double StoryboardUpdateMilliseconds,
    double ComposeMilliseconds,
    double ComposeCollectMilliseconds,
    double ComposeSortMilliseconds,
    double ComposeMergeMilliseconds,
    double ComposeApplyMilliseconds,
    double ComposeBatchBeginMilliseconds,
    double ComposeBatchEndMilliseconds,
    double SinkSetValueMilliseconds,
    double CleanupCompletedMilliseconds,
    string HottestSetValuePathSummary,
    string HottestSetValueWriteSummary,
    double HottestSetValueWriteMilliseconds,
    // New telemetry fields
    int PauseStoryboardCallCount,
    int ResumeStoryboardCallCount,
    int StopStoryboardCallCount,
    int RemoveStoryboardCallCount,
    int SeekStoryboardCallCount,
    int SetStoryboardSpeedRatioCallCount,
    int SkipStoryboardToFillCallCount,
    int FindInstancesCallCount,
    int FindInstancesIterations,
    int HasLiveContributionForLaneCallCount,
    int HasLiveContributionForLaneTrueCount,
    int HasLiveContributionForLaneFalseCount,
    int InvalidateFrozenLaneStateKeyCallCount,
    int RemoveFromLaneCallCount,
    int RemoveFromLaneNoopCount,
    int RemoveFromLanesCallCount,
    int RemoveFromLanesNoopCount,
    int TryResolveControllableCallCount,
    int TryResolveControllableHitCount,
    int TryResolveControllableMissCount,
    int SetControllableSpeedRatioCallCount,
    int CleanupCompletedStoryboardsCallCount,
    int CleanupCompletedStoryboardsRemovedCount,
    int ClearActiveLaneBufferCallCount,
    int PreparedStoryboardMetadataCacheHits,
    int PreparedStoryboardMetadataCacheMisses);

/// <summary>
/// Animation sink telemetry for dependency property and CLR property set operations.
/// </summary>
internal readonly record struct AnimationSinkTelemetrySnapshot(
    int DependencyPropertySetValueCount,
    double DependencyPropertySetValueMilliseconds,
    int ClrPropertySetValueCount,
    double ClrPropertySetValueMilliseconds);
