using HAI_Shared;
using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace HAILogger
{
    static class Helper
    {
        public static AreaContract ConvertArea(ushort id, clsArea area)
        {
            AreaContract ret = new AreaContract();

            ret.id = id;
            ret.name = area.Name;     
            ret.burglary = area.AreaBurglaryAlarmText;
            ret.co = area.AreaGasAlarmText;
            ret.fire = area.AreaFireAlarmText;
            ret.water = area.AreaWaterAlarmText;

            string mode = area.ModeText();

            if (mode.Contains("DAY"))
                ret.mode = "DAY";
            else if (mode.Contains("NIGHT"))
                ret.mode = "NIGHT";
            else if (mode.Contains("AWAY"))
                ret.mode = "AWAY";
            else if (mode.Contains("VACATION"))
                ret.mode = "VACATION";
            else
                ret.mode = "OFF";

            return ret;
        }

        public static ZoneContract ConvertZone(ushort id, clsZone zone)
        {
            ZoneContract ret = new ZoneContract();

            ret.id = id;
            ret.zonetype = zone.ZoneType;
            ret.name = zone.Name;
            ret.status = zone.StatusText();
            ret.temp = zone.TempText();

            return ret;
        }

        public static UnitContract ConvertUnit(ushort id, clsUnit unit)
        {
            UnitContract ret = new UnitContract();

            ret.id = id;
            ret.name = unit.Name;

            if (unit.Status > 100)
                ret.level = (ushort)(unit.Status - 100);
            else if (unit.Status == 1)
                ret.level = 100;
            else
                ret.level = 0;

            return ret;
        }

        public static ThermostatContract ConvertThermostat(ushort id, clsThermostat unit)
        {
            ThermostatContract ret = new ThermostatContract();

            ret.id = id;
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

        public static int ConvertTemperature(int f)
        {
            // Convert to celsius
            double c = 5.0 / 9.0 * (f - 32);

            // Convert to omni temp (0 is -40C and 255 is 87.5C)
            return (int)Math.Round((c + 40) * 2, 0);
        }

        public static string Serialize<T>(T obj)
        {
            MemoryStream stream = new MemoryStream();
            DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(T));
            ser.WriteObject(stream, obj);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
