# 경매장 서버 이미지. 빌드 컨텍스트는 Server/ — docker-compose.yml이 정한다. 띄우는 법: Server/docs/도커.md
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish AuctionServer/AuctionServer.csproj -c Release -o /app -p:ContinuousIntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "AuctionServer.dll"]
