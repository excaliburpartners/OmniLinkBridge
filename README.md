# OmniLink Bridge
Provides time sync, logging, web service API, and MQTT bridge for HAI/Leviton OmniPro II controllers. Provides integration with Samsung SmarthThings via Web Service API and Home Assistant via MQTT.

## Download
You can download the [binary here](http://www.excalibur-partners.com/downloads/OmniLinkBridge_1_1_2.zip) or use docker to build an image from git.

## Requirements
- .NET Framework 4.5.2 (or Mono equivalent)

## Operation
- Area, Messages, Units, and Zones are logged to mySQL when status changes
- Thermostats are logged to mySQL once per minute
	- If no notifications are received within 4 minutes a request is issued
	- After 5 minutes of no updates a warning will be logged and mySQL will not be updated
	- If the temp is 0 a warning will be logged and mySQL will not be updated
- Controller time is checked and compared to the local computer time disregarding time zones

## Notifications
- Supports email, prowl, and pushover
- Always sent for area alarms and critical system events
- Optionally enable for area status changes and console messages

## Installation Windows
1. Copy files to your desired location like C:\OmniLinkBridge
2. Edit OmniLinkBridge.ini and define at a minimum the controller IP and encryptions keys
3. Run OmniLinkBridge.exe to verify connectivity
4. Add Windows service
	- sc create OmniLinkBridge binpath=C:\OmniLinkBridge\OmniLinkBridge.exe
5. Start service
	- net start OmniLinkBridge

## Installation Linux
1. Copy files to your desired location like /opt/OmniLinkBridge
2. Configure at a minimum the controller IP and encryptions keys
	- vim OmniLinkBridge.ini
3. Run as interactive to verify connectivity
	- mono OmniLinkBridge.exe -i
4. Add systemd file and configure paths
	- cp omnilinkbridge.service /etc/systemd/system/
	- vim /etc/systemd/system/omnilinkbridge.service
	- systemctl daemon-reload
5. Enable at boot and start service
	- systemctl enable omnilinkbridge.service
	- systemctl start omnilinkbridge.service

## Docker
1. Clone git repo and build docker image
	- git clone https://github.com/excaliburpartners/OmniLinkBridge.git
	- cd OmniLinkBridge
	- docker build --tag="omnilink-bridge" .
2. Configure at a minimum the controller IP and encryptions keys. The web service port must be 8000 unless the Dockerfile is changed.
	- mkdir /opt/omnilink-bridge
	- cp OmniLinkBridge/OmniLinkBridge.ini /opt/omnilink-bridge
	- vim /opt/omnilink-bridge/OmniLinkBridge.ini
3. Start docker container
	- docker run -d --name="omnilink-bridge" -v /opt/omnilink-bridge:/config -v /etc/localtime:/etc/localtime:ro --net=host --restart unless-stopped omnilink-bridge
4. Verify connectivity by looking at logs
	- docker container logs omnilink-bridge
	
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

mysql_connection = DRIVER={MySQL ODBC 8.0 Driver};SERVER=localhost;DATABASE=OmniLinkBridge;USER=root;PASSWORD=myPassword;OPTION=3;

## Web Service API
To test the API you can use your browser to view a page or PowerShell (see below) to change a value.

- http://localhost:8000/ListUnits
- http://localhost:8000/GetUnit?id=1
- Invoke-WebRequest -Uri "http://localhost:8000/SetUnit" -Method POST -ContentType "application/json" -Body (convertto-json -InputObject @{"id"=1;"value"=100}) -UseBasicParsing

## MQTT
This module will also publish discovery topics for Home Assistant to auto configure devices.

### Areas
```
SUB omnilink/areaX/state  
string triggered, pending, armed_night, armed_night_delay, armed_home, armed_home_instant, armed_away, armed_vacation, disarmed

SUB omnilink/areaX/basic_state  
string triggered, pending, armed_night, armed_home, armed_away, disarmed

PUB omnilink/areaX/command  
string(insensitive) arm_home, arm_away, arm_night, disarm, arm_home_instant, arm_night_delay, arm_vacation
```

### Zones
```
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
SUB omnilink/unitX/state  
PUB omnilink/unitX/command  
string OFF, ON

SUB omnilink/unitX/brightness_state  
PUB omnilink/unitX/brightness_command  
int Level from 0 to 100 percent
```

### Thermostats
```
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
SUB omnilink/buttonX/state  
string OFF

PUB omnilink/buttonX/command  
string ON
```

## Change Log
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
