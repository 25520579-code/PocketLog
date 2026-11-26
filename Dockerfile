FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY PocketLog.Shared/PocketLog.Shared.csproj PocketLog.Shared/
COPY PocketLog.Server.Linux/PocketLog.Server.Linux.csproj PocketLog.Server.Linux/

RUN dotnet restore PocketLog.Server.Linux/PocketLog.Server.Linux.csproj

COPY PocketLog.Shared/ PocketLog.Shared/
COPY PocketLog.Server.Linux/ PocketLog.Server.Linux/

RUN dotnet publish PocketLog.Server.Linux/PocketLog.Server.Linux.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app

ENV TZ=Asia/Ho_Chi_Minh \
    POCKETLOG_PORT=8888 \
    POCKETLOG_UDP_PORT=8889 \
    POCKETLOG_DATA_DIR=/data

RUN mkdir -p /data/ServerStorage
VOLUME ["/data"]

EXPOSE 8888/tcp
EXPOSE 8889/udp

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PocketLog.Server.Linux.dll"]
