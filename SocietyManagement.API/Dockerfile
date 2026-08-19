# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["SocietyManagement.API/SocietyManagement.API.csproj", "SocietyManagement.API/"]
COPY ["SocietyManagement.Application/SocietyManagement.Application.csproj", "SocietyManagement.Application/"]
COPY ["SocietyManagement.Infrastructure/SocietyManagement.Infrastructure.csproj", "SocietyManagement.Infrastructure/"]
COPY ["SocietyManagement.Domain/SocietyManagement.Domain.csproj", "SocietyManagement.Domain/"]
COPY ["SocietyManagement.Shared/SocietyManagement.Shared.csproj", "SocietyManagement.Shared/"]
RUN dotnet restore "SocietyManagement.API/SocietyManagement.API.csproj"

COPY . .
WORKDIR "/src/SocietyManagement.API"
RUN dotnet build "SocietyManagement.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "SocietyManagement.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "SocietyManagement.API.dll"]
