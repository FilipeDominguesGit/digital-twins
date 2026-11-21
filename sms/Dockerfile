# SMS twin. Build from the repo root: docker build -t sms-twin sms
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY app.cs .
RUN dotnet publish app.cs -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
LABEL org.opencontainers.image.title="sms-twin" \
      org.opencontainers.image.description="SMS provider twin for tests and previews: send API, inspection API, fault injection. State in memory." \
      org.opencontainers.image.licenses="MIT"
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "app.dll"]
