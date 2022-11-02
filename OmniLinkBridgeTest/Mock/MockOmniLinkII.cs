using HAI_Shared;
using OmniLinkBridge.OmniLink;
using System;

namespace OmniLinkBridgeTest.Mock
{
    class MockOmniLinkII : IOmniLinkII
    {
        public clsHAC Controller { get; private set; }

        public event EventHandler<SendCommandEventArgs> OnSendCommand;

        public MockOmniLinkII()
        {
            Controller = new clsHAC
            {
                Model = enuModel.OMNI_PRO_II,
                TempFormat = enuTempFormat.Fahrenheit
            };
        }

        public bool SendCommand(enuUnitCommand Cmd, byte Par, ushort Pr2)
        {
            OnSendCommand?.Invoke(null, new SendCommandEventArgs() { Cmd = Cmd, Par = Par, Pr2 = Pr2 });
            return true;
        }
    }
}
