using System;
using Serilog.Core;
using Serilog.Events;

namespace OmniLinkBridge
{
    public class ControllerEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if(Global.controller_id != Guid.Empty)
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                    "ControllerId", Global.controller_id));
        }
    }
}
