FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5291

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY services/BuildingBlocks services/BuildingBlocks
COPY services/Notifications/Notifications.Api services/Notifications/Notifications.Api
RUN dotnet restore services/Notifications/Notifications.Api/Notifications.Api.csproj
RUN dotnet publish services/Notifications/Notifications.Api/Notifications.Api.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:5291
ENTRYPOINT ["dotnet", "Notifications.Api.dll"]