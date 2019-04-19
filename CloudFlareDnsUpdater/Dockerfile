FROM microsoft/dotnet:2.2-runtime AS base
WORKDIR /app

FROM microsoft/dotnet:2.2-sdk AS build
WORKDIR /src
COPY CloudFlareDnsUpdater/CloudFlareDnsUpdater.csproj CloudFlareDnsUpdater/
RUN dotnet restore CloudFlareDnsUpdater/CloudFlareDnsUpdater.csproj
COPY . .
WORKDIR /src/CloudFlareDnsUpdater
RUN dotnet build CloudFlareDnsUpdater.csproj -c Release -o /app

FROM build AS publish
RUN dotnet publish CloudFlareDnsUpdater.csproj -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "CloudFlareDnsUpdater.dll"]
