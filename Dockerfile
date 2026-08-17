FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY deploy/hrms-backend-render-fixed.zip.b64.txt /tmp/hrms.b64
RUN base64 -d /tmp/hrms.b64 > /tmp/hrms.zip && \
    mkdir /real && cd /real && \
    python3 -c "import zipfile; zipfile.ZipFile('/tmp/hrms.zip').extractall('.')"
WORKDIR /real
RUN dotnet restore HRMS.sln
RUN dotnet publish HRMS.API/HRMS.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:10000
ENV DOTNET_USE_POLLING_FILE_WATCHER=1
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 10000
ENTRYPOINT ["dotnet", "HRMS.API.dll"]
