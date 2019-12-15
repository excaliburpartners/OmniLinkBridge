# OmniLink Bridge
Provides MQTT bridge, web service API, time sync, and logging for [HAI/Leviton OmniPro II controllers](https://www.leviton.com/en/products/brands/omni-security-automation). Provides integration with [Samsung SmartThings via web service API](https://github.com/excaliburpartners/SmartThings-OmniPro) and [Home Assistant via MQTT](https://www.home-assistant.io/components/mqtt/).

## Download
You can use docker to build an image from git or download the [binary here](https://github.com/excaliburpartners/OmniLinkBridge/releases/latest/download/OmniLinkBridge.zip).

## Requirements
- [Docker](https://www.docker.com/)
- .NET Framework 4.5.2 (or Mono equivalent)

## Operation
OmniLinkBridge is divided into the following modules

- OmniLinkII
    - Settings: controller_
    - Maintains connection to the OmniLink controller
    - Thermostats
	    - If no status update has been received after 4 minutes a request is issued
        - A status update containing a temperature of 0 is ignored
            - This can occur when a ZigBee thermostat has lost communication
- Logger
    - Console output
        - Settings: verbose_
        - Thermostats (verbose_thermostat_timer)
            - After 5 minutes of no status updates a warning will be logged
	        - When a current temperature of 0 is received a warning will be logged
    - MySQL logging
        - Settings: mysql_
        - Thermostats are logged every minute and when an event is received
    - Push notifications
        - Settings: notify_
        - Always sent for area alarms and critical system events
        - Optionally enable for area status changes and console messages
        - Email
            - Settings: mail_
        - Prowl
            - Settings: prowl_
        - Pushover
            - Settings: pushover_
- Time Sync
    - Settings: time_
    - Controller time is checked and compared to the local computer time disregarding time zones
- Web API
    - Settings: webapi_
    - Provides integration with [Samsung SmartThings](https://github.com/excaliburpartners/SmartThings-OmniPro)
    - Allows an application to subscribe to receive POST notifications status updates are received from the OmniLinkII module
        - On failure to POST to callback URL subscription is removed
        - Recommended for application to send subscribe reqeusts every few minutes
    - Requests to GET endpoints return status from the OmniLinkII module
    - Requests to POST endpoints send commands to the OmniLinkII module
- MQTT
    - Settings: mqtt_
    - Maintains connection to the MQTT broker
    - Publishes discovery topics for [Home Assistant](https://www.home-assistant.io/components/mqtt/) to auto configure devices
    - Publishes topics for status received from the OmniLinkII module
    - Subscribes to command topics and sends commands to the OmniLinkII module

## Docker Hub (preferred)
1. Configure at a minimum the controller IP and encryptions keys. The web service port must be 8000.
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

## Docker (for developers)
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

### Areas
```
SUB omnilink/areaX/name
string Area name

SUB omnilink/areaX/state  
string triggered, pending, armed_night, armed_night_delay, armed_home, armed_home_instant, armed_away, armed_vacation, disarmed

SUB omnilink/areaX/basic_state  
string triggered, pending, armed_night, armed_home, armed_away, disarmed

PUB omnilink/areaX/command  
string(insensitive) arm_home, arm_away, arm_night, disarm, arm_home_instant, arm_night_delay, arm_vacation
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
string(insensitive) bypass, restore
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
```

### Thermostats
```
SUB omnilink/thermostatX/name
string Thermostat name

SUB omnilink/thermostatX/current_operation  
string idle, cool, heat

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
string auto, off, cool, heat

SUB omnilink/thermostatX/fan_mode_state  
PUB omnilink/thermostatX/fan_mode_command  
string auto, on, cycle

SUB omnilink/thermostatX/hold_state  
PUB omnilink/thermostatX/hold_command  
string off, hold
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

## MySQL Setup
You will want to install the MySQL Community Server, Workbench, and ODBC Connector. The Workbench software provides a graphical interface to administer the MySQL server. The OmniLink Bridge uses ODBC to communicate with the database. The MySQL ODBC Connector library is needed for Windows ODBC to communicate with MySQL. 

http://dev.mysql.com/downloads/mysql/
http://dev.mysql.com/downloads/tools/workbench/
http://dev.mysql.com/downloads/connector/odbc/

At this point we need to open MySQL Workbench to create the database (called a schema in the Workbench GUI) for OmniLinkBridge to use.

1. After opening the program double-click on "Local instance MySQL" and enter the password you set in the wizard.
2. On the toolbar click the "Create a new schema" button, provide a name, and click apply.
3. On the left side right-click on the schema you created and click "Set as default schema".
4. In the middle section under Query1 click the open file icon and select the OmniLinkBridge.sql file.
5. Click the Execute lighting bolt to run the query, which will create the tables.

Lastly in OmniLinkBridge.ini set mysql_connection. This should get you up and running. The MySQL Workbench can also be used to view the data that OmniLink Bridge inserts into the tables.

```
mysql_connection = DRIVER={MySQL};SERVER=localhost;DATABASE=OmniLinkBridge;USER=root;PASSWORD=myPassword;OPTION=3;
```

## Change Log
Version 1.1.5 - 2019-12-14
- Fix SQL logging for areas, units, and thermostats
- Refactor MQTT parser and add unit tests
- Update readme, fix thermostat logging interval, and cleanup code

Version 1.1.4 - 2019-11-22
- Utilize controller temperature format
- Don't publish invalid thermostat temperatures
- Always enable first area to support Omni LTe and Omni IIe
- Fix MQTT id validation and add notice for publishing to area 0
- Fix missing last area, zone, unit, thermostat, and button
- Fix compatibility with Home Assistant 0.95.4 MQTT extra keys
- Add Home Assistant MQTT device registry support
- Add MQTT messages for controller disconnect and last will
- Shutdown cleanly on linux or docker

Version 1.1.3 - 2019-02-10
- Publish config when reconnecting to MQTT
- Update readme documentation
- Add override zone type for web service
- Add area json status and climate temp sensor
- Fix compatibility with Home Assistant 0.87 MQTT strict config

Version 1.1.2 - 2018-10-23
- Add min and max climate temperatures
- Update docker run command to use local time zone
- Improve area and zone MQTT support
- Add option to change MQTT prefix to support multiple instances
- Add detailed zone sensor and thermostat humidity sensor
- Add prefix for MQTT discovery entity name
- Request zone status update on area status change

Version 1.1.1 - 2018-10-18
- Added docker support
- Save subscriptions on change

Version 1.1.0 - 2018-10-13
- Renamed to OmniLinkBridge
- Restructured code to be event based with modules
- Added MQTT module for Home Assistant
- Added pushover notifications
- Added web service API subscriptions file to persist subscriptions

Version 1.0.8 - 2016-11-28
- Fixed web service threading when multiple subscriptions exist
- Added additional zone types to contact and motion web service API
- Split command line options for config and log files

Version 1.0.7 - 2016-11-25
- Use previous area state when area is arming for web service API
- Add interactive command line option and use path separator for Mono compatibility

Version 1.0.6 - 2016-11-20
- Added thermostat status and auxiliary temp to web service API

Version 1.0.5 - 2016-11-15
- Added web service API for Samsung SmartThings integration

Version 1.0.4 - 2014-05-08
- Merged HAILogger.exe and HAILoggerService.exe
- Added immediate time sync after controller reconnect

Version 1.0.3 - 2013-01-06
- Added setting for prowl console message notification
- Added settings for verbose output control
- Added setting to enable mySQL logging
- Added queue to mySQL logging
- Changed mySQL log time from mySQL NOW() to computer time
- Changed prowl notifications to be asynchronous
- Fixed crash when prowl api down
- Fixed setting yes/no parsing so no setting works
- Fixed incorrect thermostat out of date status warning

Version 1.0.2 - 2012-12-30
- Fixed thermostat invalid mySQL logging error

Version 1.0.1 - 2012-12-30
- Added setting to adjust time sync interval
- Fixed crash when controller time not initially set

Version 1.0.0 - 2012-12-29
- Initial release
