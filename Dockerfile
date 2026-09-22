# Stage 1: Base runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

# Stage 2: SDK image for compiling and restoring dependencies
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files first for optimized Docker layer caching
COPY ["src/TodoApp.Domain/TodoApp.Domain.csproj", "src/TodoApp.Domain/"]
COPY ["src/TodoApp.Application/TodoApp.Application.csproj", "src/TodoApp.Application/"]
COPY ["src/TodoApp.Infrastructure/TodoApp.Infrastructure.csproj", "src/TodoApp.Infrastructure/"]
COPY ["src/TodoApp.Api/TodoApp.Api.csproj", "src/TodoApp.Api/"]

RUN dotnet restore "src/TodoApp.Api/TodoApp.Api.csproj"

# Copy source code and build
COPY src/ src/
RUN cp -n src/TodoApp.Api/appsettings.Example.json src/TodoApp.Api/appsettings.json 2>/dev/null || true
WORKDIR "/src/src/TodoApp.Api"
RUN dotnet build "TodoApp.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Stage 3: Publish optimized production binaries
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "TodoApp.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Stage 4: Final lightweight runtime container
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TodoApp.Api.dll"]

