# Builds the whole portal: React UI -> served by the ASP.NET Core API.
# Used by compose.yaml; see README.

# 1. React UI
FROM node:22-alpine AS frontend
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# 2. .NET solution (API + tests)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src/backend
COPY backend/StudentPortal.sln ./
COPY backend/StudentPortal.Api/StudentPortal.Api.csproj StudentPortal.Api/
COPY backend/StudentPortal.Tests/StudentPortal.Tests.csproj StudentPortal.Tests/
RUN dotnet restore StudentPortal.sln
COPY backend/ ./
COPY --from=frontend /src/backend/StudentPortal.Api/wwwroot StudentPortal.Api/wwwroot
RUN dotnet build StudentPortal.sln -c Release --no-restore

# 3. Security test runner (docker compose run --rm tests)
FROM build AS tests
ENTRYPOINT ["dotnet", "test", "StudentPortal.Tests", "-c", "Release", "--no-build", "--logger", "console;verbosity=detailed"]

# 4. Published API
FROM build AS publish
RUN dotnet publish StudentPortal.Api -c Release --no-build -o /app

# 5. Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS app
WORKDIR /app
COPY --from=publish /app ./
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "StudentPortal.Api.dll"]
