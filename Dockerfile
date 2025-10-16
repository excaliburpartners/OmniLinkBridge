FROM debian:bookworm AS build

RUN apt update && apt install -y dirmngr ca-certificates gnupg \
  && gpg --homedir /tmp --no-default-keyring --keyring gnupg-ring:/usr/share/keyrings/mono-official-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF \
  && chmod +r /usr/share/keyrings/mono-official-archive-keyring.gpg \
  && echo "deb [signed-by=/usr/share/keyrings/mono-official-archive-keyring.gpg] https://download.mono-project.com/repo/debian stable-buster main" | tee /etc/apt/sources.list.d/mono-official-stable.list \
  && apt update && apt install -y mono-complete nuget unixodbc \
  && rm -rf /var/lib/apt/lists/* /tmp/*

WORKDIR /build
COPY . .

RUN nuget restore /build/OmniLinkBridge.sln
RUN msbuild /build/OmniLinkBridge.sln /t:Build /p:Configuration=Release
RUN mv /build/OmniLinkBridge/bin/Release /app

FROM debian:bookworm AS runtime

RUN apt update && apt install -y dirmngr ca-certificates gnupg \
  && gpg --homedir /tmp --no-default-keyring --keyring gnupg-ring:/usr/share/keyrings/mono-official-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF \
  && chmod +r /usr/share/keyrings/mono-official-archive-keyring.gpg \
  && echo "deb [signed-by=/usr/share/keyrings/mono-official-archive-keyring.gpg] https://download.mono-project.com/repo/debian stable-buster main" | tee /etc/apt/sources.list.d/mono-official-stable.list \
  && apt update && apt install -y mono-complete nuget unixodbc \
  && rm -rf /var/lib/apt/lists/* /tmp/*

ARG TARGETPLATFORM
ENV TARGETPLATFORM=${TARGETPLATFORM:-linux/amd64}

RUN if [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
  apt update && apt install -y curl; \
  curl -L -o mysql-connector-odbc.deb https://dev.mysql.com/get/Downloads/Connector-ODBC/9.4/mysql-connector-odbc_9.4.0-1debian12_amd64.deb && \
  apt install -y ./mysql-connector-odbc.deb; \
  rm -rf /var/lib/apt/lists/* /tmp/*; \
  fi

COPY --from=build /app/OmniLinkBridge.ini /config/OmniLinkBridge.ini

EXPOSE 8000/tcp
VOLUME /config
WORKDIR /app
COPY --from=build /app .
CMD [ "mono",  "OmniLinkBridge.exe", "-i", "-c", "/config/OmniLinkBridge.ini", "-e", "-s", "/config/WebSubscriptions.json", "-lf", "disable" ]