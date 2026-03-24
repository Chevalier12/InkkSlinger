using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private void RunFrameUpdateParticipants(GameTime gameTime)
    {
        var refreshStart = System.Diagnostics.Stopwatch.GetTimestamp();
        RefreshActiveUpdateParticipantsIfNeeded();
        _lastFrameUpdateParticipantRefreshMs = System.Diagnostics.Stopwatch.GetElapsedTime(refreshStart).TotalMilliseconds;

        var updateStart = System.Diagnostics.Stopwatch.GetTimestamp();
        var updatedCount = 0;
        var hottestParticipantType = "none";
        var hottestParticipantMs = 0d;
        for (var i = _activeUpdateParticipants.Count - 1; i >= 0; i--)
        {
            var indexedParticipant = _activeUpdateParticipants[i];
            var participant = indexedParticipant.Participant;
            if (!participant.IsFrameUpdateActive)
            {
                _activeUpdateParticipants.RemoveAt(i);
                continue;
            }

            var participantStart = System.Diagnostics.Stopwatch.GetTimestamp();
            participant.UpdateFromUiRoot(gameTime);
            var participantMs = System.Diagnostics.Stopwatch.GetElapsedTime(participantStart).TotalMilliseconds;
            if (participantMs > hottestParticipantMs)
            {
                hottestParticipantMs = participantMs;
                hottestParticipantType = indexedParticipant.Visual.GetType().Name;
            }

            updatedCount++;
        }

        _lastFrameUpdateParticipantUpdateMs = System.Diagnostics.Stopwatch.GetElapsedTime(updateStart).TotalMilliseconds;
        _lastFrameUpdateParticipantCount = updatedCount;
        _lastHottestFrameUpdateParticipantType = hottestParticipantType;
        _lastHottestFrameUpdateParticipantMs = hottestParticipantMs;
    }

    private void RefreshActiveUpdateParticipantsIfNeeded()
    {
        if (!_activeUpdateParticipantsDirty)
        {
            return;
        }

        EnsureVisualIndexCurrent();
        var participants = _visualIndex.UpdateParticipants;
        _activeUpdateParticipants.Clear();
        for (var i = 0; i < participants.Count; i++)
        {
            var indexedParticipant = participants[i];
            if (!indexedParticipant.Participant.IsFrameUpdateActive)
            {
                continue;
            }

            _activeUpdateParticipants.Add(indexedParticipant);
        }

        _lastFrameUpdateParticipantRefreshCount++;
        _activeUpdateParticipantsDirty = false;
    }
}
