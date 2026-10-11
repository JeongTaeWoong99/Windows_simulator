# 게임 서버 이미지. 빌드 컨텍스트는 Server/ — docker-compose.yml이 정한다. 띄우는 법: Server/docs/도커.md
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
# CI 빌드로 표시해 Unity 미러링(PowerShell)·분석기 복사를 건너뛴다 — 컨테이너에는 Unity 프로젝트가 없다.
RUN dotnet publish WSGameServer/WSGameServer.csproj -c Release -o /app -p:ContinuousIntegrationBuild=true

FROM mcr.microsoft.com/dotnet/runtime:10.0
# 운영 중 DB를 들여다보거나 admin_level을 올릴 때 쓴다 — docker compose exec game sqlite3 /data/game.sqlite3
RUN apt-get update && apt-get install -y --no-install-recommends sqlite3 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "WSGameServer.dll"]
