FROM mono:latest

COPY . /build

RUN nuget restore /build/OmniLinkBridge.sln
RUN msbuild /build/OmniLinkBridge.sln /t:Build /p:Configuration=Release

RUN mv /build/OmniLinkBridge/bin/Release /app
RUN rm -rf /build

EXPOSE 8000/tcp

VOLUME /config

WORKDIR /app

CMD [ "mono",  "OmniLinkBridge.exe", "-i", "-c", "/config/OmniLinkBridge.ini", "-s", "/config/WebSubscriptions.json" ]