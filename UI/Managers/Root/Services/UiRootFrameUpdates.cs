using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private void RunFrameUpdateParticipants(GameTime gameTime)
    {
        EnsureVisualIndexCurrent();
        var participants = _visualIndex.UpdateParticipants;
        var updatedCount = 0;
        for (var i = 0; i < participants.Count; i++)
        {
            var participant = participants[i].Participant;
            if (!participant.IsFrameUpdateActive)
            {
                continue;
            }

            participant.UpdateFromUiRoot(gameTime);
            updatedCount++;
        }

        _lastFrameUpdateParticipantCount = updatedCount;
    }
}
