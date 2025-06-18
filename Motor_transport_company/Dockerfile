# Используем Windows-совместимый образ
FROM mcr.microsoft.com/dotnet/sdk:9.0-preview-nanoserver-ltsc2022 AS build
USER ContainerUser
WORKDIR /src
COPY . .
RUN dotnet restore -p:EnableWindowsTargeting=true
RUN dotnet publish -c Release -o /app -p:EnableWindowsTargeting=true -r win-x64 --self-contained true

# Финальный образ
FROM mcr.microsoft.com/dotnet/runtime:9.0-preview-nanoserver-ltsc2022
USER ContainerUser
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["Motor_transport_company.exe"]