using HAI_Shared;
using OmniLinkBridge.WebAPI;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace OmniLinkBridge.WebAPI
{
    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class OmniLinkService : IOmniLinkService
    {
        private static readonly ILogger log = Log.Logger.ForContext(MethodBase.GetCurrentMethod().DeclaringType);

        public void Subscribe(SubscribeContract contract)
        {
            log.Debug("Subscribe");
            WebNotification.AddSubscription(contract.callback);
        }

        public List<NameContract> ListAreas()
        {
            log.Debug("ListAreas");

            List<NameContract> names = new List<NameContract>();
            for (ushort i = 1; i <= WebServiceModule.OmniLink.Controller.Areas.Count; i++)
            {
                clsArea area = WebServiceModule.OmniLink.Controller.Areas[i];

                // PC Access doesn't let you customize the area name for the Omni LTe or Omni IIe
                // (configured for 1 area). To workaround ignore default properties for the first area.
                if (i == 1 || area.DefaultProperties == false)
                    names.Add(new NameContract() { id = i, name = area.Name });
            }
            return names;
        }

        public AreaContract GetArea(ushort id)
        {
            log.Debug("GetArea: {id}", id);

            WebOperationContext ctx = WebOperationContext.Current;
            ctx.OutgoingResponse.Headers.Add("type", "area");

            return WebServiceModule.OmniLink.Controller.Areas[id].ToContract();
        }

        public List<NameContract> ListZonesContact()
        {
            log.Debug("ListZonesContact");

            List<NameContract> names = new List<NameContract>();
            for (ushort i = 1; i <= WebServiceModule.OmniLink.Controller.Zones.Count; i++)
            {
                clsZone zone = WebServiceModule.OmniLink.Controller.Zones[i];

                if (zone.DefaultProperties == false && zone.ToDeviceType() == DeviceType.contact)
                    names.Add(new NameContract() { id = i, name = zone.Name });
            }
            return names;
        }

        public List<NameContract> ListZonesMotion()
        {
            log.Debug("ListZonesMotion");

            List<NameContract> names = new List<NameContract>();
            for (ushort i = 1; i <= WebServiceModule.OmniLink.Controller.Zones.Count; i++)
            {
                clsZone zone = WebServiceModule.OmniLink.Controller.Zones[i];

                if (zone.DefaultProperties == false && zone.ToDeviceType() == DeviceType.motion)
                    names.Add(new NameContract() { id = i, name = zone.Name });
            }
            return names;
        }

        public List<NameContract> ListZonesWater()
        {
            log.Debug("ListZonesWater");

            List<NameContract> names = new List<NameContract>();
            for (ushort i = 1; i <= WebServiceModule.OmniLink.Controller.Zones.Count; i++)
            {
                clsZone zone = WebServiceModule.OmniLink.Controller.Zones[i];

                if (zone.DefaultProperties == false && zone.ToDeviceType() == DeviceType.water)
                    names.Add(new NameContract() { id = i, name = zone.Name });
            }
            return names;
        }

        public List<NameContract> ListZonesSmoke()
        {
            log.Debug("ListZonesSmoke");

            List<NameContract> names = new List<NameContract>();
            for (ushort i = 1; i <= WebServiceModule.OmniLink.Controller.Zones.Count; i++)
            {
                clsZone zone = WebServiceModule.OmniLink.Controller.Zones[i];

                if (zone.DefaultProperties == false && zone.ToDeviceType() == DeviceType.smoke)
                    names.Add(new NameContract() { id = i, name = zone.Name });
            }
            return names;
        }

        public List<NameContract> ListZonesCO()
        {
            log.Debug("ListZonesCO");

            List<NameContract> names = new List<NameContract>();
            for (ushort i = 1; i <= WebServiceModule.OmniLink.Controller.Zones.Count; i++)
            {
                clsZone zone = WebServiceModule.OmniLink.Controller.Zones[i];

                if (zone.DefaultProperties == false && zone.ToDeviceType() == DeviceType.co)
                    names.Add(new NameContract() { id = i, name = zone.Name });
            }
            return names;
        }

        public List<NameContract> ListZonesTemp()
        {
            log.Debug("ListZonesTemp");

            List<NameContract> names = new List<NameContract>();
            for (ushort i = 1; i <= WebServiceModule.OmniLink.Controller.Zones.Count; i++)
            {
                clsZone zone = WebServiceModule.OmniLink.Controller.Zones[i];

                if (zone.DefaultProperties == false && zone.IsTemperatureZone())
                    names.Add(new NameContract() { id = i, name = zone.Name });
            }
            return names;
        }

        public ZoneContract GetZone(ushort id)
        {
            log.Debug("GetZone: {id}", id);

            WebOperationContext ctx = WebOperationContext.Current;

            if (WebServiceModule.OmniLink.Controller.Zones[id].IsTemperatureZone())
            {
                ctx.OutgoingResponse.Headers.Add("type", "temp");
            }
            else
            {
                ctx.OutgoingResponse.Headers.Add("type", Enum.GetName(typeof(DeviceType), 
                    WebServiceModule.OmniLink.Controller.Zones[id].ToDeviceType()));
            }

            return WebServiceModule.OmniLink.Controller.Zones[id].ToContract();
        }

        public List<NameContract> ListUnits()
        {
            log.Debug("ListUnits");

            List<NameContract> names = new List<NameContract>();
            for (ushort i = 1; i <= WebServiceModule.OmniLink.Controller.Units.Count; i++)
            {
                clsUnit unit = WebServiceModule.OmniLink.Controller.Units[i];

                if (unit.DefaultProperties == false)
                    names.Add(new NameContract() { id = i, name = unit.Name });
            }
            return names;
        }

        public UnitContract GetUnit(ushort id)
        {
            log.Debug("GetUnit: {id}", id);

            WebOperationContext ctx = WebOperationContext.Current;
            ctx.OutgoingResponse.Headers.Add("type", "unit");

            return WebServiceModule.OmniLink.Controller.Units[id].ToContract();
        }

        public void SetUnit(CommandContract unit)
        {
            log.Debug("SetUnit: {id} to {value}%", unit.id, unit.value);

            if (unit.value == 0)
                WebServiceModule.OmniLink.Controller.SendCommand(enuUnitCommand.Off, 0, unit.id);
            else if (unit.value == 100)
                WebServiceModule.OmniLink.Controller.SendCommand(enuUnitCommand.On, 0, unit.id);
            else
                WebServiceModule.OmniLink.Controller.SendCommand(enuUnitCommand.Level, BitConverter.GetBytes(unit.value)[0], unit.id);
        }


        public void SetUnitKeypadPress(CommandContract unit)
        {
            log.Debug("SetUnitKeypadPress: {id} to {value}", unit.id, unit.value);
            WebServiceModule.OmniLink.Controller.SendCommand(enuUnitCommand.LutronHomeWorksKeypadButtonPress, BitConverter.GetBytes(unit.value)[0], unit.id);
        }

        public List<NameContract> ListThermostats()
        {
            log.Debug("ListThermostats");

            List<NameContract> names = new List<NameContract>();
            for (ushort i = 1; i <= WebServiceModule.OmniLink.Controller.Thermostats.Count; i++)
            {
                clsThermostat unit = WebServiceModule.OmniLink.Controller.Thermostats[i];

                if (unit.DefaultProperties == false)
                    names.Add(new NameContract() { id = i, name = unit.Name });
            }
            return names;
        }

        public ThermostatContract GetThermostat(ushort id)
        {
            log.Debug("GetThermostat: {id}", id);

            WebOperationContext ctx = WebOperationContext.Current;
            ctx.OutgoingResponse.Headers.Add("type", "thermostat");

            return WebServiceModule.OmniLink.Controller.Thermostats[id].ToContract();
        }

        public void SetThermostatCoolSetpoint(CommandContract unit)
        {
            double tempHigh = unit.value;
            string tempUnit = "C";
            if (WebServiceModule.OmniLink.Controller.TempFormat == enuTempFormat.Fahrenheit)
            {
                tempHigh = tempHigh.ToCelsius();
                tempUnit = "F";
            }

            int temp = tempHigh.ToOmniTemp();
            log.Debug("SetThermostatCoolSetpoint: {id} to {value}{tempUnit} {temp}", unit.id, unit.value, tempUnit, temp);
            WebServiceModule.OmniLink.Controller.SendCommand(enuUnitCommand.SetHighSetPt, BitConverter.GetBytes(temp)[0], unit.id);
        }

        public void SetThermostatHeatSetpoint(CommandContract unit)
        {
            double tempLoad = unit.value;
            string tempUnit = "C";
            if (WebServiceModule.OmniLink.Controller.TempFormat == enuTempFormat.Fahrenheit)
            {
                tempLoad = tempLoad.ToCelsius();
                tempUnit = "F";
            }

            int temp = tempLoad.ToOmniTemp();
            log.Debug("SetThermostatHeatSetpoint: {id} to {value}{tempUnit} {temp}", unit.id, unit.value, tempUnit, temp);
            WebServiceModule.OmniLink.Controller.SendCommand(enuUnitCommand.SetLowSetPt, BitConverter.GetBytes(temp)[0], unit.id);
        }

        public void SetThermostatMode(CommandContract unit)
        {
            log.Debug("SetThermostatMode: {id} to {value}", unit.id, unit.value);
            WebServiceModule.OmniLink.Controller.SendCommand(enuUnitCommand.Mode, BitConverter.GetBytes(unit.value)[0], unit.id);
        }

        public void SetThermostatFanMode(CommandContract unit)
        {
            log.Debug("SetThermostatFanMode: {id} to {value}", unit.id, unit.value);
            WebServiceModule.OmniLink.Controller.SendCommand(enuUnitCommand.Fan, BitConverter.GetBytes(unit.value)[0], unit.id);
        }

        public void SetThermostatHold(CommandContract unit)
        {
            log.Debug("SetThermostatHold: {id} to {value}", unit.id, unit.value);
            WebServiceModule.OmniLink.Controller.SendCommand(enuUnitCommand.Hold, BitConverter.GetBytes(unit.value)[0], unit.id);
        }

        public List<NameContract> ListButtons()
        {
            log.Debug("ListButtons");

            List<NameContract> names = new List<NameContract>();
            for (ushort i = 1; i <= WebServiceModule.OmniLink.Controller.Buttons.Count; i++)
            {
                clsButton unit = WebServiceModule.OmniLink.Controller.Buttons[i];

                if (unit.DefaultProperties == false)
                    names.Add(new NameContract() { id = i, name = unit.Name });
            }
            return names;
        }

        public void PushButton(CommandContract unit)
        {
            log.Debug("PushButton: {id}", unit.id);
            WebServiceModule.OmniLink.Controller.SendCommand(enuUnitCommand.Button, 0, unit.id);
        }
    }
}