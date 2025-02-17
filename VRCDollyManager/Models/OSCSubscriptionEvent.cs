using LucHeart.CoreOSC;

namespace VRCDollyManager.Models;

public class OscSubscriptionEvent(OscMessage message) : EventArgs
{
    public OscMessage Message { get; private set; } = message;
}