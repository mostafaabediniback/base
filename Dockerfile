FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
RUN apt-get update && apt-get install -y --no-install-recommends unzip && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY deploy/HRMS.Application\(6\).zip /tmp/hrms.zip
RUN unzip -q /tmp/hrms.zip -d /src
RUN dotnet restore HRMS.API/HRMS.API.csproj
RUN dotnet publish HRMS.API/HRMS.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_USE_POLLING_FILE_WATCHER=1
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
EXPOSE 10000
ENTRYPOINT ["dotnet", "HRMS.API.dll"]
