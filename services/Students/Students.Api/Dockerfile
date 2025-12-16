## Multi-stage Dockerfile for Students.Api (NET 8)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for efficient restore
COPY ["services/Students/Students.Api/Students.Api.csproj", "services/Students/Students.Api/"]
COPY ["services/Students/Students.Application/Students.Application.csproj", "services/Students/Students.Application/"]
COPY ["services/Students/Students.Infrastructure/Students.Infrastructure.csproj", "services/Students/Students.Infrastructure/"]
COPY ["services/Students/Students.Domain/Students.Domain.csproj", "services/Students/Students.Domain/"]
COPY ["services/BuildingBlocks/BuildingBlocks.csproj", "services/BuildingBlocks/"]

RUN dotnet restore "services/Students/Students.Api/Students.Api.csproj"

# Copy the rest of the source code
COPY . .
WORKDIR "/src/services/Students/Students.Api"

# Publish
RUN dotnet publish "Students.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:5084
EXPOSE 5084
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Students.Api.dll"]