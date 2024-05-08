using HAI_Shared;

namespace OmniLinkBridge.WebAPI
{
    public static class MappingExtensions
    {
        private static string lastmode = "OFF";

        public static AreaContract ToContract(this clsArea area)
        {
            AreaContract ret = new AreaContract
            {
                id = (ushort)area.Number,
                name = area.Name,
                burglary = area.AreaBurglaryAlarmText,
                co = area.AreaGasAlarmText,
                fire = area.AreaFireAlarmText,
                water = area.AreaWaterAlarmText
            };

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
            ZoneContract ret = new ZoneContract
            {
                id = (ushort)zone.Number,
                zonetype = zone.ZoneType,
                name = zone.Name,
                status = zone.StatusText()
            };

            if (zone.IsTemperatureZone())
                ret.temp = zone.TempText();
            else if(zone.IsHumidityZone())
                ret.temp = zone.TempText();

            return ret;
        }

        public static UnitContract ToContract(this clsUnit unit)
        {
            UnitContract ret = new UnitContract
            {
                id = (ushort)unit.Number,
                name = unit.Name
            };

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
            ThermostatContract ret = new ThermostatContract
            {
                id = (ushort)unit.Number,
                name = unit.Name
            };

            ushort.TryParse(unit.TempText(), out ushort temp);
            ushort.TryParse(unit.HeatSetpointText(), out ushort heat);
            ushort.TryParse(unit.CoolSetpointText(), out ushort cool);
            ushort.TryParse(unit.HumidityText(), out ushort humidity);

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

        public static DeviceType ToDeviceType(this clsZone zone)
        {
            Global.webapi_override_zone.TryGetValue(zone.Number, out OverrideZone override_zone);

            if (override_zone != null)
                return override_zone.device_type;

            switch (zone.ZoneType)
            {
                case enuZoneType.EntryExit:
                case enuZoneType.X2EntryDelay:
                case enuZoneType.X4EntryDelay:
                case enuZoneType.Perimeter:
                case enuZoneType.Tamper:
                case enuZoneType.Auxiliary:
                    return DeviceType.contact;
                case enuZoneType.AwayInt:
                case enuZoneType.NightInt:
                    return DeviceType.motion;
                case enuZoneType.Water:
                    return DeviceType.water;
                case enuZoneType.Fire:
                    return DeviceType.smoke;
                case enuZoneType.Gas:
                    return DeviceType.co;
                default:
                    return DeviceType.unknown;
            }
        }
    }
}
