using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniLinkBridge.MQTT
{
    public class Topic
    {
        public string Value { get; private set; }

        private Topic(string value)
        {
            Value = value; 
        }

        public override string ToString()
        {
            return Value;
        }

        public static Topic state { get { return new Topic("state"); } }
        public static Topic command { get { return new Topic("command"); } }

        public static Topic brightness_state { get { return new Topic("brightness_state"); } }
        public static Topic brightness_command { get { return new Topic("brightness_command"); } }

        public static Topic current_operation { get { return new Topic("current_operation"); } }
        public static Topic current_temperature { get { return new Topic("current_temperature"); } }
        public static Topic current_humidity { get { return new Topic("current_humidity"); } }

        public static Topic temperature_heat_state { get { return new Topic("temperature_heat_state"); } }
        public static Topic temperature_heat_command { get { return new Topic("temperature_heat_command"); } }

        public static Topic temperature_cool_state { get { return new Topic("temperature_cool_state"); } }
        public static Topic temperature_cool_command { get { return new Topic("temperature_cool_command"); } }

        public static Topic humidify_state { get { return new Topic("humidify_state"); } }
        public static Topic humidify_command { get { return new Topic("humidify_command"); } }

        public static Topic dehumidify_state { get { return new Topic("dehumidify_state"); } }
        public static Topic dehumidify_command { get { return new Topic("dehumidify_command"); } }

        public static Topic mode_state { get { return new Topic("mode_state"); } }
        public static Topic mode_command { get { return new Topic("mode_command"); } }

        public static Topic fan_mode_state { get { return new Topic("fan_mode_state"); } }
        public static Topic fan_mode_command { get { return new Topic("fan_mode_command"); } }

        public static Topic hold_state { get { return new Topic("hold_state"); } }
        public static Topic hold_command { get { return new Topic("hold_command"); } }
    }
}
