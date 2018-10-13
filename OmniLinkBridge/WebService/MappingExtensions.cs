using HAI_Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniLinkBridge.WebAPI
{
    public static class MappingExtensions
    {
        private static string lastmode = "OFF";

        public static AreaContract ToContract(this clsArea area)
        {
            AreaContract ret = new AreaContract();

            ret.id = (ushort)area.Number;
            ret.name = area.Name;
            ret.burglary = area.AreaBurglaryAlarmText;
            ret.co = area.AreaGasAlarmText;
            ret.fire = area.AreaFireAlarmText;
            ret.water = area.AreaWaterAlarmText;

            if (area.ExitTimer > 0)
            {
                ret.mode = lastmode;
            }
            else
            {
                ret.mode = area.ModeText();
                lastmode = ret.mode;
            }

            return ret;
        }

        public static ZoneContract ToContract(this clsZone zone)
        {
            ZoneContract ret = new ZoneContract();

            ret.id = (ushort)zone.Number;
            ret.zonetype = zone.ZoneType;
            ret.name = zone.Name;
            ret.status = zone.StatusText();
            ret.temp = zone.TempText();

            return ret;
        }

        public static UnitContract ToContract(this clsUnit unit)
        {
            UnitContract ret = new UnitContract();

            ret.id = (ushort)unit.Number;
            ret.name = unit.Name;

            if (unit.Status > 100)
                ret.level = (ushort)(unit.Status - 100);
            else if (unit.Status == 1)
                ret.level = 100;
            else
                ret.level = 0;

            return ret;
        }

        public static ThermostatContract ToContract(this clsThermostat unit)
        {
            ThermostatContract ret = new ThermostatContract();

            ret.id = (ushort)unit.Number;
            ret.name = unit.Name;

            ushort temp, heat, cool, humidity;

            ushort.TryParse(unit.TempText(), out temp);
            ushort.TryParse(unit.HeatSetpointText(), out heat);
            ushort.TryParse(unit.CoolSetpointText(), out cool);
            ushort.TryParse(unit.HumidityText(), out humidity);

            ret.temp = temp;
            ret.humidity = humidity;
            ret.heatsetpoint = heat;
            ret.coolsetpoint = cool;
            ret.mode = unit.Mode;
            ret.fanmode = unit.FanMode;
            ret.hold = unit.HoldStatus;

            string status = unit.HorC_StatusText();

            if (status.Contains("COOLING"))
                ret.status = "COOLING";
            else if (status.Contains("HEATING"))
                ret.status = "HEATING";
            else
                ret.status = "OFF";

            return ret;
        }
    }
}
