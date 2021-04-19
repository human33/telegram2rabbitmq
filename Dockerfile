# Dockerfile 
FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine AS build-env
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out


# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:5.0
WORKDIR /app

COPY --from=build-env /app/out .

EXPOSE 9090/tcp
EXPOSE 32999/tcp

ENTRYPOINT [ "dotnet", "telegramBridge.dll" ]