FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Ingat.AI/Ingat.AI.csproj", "Ingat.AI/"]
RUN dotnet restore "./Ingat.AI/Ingat.AI.csproj"

COPY . .
WORKDIR /src/Ingat.AI
RUN dotnet publish -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5200

ENTRYPOINT ["dotnet", "Ingat.AI.dll"]
