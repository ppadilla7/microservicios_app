## Multi-stage Dockerfile for Security.Api (NET 8)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for efficient restore
COPY ["services/Security/Security.Api/Security.Api.csproj", "services/Security/Security.Api/"]
COPY ["services/Security/Security.Application/Security.Application.csproj", "services/Security/Security.Application/"]
COPY ["services/Security/Security.Infrastructure/Security.Infrastructure.csproj", "services/Security/Security.Infrastructure/"]
COPY ["services/Security/Security.Domain/Security.Domain.csproj", "services/Security/Security.Domain/"]
COPY ["services/BuildingBlocks/BuildingBlocks.csproj", "services/BuildingBlocks/"]

RUN dotnet restore "services/Security/Security.Api/Security.Api.csproj"

# Copy the rest of the source code
COPY . .
WORKDIR "/src/services/Security/Security.Api"

# Publish
RUN dotnet publish "Security.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:5096
EXPOSE 5096
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Security.Api.dll"]