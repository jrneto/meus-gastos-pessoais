# Base image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Build image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj files and restore
COPY ["src/GastosApp.Api/GastosApp.Api.csproj", "src/GastosApp.Api/"]
COPY ["src/GastosApp.Application/GastosApp.Application.csproj", "src/GastosApp.Application/"]
COPY ["src/GastosApp.Domain/GastosApp.Domain.csproj", "src/GastosApp.Domain/"]
COPY ["src/GastosApp.Infrastructure/GastosApp.Infrastructure.csproj", "src/GastosApp.Infrastructure/"]
RUN dotnet restore "src/GastosApp.Api/GastosApp.Api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/GastosApp.Api"
RUN dotnet build "GastosApp.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "GastosApp.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GastosApp.Api.dll"]
