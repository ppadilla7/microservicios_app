## Multi-stage Dockerfile for Courses.Api (NET 8)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for efficient restore
COPY ["services/Courses/Courses.Api/Courses.Api.csproj", "services/Courses/Courses.Api/"]
COPY ["services/Courses/Courses.Application/Courses.Application.csproj", "services/Courses/Courses.Application/"]
COPY ["services/Courses/Courses.Infrastructure/Courses.Infrastructure.csproj", "services/Courses/Courses.Infrastructure/"]
COPY ["services/Courses/Courses.Domain/Courses.Domain.csproj", "services/Courses/Courses.Domain/"]
COPY ["services/BuildingBlocks/BuildingBlocks.csproj", "services/BuildingBlocks/"]
# Ensure custom NuGet sources/config are available during restore
COPY ["NuGet.Config", "."]

RUN dotnet restore "services/Courses/Courses.Api/Courses.Api.csproj" --disable-parallel --verbosity detailed --source https://api.nuget.org/v3/index.json

# Copy the rest of the source code
COPY . .
WORKDIR "/src/services/Courses/Courses.Api"

# Publish
RUN dotnet publish "Courses.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:5202
EXPOSE 5202
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Courses.Api.dll"]