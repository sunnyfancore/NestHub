FROM --platform=$BUILDPLATFORM node:24-bookworm-slim AS frontend
WORKDIR /source/src/NestHub.Web
COPY src/NestHub.Web/package*.json ./
RUN npm ci
COPY src/NestHub.Web/ ./
RUN npm run build

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/NestHub.Api/NestHub.Api.csproj src/NestHub.Api/
RUN dotnet restore src/NestHub.Api/NestHub.Api.csproj
COPY src/NestHub.Api/ src/NestHub.Api/
COPY extensions/nesthub/ extensions/nesthub/
COPY --from=frontend /source/src/NestHub.Web/dist/ src/NestHub.Api/wwwroot/
RUN dotnet publish src/NestHub.Api/NestHub.Api.csproj -c Release -o /out --no-restore -p:SkipFrontendBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl tzdata \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    TZ=Asia/Shanghai
COPY --from=build /out/ ./
RUN mkdir -p /app/AppData && chown -R app:app /app/AppData
USER app
ARG APP_REVISION=local
ENV NESTHUB_BUILD_REVISION=$APP_REVISION
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=90s --retries=5 \
    CMD curl --fail --silent http://127.0.0.1:8080/health || exit 1
ENTRYPOINT ["dotnet", "NestHub.Api.dll"]
