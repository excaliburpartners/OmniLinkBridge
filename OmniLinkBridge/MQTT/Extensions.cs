using HAI_Shared;

namespace OmniLinkBridge.MQTT
{
    public static class Extensions
    {
        public static AreaCommandCode ToCommandCode(this string payload, bool supportValidate = false)
        {
            string[] payloads = payload.Split(',');
            int code = 0;

            AreaCommandCode ret = new AreaCommandCode()
            {
                Command = payloads[0]
            };

            if (payload.Length == 1)
                return ret;

            if (payloads.Length == 2)
            {
                ret.Success = int.TryParse(payloads[1], out code);
            }
            else if (supportValidate && payloads.Length == 3)
            {
                // Special case for Home Assistant when code not required
                if (string.Compare(payloads[1], "validate", true) == 0 &&
                    string.Compare(payloads[2], "None", true) == 0)
                {
                    ret.Success = true;
                }
                else if (string.Compare(payloads[1], "validate", true) == 0)
                {
                    ret.Validate = true;
                    ret.Success = int.TryParse(payloads[2], out code);
                }
                else
                    ret.Success = false;
            }

            ret.Code = code;
            return ret;
        }

        public static UnitType ToUnitType(this clsUnit unit)
        {
            Global.mqtt_discovery_override_unit.TryGetValue(unit.Number, out OverrideUnit override_unit);

            if (unit.Type == enuOL2UnitType.Output)
                return UnitType.@switch;

            if (unit.Type == enuOL2UnitType.Flag)
            {
                if (override_unit != null && override_unit.type == UnitType.number)
                    return UnitType.number;

                return UnitType.@switch;
            }

            if (override_unit != null && override_unit.type == UnitType.@switch)
                return UnitType.@switch;

            return UnitType.light;
        }
    }
}
