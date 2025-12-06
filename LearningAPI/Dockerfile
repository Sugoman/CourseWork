FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["LearningAPI/LearningAPI.csproj", "LearningAPI/"]
COPY ["LearningTrainerShared/LearningTrainerShared.csproj", "LearningTrainerShared/"]
RUN dotnet restore "./LearningAPI/LearningAPI.csproj"

COPY . .
WORKDIR /src/LearningAPI
RUN dotnet publish -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS final

WORKDIR /app

COPY --from=build /app/publish .

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV LC_ALL=en_US.UTF-8
ENV LANG=en_US.UTF-8

EXPOSE 8080

ENTRYPOINT ["dotnet", "LearningAPI.dll"]