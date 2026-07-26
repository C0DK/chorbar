FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS builder
WORKDIR /src

COPY Chorbar/Chorbar.csproj Chorbar/
RUN dotnet restore Chorbar/Chorbar.csproj --runtime linux-musl-x64

COPY . .
RUN dotnet publish Chorbar/Chorbar.csproj \
      -c Release \
      -o /app \
      --no-restore \
      -p:PublishSingleFile=true \
      -p:SelfContained=false \
      -p:InvariantGlobalization=true \
      -r linux-musl-x64

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app

COPY --from=builder /app ./

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_CONTENTROOT=/app \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

EXPOSE 8080

USER app
ENTRYPOINT ["dotnet", "Chorbar.dll"]