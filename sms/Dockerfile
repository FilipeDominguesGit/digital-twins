# SMS twin. Build from the repo root: docker build -f sms/Dockerfile -t sms-twin .
FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src
# The shared build rules (D-028) and the SDK pin apply here too.
COPY global.json Directory.Build.props .editorconfig stylecop.json ./
COPY sms/app/SmsTwin.csproj sms/app/
RUN dotnet restore sms/app/SmsTwin.csproj
COPY sms/app/ sms/app/
RUN dotnet publish sms/app/SmsTwin.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
LABEL org.opencontainers.image.title="sms-twin" \
      org.opencontainers.image.description="SMS provider twin for tests and previews: send API, inspection API, fault injection, incoming SMS to a signed webhook. State in memory." \
      org.opencontainers.image.licenses="MIT"
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "SmsTwin.dll"]
