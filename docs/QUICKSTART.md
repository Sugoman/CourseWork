# 🚀 Быстрый старт LearningTrainer

<div align="center">

*Запустите проект за 5 минут*

</div>

---

## ⚡ Самый быстрый способ

### Docker Compose (рекомендуется)

```bash
# 1. Клонирование
git clone https://github.com/Sugoman/CourseWork.git
cd CourseWork/LearningTrainer

# 2. Запуск всех сервисов
docker-compose up --build
```

### После запуска

| Сервис | URL | Описание |
|--------|-----|----------|
| 🔧 **API + Swagger** | http://localhost:5077/swagger | REST API документация |
| 🌐 **Web App** | http://localhost:5078 | Blazor маркетплейс |
| 🗄️ **SQL Server** | localhost:14333 | База данных |

**Credentials SQL Server:**
- User: `sa`
- Password: `MySuperStrong!Pass123`

---

## 🛠️ Локальный запуск (без Docker)

### Шаг 1: Подготовка

```bash
# Клонирование
git clone https://github.com/Sugoman/CourseWork.git
cd CourseWork

# SQL Server через Docker (всё ещё нужен)
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=MySuperStrong!Pass123" \
  -p 14333:1433 --name learning_sql -d mcr.microsoft.com/mssql/server:2022-latest
```

### Шаг 2: API Backend

```bash
cd LearningAPI

# Применить миграции
dotnet ef database update --project ../LearningTrainerShared

# Запустить API
dotnet run
```
✅ API доступен: http://localhost:5077/swagger

### Шаг 3: Web-приложение (новый терминал)

```bash
cd LearningTrainerWeb
dotnet run
```
✅ Web доступен: http://localhost:5078

### Шаг 4: WPF Desktop (опционально)

```bash
cd LearningTrainer
dotnet run
```
✅ Откроется Windows-приложение

---

## 👥 Роли в системе

| Роль | Описание | Как получить |
|------|----------|--------------|
| **User** | Базовый пользователь | Регистрация без кода |
| **Teacher** | Учитель с учениками | Settings → "Стать учителем" |
| **Student** | Ученик учителя | Регистрация с invite-кодом |
| **Admin** | Полный доступ | Назначается в БД |

---

## 🛒 Использование маркетплейса

### В Web-приложении

1. Откройте http://localhost:5078
2. Просматривайте каталог словарей и правил
3. Зарегистрируйтесь для скачивания и комментирования
4. В разделе "Мой контент" управляйте публикациями

### В WPF-приложении

1. Создайте словарь или правило
2. Откройте для редактирования
3. Нажмите **"Опубликовать на сайте"** (зелёная кнопка)
4. Контент появится в веб-маркетплейсе

---

## 🧪 Тестирование

```bash
# Запуск всех тестов
dotnet test LearningAPI.Tests

# Результат: ✅ 22 теста пройдено
```

---

## 📋 Требования

| Компонент | Версия | Ссылка |
|-----------|--------|--------|
| .NET SDK | 8.0+ | [Скачать](https://dotnet.microsoft.com/download/dotnet/8.0) |
| Docker Desktop | Latest | [Скачать](https://www.docker.com/products/docker-desktop/) |
| IDE | VS 2022 / Rider | - |

---

## 🔧 Конфигурация

### appsettings.json (API)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,14333;Database=LearningTrainer;User Id=sa;Password=MySuperStrong!Pass123;TrustServerCertificate=true"
  },
  "Jwt": {
    "Secret": "your-super-secret-key-min-32-chars",
    "Issuer": "LearningTrainerAPI",
    "Audience": "LearningTrainerClients"
  }
}
```

### appsettings.json (Web)

```json
{
  "ApiBaseUrl": "http://localhost:5077"
}
```

---

## ❓ Troubleshooting

### Порт занят
```bash
# Найти процесс на порту
netstat -ano | findstr :5077
# Завершить процесс
taskkill /PID <PID> /F
```

### SQL Server не запускается
```bash
# Проверить логи
docker logs learning_sql
# Перезапустить
docker restart learning_sql
```

### Миграции не применяются
```bash
cd LearningAPI
dotnet ef database drop --project ../LearningTrainerShared --force
dotnet ef database update --project ../LearningTrainerShared
```

---

## 🚀 Следующие шаги

1. 📖 Изучите [MARKETPLACE.md](MARKETPLACE.md) для работы с маркетплейсом
2. 🔬 Посмотрите [TECHNICAL_SUMMARY.md](TECHNICAL_SUMMARY.md) для понимания архитектуры
3. 🧪 Запустите [EXPERIMENTS.md](EXPERIMENTS.md) для тестирования производительности

