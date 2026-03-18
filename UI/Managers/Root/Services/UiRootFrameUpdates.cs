using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private void RunFrameUpdateParticipants(GameTime gameTime)
    {
        RefreshActiveUpdateParticipantsIfNeeded();
        var updatedCount = 0;
        for (var i = _activeUpdateParticipants.Count - 1; i >= 0; i--)
        {
            var indexedParticipant = _activeUpdateParticipants[i];
            var participant = indexedParticipant.Participant;
            if (!participant.IsFrameUpdateActive || !IsElementConnectedToVisualRoot(indexedParticipant.Visual))
            {
                _activeUpdateParticipants.RemoveAt(i);
                continue;
            }

            participant.UpdateFromUiRoot(gameTime);
            updatedCount++;
        }

        _lastFrameUpdateParticipantCount = updatedCount;
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
