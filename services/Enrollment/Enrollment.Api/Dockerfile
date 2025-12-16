## Multi-stage Dockerfile for Enrollment.Api (NET 8)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for efficient restore
COPY ["services/Enrollment/Enrollment.Api/Enrollment.Api.csproj", "services/Enrollment/Enrollment.Api/"]
COPY ["services/Enrollment/Enrollment.Application/Enrollment.Application.csproj", "services/Enrollment/Enrollment.Application/"]
COPY ["services/Enrollment/Enrollment.Infrastructure/Enrollment.Infrastructure.csproj", "services/Enrollment/Enrollment.Infrastructure/"]
COPY ["services/Enrollment/Enrollment.Domain/Enrollment.Domain.csproj", "services/Enrollment/Enrollment.Domain/"]
COPY ["services/BuildingBlocks/BuildingBlocks.csproj", "services/BuildingBlocks/"]

RUN dotnet restore "services/Enrollment/Enrollment.Api/Enrollment.Api.csproj"

# Copy the rest of the source code
COPY . .
WORKDIR "/src/services/Enrollment/Enrollment.Api"

# Publish
RUN dotnet publish "Enrollment.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:5281
EXPOSE 5281
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Enrollment.Api.dll"]