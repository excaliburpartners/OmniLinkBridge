# HAILogger
Provides logging and web service API for HAI/Leviton OmniPro II controllers

##Download
You can download the [binary here](http://www.excalibur-partners.com/downloads/HAILogger_1_0_6.zip)

##Requirements
- .NET Framework 4.0
- mySQL 5.1 ODBC Connector

##Operation
- Area, Messages, Units, and Zones are logged to mySQL when status changes
- Thermostats are logged to mySQL once per minute
	- If no notifications are received within 4 minutes a request is issued
	- After 5 minutes of no updates a warning will be logged and mySQL will not be updated
	- If the temp is 0 a warning will be logged and mySQL will not be updated
- Controller time is checked and compared to the local computer time disregarding time zones

##Notifications
- Emails are sent to mail_alarm_to when an area status changes
- Prowl notifications are sent when an areas status changes

##Installation
1. Copy files to your desiered location like C:\HAILogger
2. Create mySQL database and import HAILogger.sql
3. Update HAILogger.ini with settings
4. Run HAILogger.exe and verify everything is working
5. For Windows Service run install.bat / uninstall.bat

##MySQL Setup
You will want to install the MySQL Community Server, Workbench, and ODBC Connector. The Workbench software provides a graphical interface to administer the MySQL server. The HAI Logger uses ODBC to communicate with the database. The MySQL ODBC Connector library is needed for Windows ODBC to communicate with MySQL. Make sure you install version 5.1 of the MySQL ODBC Connector provided in the link below.

http://dev.mysql.com/downloads/mysql/
http://dev.mysql.com/downloads/tools/workbench/
http://dev.mysql.com/downloads/connector/odbc/5.1.html

After installing MySQL server it should have asked you to setup an instance. One of the steps of the instance wizard was to create a root password. Assuming you installed the HAI Logger on the same computer you will want to use the below settings in HAILogger.ini.

mysql_server = localhost
mysql_user = root
mysql_password = password you set in the wizard

At this point we need to open MySQL Workbench to create the database (called a schema in the Workbench GUI) for HAILogger to use.

1. After opening the program double-click on "Local instance MySQL" and enter the password you set in the wizard.
2. On the toolbar click the "Create a new schema" button, provide a name, and click apply.
3. On the left side right-click on the schema you created and click "Set as default schema".
4. In the middle section under Query1 click the open file icon and select the HAILogger.sql file.
5. Click the Execute lighting bolt to run the query, which will create the tables.

Lastly in HAILogger.ini set mysql_database to the name of the schema you created. This should get you up and running. The MySQL Workbench can also be used to view the data that HAILogger inserts into the tables.

##Web Service API
To test the API you can use your browser to view a page or PowerShell (see below) to change a value.

- http://localhost:8000/ListUnits
- http://localhost:8000/GetUnit?id=1
- Invoke-WebRequest  -Uri "http://localhost:8000/SetUnit" -Method POST -ContentType "application/json" -Body (convertto-json -InputObject @{"id"=1;"value"=100}) -UseBasicParsing

##Change Log
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