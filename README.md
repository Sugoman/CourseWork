# 📚 LearningTrainer

<div align="center">

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4?style=for-the-badge&logo=blazor&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-Desktop-0078D4?style=for-the-badge&logo=windows&logoColor=white)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL_Server-2022-CC2927?style=for-the-badge&logo=microsoftsqlserver&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?style=for-the-badge&logo=docker&logoColor=white)

### 🌍 Универсальная платформа для изучения иностранных языков

*Интервальное повторение • Маркетплейс контента • Сообщество*

![Build Status](https://img.shields.io/badge/build-passing-brightgreen?style=flat-square)
![Tests](https://img.shields.io/badge/tests-22%20passed-success?style=flat-square)
![License](https://img.shields.io/badge/license-MIT-blue?style=flat-square)

</div>

---

## ✨ Что такое LearningTrainer?

**LearningTrainer** — кроссплатформенная система для изучения иностранных языков с современным веб-интерфейсом в стиле Stripe/Dribbble:

| Платформа | Описание |
|-----------|----------|
| 🖥️ **WPF Desktop** | Полнофункциональный клиент для Windows с offline-режимом |
| 🌐 **Blazor Web** | Современный Landing Page с маркетплейсом контента |
| 🔧 **REST API** | Бэкенд на ASP.NET Core с JWT-аутентификацией |

### 🎨 Современный веб-дизайн

Веб-приложение построено по принципам современного UI/UX:
- **Hero секция** на всю ширину с градиентом и декоративными элементами
- **Glassmorphism** карточки с многослойными тенями
- **Адаптивная вёрстка** для всех устройств (включая iPhone SE)
- **Горизонтальное меню** вместо классического сайдбара
- **Шрифты** Inter + Poppins

---

## 🛒 Маркетплейс контента

Делитесь знаниями с сообществом:

| Функция | Описание |
|---------|----------|
| 📤 **Публикация** | Опубликуйте свои словари и правила одним кликом |
| 📥 **Скачивание** | Находите и скачивайте материалы других пользователей |
| ⭐ **Рейтинги** | Оценивайте контент от 1 до 5 звёзд |
| 💬 **Комментарии** | Оставляйте отзывы и обратную связь |
| 🔍 **Поиск** | Находите по языкам, категориям, уровню сложности |

---

## 🌐 Поддерживаемые языки

Создавайте словари для **любой языковой пары**:

| Примеры | Направление |
|---------|-------------|
| 🇬🇧 → 🇷🇺 | Английский для русскоговорящих |
| 🇩🇪 → 🇬🇧 | Немецкий для англоговорящих |
| 🇯🇵 → 🇷🇺 | Японский для русскоговорящих |
| 🇫🇷 → 🇪🇸 | Французский для испаноговорящих |
| 🇨🇳 → 🇩🇪 | Китайский для немецкоговорящих |

---

## ⚡ Ключевые возможности

| Функция | Описание |
|---------|----------|
| 🔐 **Аутентификация** | JWT токены, регистрация с кодом учителя |
| 👥 **Роли** | User, Teacher, Student, Admin |
| 📚 **Словари** | Любые языковые пары, импорт/экспорт JSON |
| 📝 **Правила** | Markdown-редактор с live-preview |
| 🎓 **Обучение** | Интервальное повторение (SM-2 алгоритм) |
| 📊 **Статистика** | Графики прогресса, streak |
| 🔤 **Транскрипция** | IPA, ромадзи, пиньинь |
| 🎨 **Темы** | Light, Dark, Dracula, Forest |
| 🌍 **Локализация** | EN, RU, ES, DE, ZH |
| 📤 **Sharing** | Учитель → Ученики |

---

## 📁 Структура проекта

```
CourseWork/
├── 🖥️ LearningTrainer/        # WPF Desktop клиент
│   ├── Views/                  # XAML представления
│   ├── ViewModels/             # MVVM ViewModels
│   ├── Services/               # API, Settings, Permissions
│   └── Resources/              # Темы, языки, иконки
├── 🌐 LearningTrainerWeb/      # Blazor Server веб-приложение
│   ├── Components/
│   │   ├── Layout/             # MainLayout, NavMenu, AuthStatus
│   │   └── Pages/              # Home, Dictionaries, Rules, etc.
│   ├── Services/               # AuthService, ContentApiService
│   └── wwwroot/css/            # Современные CSS стили
├── 🔧 LearningAPI/             # REST API Backend
│   ├── Controllers/            # Auth, Marketplace, Progress
│   └── Middleware/             # Exception handling
├── 📦 LearningTrainerShared/   # Общая библиотека
│   ├── Models/                 # Entities, DTOs
│   └── Context/                # EF DbContext
├── 🧪 LearningAPI.Tests/       # Unit-тесты (22)
├── 📈 StressTestClient/        # Нагрузочное тестирование
└── 📄 docs/                    # Документация
```

---

## 🚀 Быстрый старт

### Требования

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (рекомендуется)
- Visual Studio 2022 / JetBrains Rider

### Вариант 1: Docker Compose (рекомендуется)

```bash
# Клонировать и запустить
git clone https://github.com/Sugoman/CourseWork.git
cd CourseWork/LearningTrainer
docker-compose up --build
```

После запуска:
| Сервис | URL |
|--------|-----|
| 🔧 API + Swagger | http://localhost:5077/swagger |
| 🌐 Web App | http://localhost:5078 |
| 🗄️ SQL Server | localhost:14333 |

### Вариант 2: Локальный запуск

```bash
# 1. Клонировать репозиторий
git clone https://github.com/Sugoman/CourseWork.git
cd CourseWork

# 2. Запустить SQL Server (Docker)
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=MySuperStrong!Pass123" \
  -p 14333:1433 --name learning_sql -d mcr.microsoft.com/mssql/server:2022-latest

# 3. Применить миграции и запустить API
cd LearningAPI
dotnet ef database update --project ../LearningTrainerShared
dotnet run

# 4. Запустить Web-приложение (новый терминал)
cd ../LearningTrainerWeb
dotnet run

# 5. Или запустить WPF клиент (новый терминал)
cd ../LearningTrainer
dotnet run
```

---

## 🛠️ Технологии

| Слой | Стек |
|------|------|
| **Desktop** | WPF, MVVM, WebView2, LiveCharts2 |
| **Web** | Blazor Server, Bootstrap 5, CSS3 (Custom Properties, Gradients, Animations) |
| **Backend** | ASP.NET Core 8.0, EF Core 9.0, MediatR |
| **Database** | SQL Server 2022, SQLite (offline) |
| **Auth** | JWT Bearer Tokens, BCrypt |
| **Container** | Docker, Docker Compose |
| **Markdown** | Markdig |
| **Tests** | xUnit, Moq |

---

## 📊 API Endpoints

### Аутентификация
| Method | Endpoint | Описание |
|--------|----------|----------|
| POST | `/api/auth/login` | Вход (Login или Email) |
| POST | `/api/auth/register` | Регистрация |
| POST | `/api/auth/refresh` | Обновление токена |

### Маркетплейс
| Method | Endpoint | Описание |
|--------|----------|----------|
| GET | `/api/marketplace/dictionaries` | Публичные словари |
| GET | `/api/marketplace/rules` | Публичные правила |
| POST | `/api/marketplace/{type}/{id}/download` | Скачать контент |
| POST | `/api/marketplace/{type}/{id}/publish` | Опубликовать |
| POST | `/api/marketplace/{type}/{id}/comments` | Добавить отзыв |

### Контент и прогресс
| Method | Endpoint | Описание |
|--------|----------|----------|
| GET/POST | `/api/dictionaries` | Словари пользователя |
| GET/POST | `/api/rules` | Правила пользователя |
| GET | `/api/progress/session/{id}` | Сессия обучения |
| POST | `/api/progress/update` | Обновить прогресс |

---

## 🧪 Тестирование

```bash
dotnet test LearningAPI.Tests
```

**Результат:** ✅ 22 теста пройдено

---

## 📄 Документация

| Документ | Описание |
|----------|----------|
| [docs/README.md](docs/README.md) | 📑 Индекс документации |
| [docs/QUICKSTART.md](docs/QUICKSTART.md) | 🚀 Быстрый старт |
| [docs/MARKETPLACE.md](docs/MARKETPLACE.md) | 🛒 Маркетплейс |
| [docs/TECHNICAL_SUMMARY.md](docs/TECHNICAL_SUMMARY.md) | 🔬 Техническое описание |
| [docs/EXPERIMENTS.md](docs/EXPERIMENTS.md) | 🧪 Эксперименты |

---

## 📸 Скриншоты

### 🌐 Web Marketplace
- Современный Landing Page с градиентным Hero
- Каталог словарей и правил с hover-эффектами
- Детальные страницы с комментариями (адаптивные)
- Публикация контента через личный кабинет

### 🖥️ WPF Desktop
- Интерфейс обучения с интервальным повторением
- Markdown редактор правил с live-preview
- Dashboard со статистикой и графиками

---

## 👨‍💻 Автор

**Студент:** Речицкий Александр Валентинович  
**Группа:** ИСПП-21  
**GitHub:** [@Sugoman](https://github.com/Sugoman)

---

## 📝 Лицензия

MIT License © 2025

---

<div align="center">

Made with ❤️ using 

![.NET](https://img.shields.io/badge/.NET-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![Blazor](https://img.shields.io/badge/Blazor-512BD4?style=flat-square&logo=blazor&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-0078D4?style=flat-square&logo=windows&logoColor=white)

**🌍 Learn any language you want! Share with the community!**

</div>
