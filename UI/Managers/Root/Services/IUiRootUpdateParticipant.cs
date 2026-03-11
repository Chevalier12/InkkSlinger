using Microsoft.Xna.Framework;

namespace InkkSlinger;

internal interface IUiRootUpdateParticipant
{
    bool IsFrameUpdateActive { get; }

    void UpdateFromUiRoot(GameTime gameTime);
}
