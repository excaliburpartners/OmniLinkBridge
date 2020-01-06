FROM mono:latest AS build

RUN apt-get update && \
  apt-get install -y unixodbc

WORKDIR /build
ADD https://dev.mysql.com/get/Downloads/Connector-ODBC/8.0/mysql-connector-odbc-8.0.18-linux-debian9-x86-64bit.tar.gz /build
RUN tar zxf mysql-connector-odbc-8.0.18-linux-debian9-x86-64bit.tar.gz && \
  mkdir -p /usr/lib/odbc/ && \
  cp mysql-connector-odbc-8.0.18-linux-debian9-x86-64bit/lib/* /usr/lib/odbc/ && \
  mysql-connector-odbc-8.0.18-linux-debian9-x86-64bit/bin/myodbc-installer -d -a -n "MySQL" -t "DRIVER=/usr/lib/odbc/libmyodbc8w.so"

COPY . .
RUN nuget restore /build/OmniLinkBridge.sln
RUN msbuild /build/OmniLinkBridge.sln /t:Build /p:Configuration=Release
RUN mv /build/OmniLinkBridge/bin/Release /app

FROM mono:latest AS runtime

RUN apt-get update && \
  apt-get install -y unixodbc

COPY --from=build /usr/lib/odbc /usr/lib/odbc
COPY --from=build /etc/odbcinst.ini /etc/odbcinst.ini
COPY --from=build /app/OmniLinkBridge.ini /config/OmniLinkBridge.ini

EXPOSE 8000/tcp
VOLUME /config
WORKDIR /app
COPY --from=build /app .
CMD [ "mono",  "OmniLinkBridge.exe", "-i", "-c", "/config/OmniLinkBridge.ini", "-e", "-s", "/config/WebSubscriptions.json", "-lf", "disable" ]