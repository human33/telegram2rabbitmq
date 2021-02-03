# Dockerfile 
FROM mcr.microsoft.com/dotnet/core/sdk:5.0.102-ca-patch-buster-slim AS build-env
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out


# Build runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:5.0
WORKDIR /app

COPY --from=build-env /app/out .

EXPOSE 9090/tcp

ENTRYPOINT [ "dotnet", "telegramBridge.dll" ]