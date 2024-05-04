using HAI_Shared;
using System;

namespace OmniLinkBridgeTest.Mock
{
    public class SendCommandEventArgs : EventArgs
    {
        public enuUnitCommand Cmd;
        public byte Par;
        public ushort Pr2;

        public SendCommandEventArgs()
        {

        }

        public SendCommandEventArgs(enuUnitCommand cmd, byte par, ushort pr2)
        {
            Cmd = cmd;
            Par = par;
            Pr2 = pr2;
        }

        public override bool Equals(object other)
        {
            if (!(other is SendCommandEventArgs toCompareWith))
                return false;

            return Cmd == toCompareWith.Cmd &&
                Par == toCompareWith.Par &&
                Pr2 == toCompareWith.Pr2;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
