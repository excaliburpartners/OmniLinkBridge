using HAI_Shared;
using OmniLinkBridge.OmniLink;
using Serilog;
using System;
using System.Reflection;

namespace OmniLinkBridgeTest.Mock
{
    class MockOmniLinkII : IOmniLinkII
    {
        private static readonly ILogger log = Log.Logger.ForContext(MethodBase.GetCurrentMethod().DeclaringType);

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
            log.Verbose("Sending: {command}, Par1: {par1}, Par2: {par2}", Cmd, Par, Pr2);
            OnSendCommand?.Invoke(null, new SendCommandEventArgs() { Cmd = Cmd, Par = Par, Pr2 = Pr2 });
            return true;
        }
    }
}
