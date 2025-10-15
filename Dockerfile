FROM debian:bookworm AS build

RUN apt update && apt install -y dirmngr ca-certificates gnupg \
  && gpg --homedir /tmp --no-default-keyring --keyring gnupg-ring:/usr/share/keyrings/mono-official-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF \
  && chmod +r /usr/share/keyrings/mono-official-archive-keyring.gpg \
  && echo "deb [signed-by=/usr/share/keyrings/mono-official-archive-keyring.gpg] https://download.mono-project.com/repo/debian stable-buster main" | tee /etc/apt/sources.list.d/mono-official-stable.list \
  && apt update && apt install -y mono-complete unixodbc \
  && rm -rf /var/lib/apt/lists/* /tmp/*

ARG TARGETPLATFORM
ENV TARGETPLATFORM=${TARGETPLATFORM:-linux/amd64}

WORKDIR /build
RUN if [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
  curl -o mysql-connector-odbc-8.0.42-src.tar.gz https://downloads.mysql.com/archives/get/p/10/file/mysql-connector-odbc-8.0.42-src.tar.gz && \
  tar zxf mysql-connector-odbc-8.0.42-linux-debian9-x86-64bit.tar.gz && \
  mkdir -p /usr/lib/odbc/ && \
  cp mysql-connector-odbc-8.0.42-linux-debian9-x86-64bit/lib/* /usr/lib/odbc/ && \
  mysql-connector-odbc-8.0.42-linux-debian9-x86-64bit/bin/myodbc-installer -d -a -n "MySQL" -t "DRIVER=/usr/lib/odbc/libmyodbc8w.so"; \
  else \
  mkdir -p /usr/lib/odbc/ && \
  touch /etc/odbcinst.ini; \
  fi

COPY . .
RUN msbuild /build/OmniLinkBridge.sln /t:Restore
RUN msbuild /build/OmniLinkBridge.sln /t:Build /p:Configuration=Release
RUN mv /build/OmniLinkBridge/bin/Release /app

FROM debian:bookworm AS runtime

RUN apt update && apt install -y dirmngr ca-certificates gnupg \
  && gpg --homedir /tmp --no-default-keyring --keyring gnupg-ring:/usr/share/keyrings/mono-official-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF \
  && chmod +r /usr/share/keyrings/mono-official-archive-keyring.gpg \
  && echo "deb [signed-by=/usr/share/keyrings/mono-official-archive-keyring.gpg] https://download.mono-project.com/repo/debian stable-buster main" | tee /etc/apt/sources.list.d/mono-official-stable.list \
  && apt update && apt install -y mono-complete unixodbc \
  && rm -rf /var/lib/apt/lists/* /tmp/*

COPY --from=build /usr/lib/odbc /usr/lib/odbc
COPY --from=build /etc/odbcinst.ini /etc/odbcinst.ini
COPY --from=build /app/OmniLinkBridge.ini /config/OmniLinkBridge.ini

EXPOSE 8000/tcp
VOLUME /config
WORKDIR /app
COPY --from=build /app .
CMD [ "mono",  "OmniLinkBridge.exe", "-i", "-c", "/config/OmniLinkBridge.ini", "-e", "-s", "/config/WebSubscriptions.json", "-lf", "disable" ]