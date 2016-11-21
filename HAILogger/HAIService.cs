using HAI_Shared;
using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace HAILogger
{
    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class HAIService : IHAIService
    {
        public void Subscribe(SubscribeContract contract)
        {
            Event.WriteVerbose("WebService", "Subscribe");
            WebNotification.AddSubscription(contract.callback);
        }

        public List<NameContract> ListAreas()
        {
            Event.WriteVerbose("WebService", "ListAreas");

            List<NameContract> names = new List<NameContract>();
            for (ushort i = 1; i < WebService.HAC.Areas.Count; i++)
            {
                clsArea area = WebService.HAC.Areas[i];

                if (area.DefaultProperties == false)
                    names.Add(new NameContract() { id = i, name = area.Name });
            }
            return names;
        }

        public AreaContract GetArea(ushort id)
        {
            Event.WriteVerbose("WebService", "GetArea: " + id);

            WebOperationContext ctx = WebOperationContext.Current;
            ctx.OutgoingResponse.Headers.Add("type", "area");

            clsArea area = WebService.HAC.Areas[id];
            return Helper.ConvertArea(id, area);
        }

        public List<NameContract> ListZonesContact()
        {
            Event.WriteVerbose("WebService", "ListZonesContact");

            List<NameContract> names = new List<NameContract>();
            for (ushort i = 1; i < WebService.HAC.Zones.Count; i++)
            {
                clsZone zone = WebService.HAC.Zones[i];

                if ((zone.ZoneType == enuZoneType.EntryExit ||
                    zone.ZoneType == enuZoneType.X2EntryDelay ||
                    zone.ZoneType == enuZoneType.X4EntryDelay ||
                    zone.ZoneType == enuZoneType.Perimeter) && zone.DefaultProperties == false)
                    names.Add(new NameContract() { id = i, name = zone.Name });
            }
            return names;
        }

        public List<NameContract> ListZonesMotion()
        {
            Event.WriteVerbose("WebService", "ListZonesMotion");

            List<NameContract> names = new List<NameContract>();
            for (ushort i = 1; i < WebService.HAC.Zones.Count; i++)
            {
                clsZone zone = WebService.HAC.Zones[i];

                if (zone.ZoneType == enuZoneType.AwayInt && zone.DefaultProperties == false)
                    names.Add(new NameContract() { id = i, name = zone.Name });
            }
            return names;
        }

        public List<NameContract> ListZonesWater()
        {
            Event.WriteVerbose("WebService", "ListZonesWater");

            List<NameContract> names = new List<NameContract>();
            for (ushort i = 1; i < WebService.HAC.Zones.Count; i++)
            {
                clsZone zone = WebService.HAC.Zones[i];

                if (zone.ZoneType == enuZoneType.Water && zone.DefaultProperties == false)
                    names.Add(new NameContract() { id = i, name = zone.Name });
            }
            return names;
        }

        public List<NameContract> ListZonesSmoke()
        {
            Event.WriteVerbose("WebService", "ListZonesSmoke");

            List<NameContract> names = new List<NameContract>();
            for (ushort i = 1; i < WebService.HAC.Zones.Count; i++)
            {
                clsZone zone = WebService.HAC.Zones[i];

                if (zone.ZoneType == enuZoneType.Fire && zone.DefaultProperties == false)
                    names.Add(new NameContract() { id = i, name = zone.Name });
            }
            return names;
        }

        public List<NameContract> ListZonesCO()
        {
            Event.WriteVerbose("WebService", "ListZonesCO");

            List<NameContract> names = new List<NameContract>();
            for (ushort i = 1; i < WebService.HAC.Zones.Count; i++)
            {
                clsZone zone = WebService.HAC.Zones[i];

                if (zone.ZoneType == enuZoneType.Gas && zone.DefaultProperties == false)
                    names.Add(new NameContract() { id = i, name = zone.Name });
            }
            return names;
        }

        public List<NameContract> ListZonesTemp()
        {
            Event.WriteVerbose("WebService", "ListZonesTemp");

            List<NameContract> names = new List<NameContract>();
            for (ushort i = 1; i < WebService.HAC.Zones.Count; i++)
            {
                clsZone zone = WebService.HAC.Zones[i];

                if (zone.IsTemperatureZone() && zone.DefaultProperties == false)
                    names.Add(new NameContract() { id = i, name = zone.Name });
            }
            return names;
        }

        public ZoneContract GetZone(ushort id)
        {
            Event.WriteVerbose("WebService", "GetZone: " + id);

            WebOperationContext ctx = WebOperationContext.Current;

            if (WebService.HAC.Zones[id].IsTemperatureZone())
            {
                ctx.OutgoingResponse.Headers.Add("type", "temp");
            }
            else
            {
                switch (WebService.HAC.Zones[id].ZoneType)
                {
                    case enuZoneType.EntryExit:
                    case enuZoneType.X2EntryDelay:
                    case enuZoneType.X4EntryDelay:
                    case enuZoneType.Perimeter:
                        ctx.OutgoingResponse.Headers.Add("type", "contact");
                        break;
                    case enuZoneType.AwayInt:
                        ctx.OutgoingResponse.Headers.Add("type", "motion");
                        break;
                    case enuZoneType.Water:
                        ctx.OutgoingResponse.Headers.Add("type", "water");
                        break;
                    case enuZoneType.Fire:
                        ctx.OutgoingResponse.Headers.Add("type", "smoke");
                        break;
                    case enuZoneType.Gas:
                        ctx.OutgoingResponse.Headers.Add("type", "co");
                        break;
                    default:
                        ctx.OutgoingResponse.Headers.Add("type", "unknown");
                        break;
                }
            }

            clsZone unit = WebService.HAC.Zones[id];
            return Helper.ConvertZone(id, unit);
        }

        public List<NameContract> ListUnits()
        {
            Event.WriteVerbose("WebService", "ListUnits");

            List<NameContract> names = new List<NameContract>();
            for (ushort i = 1; i < WebService.HAC.Units.Count; i++)
            {
                clsUnit unit = WebService.HAC.Units[i];

                if (unit.DefaultProperties == false)
                    names.Add(new NameContract() { id = i, name = unit.Name });
            }
            return names;
        }

        public UnitContract GetUnit(ushort id)
        {
            Event.WriteVerbose("WebService", "GetUnit: " + id);

            WebOperationContext ctx = WebOperationContext.Current;
            ctx.OutgoingResponse.Headers.Add("type", "unit");

            clsUnit unit = WebService.HAC.Units[id];
            return Helper.ConvertUnit(id, unit);
        }

        public void SetUnit(CommandContract unit)
        {
            Event.WriteVerbose("WebService", "SetUnit: " + unit.id + " to " + unit.value + "%");

            if (unit.value == 0)
                WebService.HAC.SendCommand(enuUnitCommand.Off, 0, unit.id);
            else if (unit.value == 100)
                WebService.HAC.SendCommand(enuUnitCommand.On, 0, unit.id);
            else
                WebService.HAC.SendCommand(enuUnitCommand.Level, BitConverter.GetBytes(unit.value)[0], unit.id);
        }


        public void SetUnitKeypadPress(CommandContract unit)
        {
            Event.WriteVerbose("WebService", "SetUnitKeypadPress: " + unit.id + " to " + unit.value + " button");
            WebService.HAC.SendCommand(enuUnitCommand.LutronHomeWorksKeypadButtonPress, BitConverter.GetBytes(unit.value)[0], unit.id);
        }

        public List<NameContract> ListThermostats()
        {
            Event.WriteVerbose("WebService", "ListThermostats");

            List<NameContract> names = new List<NameContract>();
            for (ushort i = 1; i < WebService.HAC.Thermostats.Count; i++)
            {
                clsThermostat unit = WebService.HAC.Thermostats[i];

                if (unit.DefaultProperties == false)
                    names.Add(new NameContract() { id = i, name = unit.Name });
            }
            return names;
        }

        public ThermostatContract GetThermostat(ushort id)
        {
            Event.WriteVerbose("WebService", "GetThermostat: " + id);

            WebOperationContext ctx = WebOperationContext.Current;
            ctx.OutgoingResponse.Headers.Add("type", "thermostat");

            clsThermostat unit = WebService.HAC.Thermostats[id];
            return Helper.ConvertThermostat(id, unit);
        }

        public void SetThermostatCoolSetpoint(CommandContract unit)
        {
            int temp = Helper.ConvertTemperature(unit.value);
            Event.WriteVerbose("WebService", "SetThermostatCoolSetpoint: " + unit.id + " to " + unit.value + "F (" + temp + ")");
            WebService.HAC.SendCommand(enuUnitCommand.SetHighSetPt, BitConverter.GetBytes(temp)[0], unit.id);
        }

        public void SetThermostatHeatSetpoint(CommandContract unit)
        {
            int temp = Helper.ConvertTemperature(unit.value);
            Event.WriteVerbose("WebService", "SetThermostatCoolSetpoint: " + unit.id + " to " + unit.value + "F (" + temp + ")");
            WebService.HAC.SendCommand(enuUnitCommand.SetLowSetPt, BitConverter.GetBytes(temp)[0], unit.id);
        }

        public void SetThermostatMode(CommandContract unit)
        {
            Event.WriteVerbose("WebService", "SetThermostatMode: " + unit.id + " to " + unit.value);
            WebService.HAC.SendCommand(enuUnitCommand.Mode, BitConverter.GetBytes(unit.value)[0], unit.id);
        }

        public void SetThermostatFanMode(CommandContract unit)
        {
            Event.WriteVerbose("WebService", "SetThermostatFanMode: " + unit.id + " to " + unit.value);
            WebService.HAC.SendCommand(enuUnitCommand.Fan, BitConverter.GetBytes(unit.value)[0], unit.id);
        }

        public void SetThermostatHold(CommandContract unit)
        {
            Event.WriteVerbose("WebService", "SetThermostatHold: " + unit.id + " to " + unit.value);
            WebService.HAC.SendCommand(enuUnitCommand.Hold, BitConverter.GetBytes(unit.value)[0], unit.id);
        }

        public List<NameContract> ListButtons()
        {
            Event.WriteVerbose("WebService", "ListButtons");

            List<NameContract> names = new List<NameContract>();
            for (ushort i = 1; i < WebService.HAC.Buttons.Count; i++)
            {
                clsButton unit = WebService.HAC.Buttons[i];

                if (unit.DefaultProperties == false)
                    names.Add(new NameContract() { id = i, name = unit.Name });
            }
            return names;
        }

        public void PushButton(CommandContract unit)
        {
            Event.WriteVerbose("WebService", "PushButton: " + unit.id);
            WebService.HAC.SendCommand(enuUnitCommand.Button, 0, unit.id);
        }
    }
}