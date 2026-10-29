# syntax=docker/dockerfile:1
FROM node:24-bookworm-slim AS web-build
WORKDIR /source/apps/web
COPY apps/web/package.json apps/web/package-lock.json ./
RUN npm ci
COPY apps/web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /source
COPY apps/Garmin.Api/Garmin.Api.csproj apps/Garmin.Api/
COPY src/Garmin.Data/Garmin.Data.csproj src/Garmin.Data/
RUN dotnet restore apps/Garmin.Api/Garmin.Api.csproj
COPY src/Garmin.Data/ src/Garmin.Data/
COPY apps/Garmin.Api/ apps/Garmin.Api/
RUN dotnet publish apps/Garmin.Api/Garmin.Api.csproj --configuration Release \
    --no-restore --output /published /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    GARMIN_DB_PATH=/data/activities.db
RUN mkdir /data && chown "$APP_UID" /data
COPY --from=api-build /published/ ./
COPY --from=web-build /source/apps/web/dist/garmin-web/browser/ ./wwwroot/
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Garmin.Api.dll"]
