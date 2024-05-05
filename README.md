# OmniLink Bridge
Provides MQTT bridge, web service API, time sync, and logging for [HAI/Leviton OmniPro II controllers](https://www.leviton.com/en/products/brands/omni-security-automation). Provides integration with [Samsung SmartThings via web service API](https://github.com/excaliburpartners/SmartThings-OmniPro) and [Home Assistant via MQTT](https://www.home-assistant.io/components/mqtt/).

Please note that OmniLink Bridge is not in active development. The MQTT and Home Assistant integrations are in maintenance mode. The SmartThings Web API and MySQL logging are deprecated and not feature consistent with MQTT.

## Download
You can use docker to build an image from git or download the [binary here](https://github.com/excaliburpartners/OmniLinkBridge/releases/latest/download/OmniLinkBridge.zip). You can also install it as a [Home Assistant Add-on](https://github.com/excaliburpartners/hassio-addons).

## Requirements
- [Docker](https://www.docker.com/)
- .NET Framework 4.7.2 (or Mono equivalent)

## Operation
OmniLink Bridge is divided into the following modules and configurable settings. Configuration settings can also be set as environment variables by using their name in uppercase. Refer to [OmniLinkBridge.ini](OmniLinkBridge/OmniLinkBridge.ini) for specifics.

- OmniLinkII: controller_
    - Maintains connection to the OmniLink controller
    - Thermostats
	    - If no status update has been received after 4 minutes a request is issued
        - A status update containing a temperature of 0 marks the thermostat offline
            - This can occur when a ZigBee thermostat has lost communication
- Time Sync: time_
    - Controller time is checked and compared to the local computer time disregarding time zones
- MQTT: mqtt_
    - Maintains connection to the MQTT broker
    - Publishes discovery topics for [Home Assistant](https://www.home-assistant.io/components/mqtt/) to auto configure devices
    - Publishes topics for status received from the OmniLinkII module
    - Subscribes to command topics and sends commands to the OmniLinkII module
- Web API: webapi_
    - Provides integration with [Samsung SmartThings](https://github.com/excaliburpartners/SmartThings-OmniPro)
    - Allows an application to subscribe to receive POST notifications status updates are received from the OmniLinkII module
        - On failure to POST to callback URL subscription is removed
        - Recommended for application to send subscribe requests every few minutes
    - Requests to GET endpoints return status from the OmniLinkII module
    - Requests to POST endpoints send commands to the OmniLinkII module
- Logger
    - Console output: verbose_
        - Enabled by default
        - Thermostats (verbose_thermostat_timer)
            - After 5 minutes of no status updates a warning will be logged
	        - When a current temperature of 0 is received a warning will be logged
    - MySQL logging: mysql_
        - Thermostats are logged every minute and when an event is received
    - Push notifications: notify_
        - Always sent for area alarms and critical system events
        - Optionally enable for area status changes and console messages
        - Email: mail_
        - Prowl: prowl_
        - Pushover: pushover_

## Docker Hub Quickstart
Quickly get started with console logging by specifying the controller address and encryption keys.
```
docker run --name="omnilink-bridge" \
  -v /etc/localtime:/etc/localtime:ro \
  -e CONTROLLER_ADDRESS='' \
  -e CONTROLLER_KEY1='00-00-00-00-00-00-00-00' \
  -e CONTROLLER_KEY2='00-00-00-00-00-00-00-00' \
  --net=host excaliburpartners/omnilink-bridge
```

Or start in the background with time sync and MQTT modules enabled.
```
docker run -d --name="omnilink-bridge" --restart always \
  -v /etc/localtime:/etc/localtime:ro \
  -e CONTROLLER_ADDRESS='' \
  -e CONTROLLER_KEY1='00-00-00-00-00-00-00-00' \
  -e CONTROLLER_KEY2='00-00-00-00-00-00-00-00' \
  -e TIME_SYNC='yes' \
  -e MQTT_ENABLED='yes' \
  -e MQTT_SERVER='' \
  -e MQTT_USERNAME='' \
  -e MQTT_PASSWORD='' \
  --net=host excaliburpartners/omnilink-bridge
```

## Docker Hub with Configuration File
1. Configure at a minimum the controller IP and encryptions keys.
```
mkdir /opt/omnilink-bridge
curl https://raw.githubusercontent.com/excaliburpartners/OmniLinkBridge/master/OmniLinkBridge/OmniLinkBridge.ini -o /opt/omnilink-bridge/OmniLinkBridge.ini 
vim /opt/omnilink-bridge/OmniLinkBridge.ini
```
2. Start docker container
```
docker run -d --name="omnilink-bridge" -v /opt/omnilink-bridge:/config -v /etc/localtime:/etc/localtime:ro --net=host --restart always excaliburpartners/omnilink-bridge
```
3. Verify connectivity by looking at logs
```
docker logs omnilink-bridge
```

## Docker for Developers
1. Clone git repo and build docker image
```
git clone https://github.com/excaliburpartners/OmniLinkBridge.git
cd OmniLinkBridge
docker build --tag="omnilink-bridge" .
```
2. Configure at a minimum the controller IP and encryptions keys. The web service port must be 8000 unless the Dockerfile is changed.
```
mkdir /opt/omnilink-bridge
cp OmniLinkBridge/OmniLinkBridge.ini /opt/omnilink-bridge
vim /opt/omnilink-bridge/OmniLinkBridge.ini
```
3. Start docker container
```
docker run -d --name="omnilink-bridge" -v /opt/omnilink-bridge:/config -v /etc/localtime:/etc/localtime:ro --net=host --restart always omnilink-bridge
```
4. Verify connectivity by looking at logs
```
docker logs omnilink-bridge
```

## Installation Windows
1. Copy files to your desired location like C:\OmniLinkBridge
2. Edit OmniLinkBridge.ini and define at a minimum the controller IP and encryptions keys
3. Run OmniLinkBridge.exe from the command prompt to verify connectivity
4. Add Windows service
```
sc create OmniLinkBridge binpath=C:\OmniLinkBridge\OmniLinkBridge.exe
```
5. Start service
```
net start OmniLinkBridge
```

## Installation Linux
1. Copy files to your desired location like /opt/OmniLinkBridge
2. Configure at a minimum the controller IP and encryptions keys
```
vim OmniLinkBridge.ini
```
3. Run as interactive to verify connectivity
```
mono OmniLinkBridge.exe -i
```
4. Add systemd file and configure paths
```
cp omnilinkbridge.service /etc/systemd/system/
vim /etc/systemd/system/omnilinkbridge.service
systemctl daemon-reload
```
5. Enable at boot and start service
```
systemctl enable omnilinkbridge.service
systemctl start omnilinkbridge.service
```

## MQTT
```
SUB omnilink/status
string online, offline

SUB omnilink/model
string Controller model

SUB omnilink/version
string Controller version
```

### System
```
SUB omnilink/system/phone/state
string secure, trouble

SUB omnilink/system/ac/state
string secure, trouble

SUB omnilink/system/battery/state
string secure, trouble

SUB omnilink/system/dcm/state
string secure, trouble
```

### Areas
```
SUB omnilink/areaX/name
string Area name

SUB omnilink/areaX/state
string triggered, arming, armed_night, armed_night_delay, armed_home, armed_home_instant, armed_away, armed_vacation, disarmed

SUB omnilink/areaX/basic_state
string triggered, arming, armed_night, armed_home, armed_away, armed_vacation, disarmed

SUB omnilink/areaX/json_state
string json

PUB omnilink/areaX/command
string arm_home, arm_away, arm_night, disarm, arm_home_instant, arm_night_delay, arm_vacation
note Use area 0 for all areas
note Optionally the user code number can be specified 'disarm,1'
note Optionally the security code can be be specified 'disarm,validate,1234'

PUB omnilink/areaX/alarm_command
string burglary, fire, auxiliary
```

### Zones
```
SUB omnilink/zoneX/name
string Zone name

SUB omnilink/zoneX/state  
string secure, not_ready, trouble, armed, tripped, bypassed

SUB omnilink/zoneX/basic_state  
string OFF, ON

SUB omnilink/zoneX/current_temperature (optional)  
int Current temperature in degrees fahrenheit  

SUB omnilink/zoneX/current_humidity (optional)  
int Current relative humidity

PUB omnilink/zoneX/command  
string bypass, restore
note Use zone 0 to restore all zones
note Optionally the user code number can be specified 'bypass,1'
```

### Units
```
SUB omnilink/unitX/name
string Unit name

SUB omnilink/unitX/state  
PUB omnilink/unitX/command  
string OFF, ON

SUB omnilink/unitX/brightness_state  
PUB omnilink/unitX/brightness_command  
int Level from 0 to 100 percent

SUB omnilink/unitX/flag_state  
PUB omnilink/unitX/flag_command  
int Level from 0 to 255

SUB omnilink/unitX/scene_state  
PUB omnilink/unitX/scene_command  
string A-L
```

### Thermostats
```
SUB omnilink/thermostatX/name
string Thermostat name

SUB omnilink/thermostatX/status
string online, offline

SUB omnilink/thermostatX/current_operation  
string idle, cooling, heating

SUB omnilink/thermostatX/current_temperature  
int Current temperature in degrees fahrenheit  

SUB omnilink/thermostatX/current_humidity  
int Current relative humidity

SUB omnilink/thermostatX/temperature_heat_state  
SUB omnilink/thermostatX/temperature_cool_state  
PUB omnilink/thermostatX/temperature_heat_command  
PUB omnilink/thermostatX/temperature_cool_command  
int Setpoint in degrees fahrenheit

SUB omnilink/thermostatX/humidify_state  
SUB omnilink/thermostatX/dehumidify_state  
PUB omnilink/thermostatX/humidify_command  
PUB omnilink/thermostatX/dehumidify_command  
int Setpoint in relative humidity

SUB omnilink/thermostatX/mode_state  
PUB omnilink/thermostatX/mode_command  
string auto, off, cool, heat, e_heat

SUB omnilink/thermostatX/mode_basic_state  
string auto, off, cool, heat

SUB omnilink/thermostatX/fan_mode_state  
PUB omnilink/thermostatX/fan_mode_command  
string auto, on, cycle

SUB omnilink/thermostatX/hold_state   
string off, on, vacation

PUB omnilink/thermostatX/hold_command  
string off, on
```

### Buttons
```
SUB omnilink/buttonX/name
string Button name

SUB omnilink/buttonX/state  
string OFF, ON

PUB omnilink/buttonX/command  
string ON
```

### Messages
```
SUB omnilink/messageX/name
string Message name

SUB omnilink/messageX/state
string off, displayed, displayed_not_acknowledged

PUB omnilink/messageX/command
string show, show_no_beep, show_no_beep_or_led, clear
```

### Locks
```
SUB omnilink/lockX/name
string Lock name

SUB omnilink/lockX/state
string locked, unlocked

PUB omnilink/lockX/command
string lock, unlock
```

### Audio Sources
```
SUB omnilink/sourceXX/name
string Audio source name
```

### Audio Zones
```
SUB omnilink/audioXX/name
string Audio zone name

SUB omnilink/audioXX/state
PUB omnilink/audioXX/command
string OFF, ON
note Use audio 0 to change all audio zones

SUB omnilink/audioXX/mute_state
PUB omnilink/audioXX/mute_command
string OFF, ON
note Use audio 0 to change all audio zones

SUB omnilink/audioXX/source_state
PUB omnilink/audioXX/source_command
string Source name
note Refer to omnilink/sourceXX/name

SUB omnilink/audioXX/volume_state
PUB omnilink/audioXX/volume_command
int Level from 0 to 100 percent
double Level from 0.00 to 1.00 (mqtt_audio_volume_media_player = yes)
```

## Web API
To test the web service API you can use your browser to view a page or PowerShell (see below) to change a value.

```
Invoke-WebRequest -Uri "http://localhost:8000/SetUnit" -Method POST -ContentType "application/json" -Body (convertto-json -InputObject @{"id"=1;"value"=100}) -UseBasicParsing
```

### Subscription
```
POST /Subscribe
{ "callback": url }
Callback is a POST request with Type header added and json body identical to the related /Get
Type: area, contact, motion, water, smoke, co, temp, unit, thermostat
```

### Areas
```
GET /ListAreas
GET /GetArea?id=X
```

### Zones
```
GET /ListZonesContact
GET /ListZonesMotion
GET /ListZonesWater
GET /ListZonesSmoke
GET /ListZonesCO
GET /ListZonesTemp
GET /GetZone?id=X
```

### Units
```
GET /ListUnits
GET /GetZone?id=X
POST /SetUnit
POST /SetUnitKeypadPress
{ "id":X, "value":0-100 }
```

### Thermostats
```
GET /ListThermostats
GET /GetThermostat?id=X
POST /SetThermostatCoolSetpoint
POST /SetThermostatHeatSetpoint
POST /SetThermostatMode
POST /SetThermostatFanMode
POST /SetThermostatHold
{ "id":X, "value": }
int mode 0=off, 1=heat, 2=cool, 3=auto, 4=emergency heat
int fanmode 0=auto, 1=on, 2=circulate
int hold 0=off, 1=on
```

### Thermostats
```
GET /ListButtons
POST /PushButton
{ "id":X, "value":1 }
```

## MySQL
The [MySQL ODBC Connector](http://dev.mysql.com/downloads/connector/odbc/) is required for MySQL logging. The docker image comes with the MySQL ODBC connector installed. For Windows and Linux you will need to download and install it. The Home Assistant Add-on does not support MySQL logging.

Configure mysql_connection in OmniLinkBridge.ini. For Windows change DRIVER={MySQL} to name of the driver shown in the ODBC Data Source Administrator.
```
mysql_connection = DRIVER={MySQL};SERVER=localhost;DATABASE=OmniLinkBridge;USER=root;PASSWORD=myPassword;OPTION=3;
```

## Telemetry
OmniLink Bridge collects anonymous telemetry data to help improve the software. You can opt of telemetry by setting a TELEMETRY_OPTOUT environment variable to 1.