using HAI_Shared;

namespace OmniLinkBridge.OmniLink
{
    public interface IOmniLinkII
    {
        clsHAC Controller { get; }

        bool SendCommand(enuUnitCommand Cmd, byte Par, ushort Pr2);
    }
}
