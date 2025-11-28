# ----------------------------------------------------------------------
# ЭТАП 1: СБОРКА (BUILD) - Используем SDK для компиляции
# ----------------------------------------------------------------------
# Используем образ Debian (bookworm-slim) для сборки, он стабильнее Alpine и включает ICU.
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# 1. Копирование файлов проекта и восстановление зависимостей (NuGet)
# Это позволяет кэшировать восстановление, если только файлы .csproj не менялись.
COPY ["LearningAPI/LearningAPI.csproj", "LearningAPI/"]
COPY ["LearningTrainerShared/LearningTrainerShared.csproj", "LearningTrainerShared/"]
RUN dotnet restore "./LearningAPI/LearningAPI.csproj"

# 2. Копирование остального кода и публикация
COPY . .
WORKDIR /src/LearningAPI
RUN dotnet publish -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false


# ----------------------------------------------------------------------
# ЭТАП 2: ЗАПУСК (FINAL) - Используем минимальный Runtime для работы
# ----------------------------------------------------------------------
# Переключаемся на образ ASP.NET Runtime (еще более легкий), который не содержит SDK.
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS final

WORKDIR /app
# USER $APP_UID # Используем стандартный не-root пользователь для безопасности

# 1. Копирование опубликованных файлов
COPY --from=build /app/publish .

# 2. Настройка глобализации и культуры (Хотя Debian включает ICU, это лучшая практика)
# Мы явно устанавливаем UTF-8 и отключаем инвариантный режим, чтобы избежать CultureNotFoundException.
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV LC_ALL=en_US.UTF-8
ENV LANG=en_US.UTF-8

# Открываем порт контейнера
EXPOSE 8080

# Команда для запуска приложения
ENTRYPOINT ["dotnet", "LearningAPI.dll"]