FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV LC_ALL=ru_RU.UTF-8
ENV LANG=ru_RU.UTF-8
ENV ASPNETCORE_URLS=http://+:8080

ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["LearningTrainerWeb/LearningTrainerWeb.csproj", "LearningTrainerWeb/"]
COPY ["LearningTrainerShared/LearningTrainerShared.csproj", "LearningTrainerShared/"]
RUN dotnet restore "./LearningTrainerWeb/LearningTrainerWeb.csproj"

COPY . .
WORKDIR /src/LearningTrainerWeb
RUN dotnet publish -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       locales \
       icu-devtools \
       libicu-dev \
    && rm -rf /var/lib/apt/lists/*

RUN sed -i 's/^# *\(ru_RU.UTF-8\)/\1/' /etc/locale.gen \
    && locale-gen

COPY --from=build /app/publish .



EXPOSE 8080

ENTRYPOINT ["dotnet", "LearningTrainerWeb.dll"]
