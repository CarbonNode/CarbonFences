FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY CarbonZones/*.csproj CarbonZones/
RUN dotnet restore CarbonZones/CarbonZones.csproj -r win-x64

COPY CarbonZones/ CarbonZones/
RUN dotnet publish CarbonZones/CarbonZones.csproj -c Release -r win-x64 --self-contained -o /publish/CarbonZones

FROM scratch AS export
COPY --from=build /publish/CarbonZones /publish/CarbonZones
