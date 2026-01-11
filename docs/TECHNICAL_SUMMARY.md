# 🔬 Техническое резюме проекта LearningTrainer

<div align="center">

*Детальное техническое описание архитектуры и технологий*

</div>

---

## 📊 Быстрый обзор

| Аспект | Описание |
|--------|---------|
| **Название** | LearningTrainer (CourseWork) |
| **Тип** | Платформа для изучения иностранных языков |
| **Платформы** | Windows Desktop (WPF) + Web (Blazor Server) + REST API |
| **Язык** | C# 12 / .NET 8 |
| **Архитектура** | N-tier + MVVM + CQRS |
| **БД** | SQL Server 2022 (API) + SQLite (Client) |
| **Контейнеризация** | Docker Compose |
| **UI** | Modern CSS (Glassmorphism, Gradients) |
| **Статус** | Активная разработка 🟢 |

---

## 📈 Статистика проекта

### Размер кодовой базы

| Проект | Файлов | Строк кода | Назначение |
|--------|--------|-----------|-----------|
| **LearningTrainer** | 70+ | ~16 000 | WPF Desktop Client |
| **LearningTrainerWeb** | 20+ | ~3 000 | Blazor Server (Маркетплейс) |
| **LearningAPI** | 12+ | ~3 500 | REST API Backend |
| **LearningTrainerShared** | 45+ | ~5 500 | Shared Models & Logic |
| **LearningAPI.Tests** | 10+ | ~1 000 | Unit Tests (22 теста) |
| **StressTestClient** | 1 | ~200 | Load Testing |
| **TOTAL** | 160+ | ~29 200 | Всё вместе |

### Распределение компонентов

```
Frontend (WPF) ─────────────────────────────────
├── Views & ViewModels       35 файлов (~8000 строк)
├── Services                 18 файлов (~4500 строк)
├── Core & Utilities         15 файлов (~2500 строк)
└── Converters & Behaviors   5 файлов (~500 строк)

Frontend (Blazor) ──────────────────────────────
├── Components/Pages         12 файлов (~1800 строк)
├── Components/Layout        4 файла (~400 строк)
├── Services                 3 файла (~800 строк)
└── CSS Styles              1 файл (~1400 строк)

Backend (API) ──────────────────────────────────
├── Controllers              10 файлов (~2500 строк)
├── Middleware               1 файл (~200 строк)
└── Configuration            2 файла (~300 строк)

Shared ─────────────────────────────────────────
├── Entities                 12 файлов (~800 строк)
├── DTOs & Requests          15 файлов (~600 строк)
├── DbContexts               3 файла (~500 строк)
├── MediatR Handlers         2 файла (~300 строк)
└── Migrations               10 файлов (~1000 строк)
```

---

## 🛠️ Технологический стек

### Backend (.NET 8)

```
ASP.NET Core Web API 8.0
├── Authentication & Authorization
│   ├── JWT Bearer (Microsoft.AspNetCore.Authentication.JwtBearer)
│   └── Role-based Access Control (RBAC)
├── Data Access
│   ├── Entity Framework Core 9.0
│   │   ├── SQL Server (Microsoft.EntityFrameworkCore.SqlServer)
│   │   ├── SQLite (Microsoft.Data.Sqlite)
│   │   └── Migrations
│   └── Connection Pooling
├── Business Logic
│   ├── MediatR 14.0 (CQRS Pattern)
│   └── Dependency Injection (Built-in)
├── API Documentation
│   └── Swagger/OpenAPI (Swashbuckle.AspNetCore)
├── Security
│   ├── BCrypt.Net - Password Hashing
│   └── HTTPS/TLS
└── Utilities
    ├── Nanoid - Unique ID Generation
    └── System.Net.Http.Json
```

### Frontend (WPF)

```
Windows Presentation Foundation
├── UI Framework
│   ├── XAML-based UI
│   ├── Data Binding (TwoWay)
│   └── Attached Behaviors
├── Architecture
│   ├── MVVM Pattern
│   │   ├── ViewModels (~18)
│   │   ├── Views (~15)
│   │   └── Converters (~8)
│   └── Services (~15)
├── Content Rendering
│   ├── Markdown (Markdig + MdXaml)
│   ├── SVG (SharpVectors.Wpf)
│   └── WebView2 (Chromium)
├── Data Access
│   ├── EF Core + SQLite
│   ├── HTTP Client for API
│   └── Local Database
└── Features
    ├── Fuzzy Search (FuzzySharp)
    ├── Spell Check
    ├── Theme Management (4 темы)
    ├── Localization (5 языков)
    └── Marketplace Publishing
```

### Frontend (Blazor Server)

```
Blazor Server .NET 8
├── Components
│   ├── Pages
│   │   ├── Home.razor (Landing Page)
│   │   ├── Dictionaries.razor (Каталог)
│   │   ├── Rules.razor (Каталог)
│   │   ├── DictionaryDetails.razor
│   │   ├── RuleDetails.razor (Адаптивный)
│   │   ├── MyContent.razor (Личный кабинет)
│   │   ├── Login.razor / Register.razor
│   │   └── ...
│   └── Layout
│       ├── MainLayout.razor (Горизонтальное меню)
│       ├── NavMenu.razor
│       └── AuthStatus.razor
├── Services
│   ├── AuthService (JWT + Session Storage)
│   └── ContentApiService (Marketplace API)
├── Styling
│   ├── app.css (~1400 строк)
│   │   ├── CSS Custom Properties
│   │   ├── Gradients & Animations
│   │   ├── Glassmorphism Cards
│   │   └── Mobile-First Responsive
│   ├── Bootstrap 5.3
│   └── Bootstrap Icons
└── Features
    ├── Каталог публичного контента
    ├── Поиск и фильтрация
    ├── Рейтинги и комментарии
    ├── Скачивание контента
    └── Личный кабинет
```

### Дизайн-система (CSS)

```css
/* Основные переменные */
:root {
    /* Цвета */
    --primary-500: #6366f1;  /* Indigo */
    --accent-purple: #a855f7; /* Violet */
    
    /* Тени */
    --shadow-card: 0 4px 20px -2px rgba(0,0,0,0.08);
    --shadow-card-hover: 0 20px 40px -8px rgba(99,102,241,0.15);
    
    /* Скругления */
    --radius-xl: 20px;
    
    /* Анимации */
    --transition: 200ms cubic-bezier(0.4, 0, 0.2, 1);
}
```

---

## 🗄️ База данных

### SQL Server 2022 (Production)

```
Tables
├── Users
│   ├── Id, Login, Email, PasswordHash, RoleId
│   ├── UserId (FK → Teacher)
│   ├── InviteCode, RefreshToken
│   └── CreatedAt
├── Dictionaries
│   ├── Id, UserId, Name, Description
│   ├── LanguageFrom, LanguageTo
│   ├── IsPublished, Rating, RatingCount, DownloadCount
│   ├── SourceDictionaryId (FK, если скачан)
│   └── IsDeleted (Soft Delete)
├── Words
│   ├── Id, DictionaryId
│   ├── OriginalWord, Translation, Example
│   ├── Phonetics (JSON)
│   └── DifficultyLevel
├── Rules
│   ├── Id, UserId, Title, MarkdownContent, HtmlContent
│   ├── Category, DifficultyLevel
│   ├── IsPublished, Rating, RatingCount, DownloadCount
│   └── SourceRuleId (FK)
├── Comments
│   ├── Id, UserId
│   ├── ContentType ("Dictionary" | "Rule")
│   ├── ContentId, Rating (1-5), Text
│   └── CreatedAt
├── Downloads
│   ├── Id, UserId
│   ├── ContentType, ContentId
│   └── DownloadedAt
├── LearningProgress
│   ├── UserId, WordId (Composite PK)
│   ├── KnowledgeLevel (0-5), NextReview
│   └── CorrectAnswers, TotalAttempts
├── DictionarySharing / RuleSharing
│   └── UserId, DictionaryId/RuleId, SharedAt
└── Roles
    └── Id, Name (Admin, Teacher, Student, User)
```

### SQLite (Local Client)
- Зеркало SQL Server для offline-режима
- Settings & Preferences
- Local-only data

---

## 🌐 API Endpoints

### Authentication
```http
POST   /api/auth/login               # Вход (Login/Email)
POST   /api/auth/register            # Регистрация
POST   /api/auth/refresh             # Обновление токена
PUT    /api/auth/change-password     # Смена пароля
POST   /api/auth/upgrade-to-teacher  # Стать учителем
```

### Dictionaries & Words
```http
GET    /api/dictionaries             # Словари (pagination)
GET    /api/dictionaries/{id}        # Детали + слова
POST   /api/dictionaries             # Создать
PUT    /api/dictionaries/{id}        # Обновить
DELETE /api/dictionaries/{id}        # Удалить

GET    /api/words/dictionary/{id}    # Слова словаря
POST   /api/words                    # Добавить слово
PUT    /api/words/{id}               # Обновить
DELETE /api/words/{id}               # Удалить
```

### Rules
```http
GET    /api/rules                    # Список правил
GET    /api/rules/{id}               # Детали
POST   /api/rules                    # Создать
PUT    /api/rules/{id}               # Обновить
DELETE /api/rules/{id}               # Удалить
```

### Marketplace
```http
# Публичный контент
GET    /api/marketplace/dictionaries           # Каталог
GET    /api/marketplace/dictionaries/{id}      # Детали
GET    /api/marketplace/rules                  # Каталог
GET    /api/marketplace/rules/{id}             # Детали

# Комментарии
GET    /api/marketplace/{type}/{id}/comments   # Список
POST   /api/marketplace/{type}/{id}/comments   # Добавить

# Действия (JWT required)
POST   /api/marketplace/{type}/{id}/download   # Скачать
POST   /api/marketplace/{type}/{id}/publish    # Опубликовать
POST   /api/marketplace/{type}/{id}/unpublish  # Снять

# Личный контент
GET    /api/marketplace/my/dictionaries        # Мои словари
GET    /api/marketplace/my/rules               # Мои правила
GET    /api/marketplace/my/downloads           # Скачанное
```

### Learning & Progress
```http
GET    /api/progress/session/{id}    # Сессия обучения
POST   /api/progress/update          # Обновить прогресс
GET    /api/progress/stats           # Статистика
```

### Sharing
```http
POST   /api/sharing/dictionary/toggle       # Расшарить
POST   /api/sharing/rule/toggle             # Расшарить
GET    /api/sharing/dictionary/{id}/status  # Статус
GET    /api/classroom/students              # Ученики
```

---

## 🐳 Docker

### docker-compose.yml

```yaml
version: '3.8'
services:
  sql_server:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=MySuperStrong!Pass123
    ports:
      - "14333:1433"
    volumes:
      - sqlserver_data:/var/opt/mssql

  learningapi:
    build: ../LearningAPI
    ports:
      - "5077:5077"
    depends_on:
      - sql_server
    environment:
      - ConnectionStrings__DefaultConnection=...

volumes:
  sqlserver_data:
```

---

## 🔐 Безопасность

| Аспект | Реализация |
|--------|------------|
| **Пароли** | BCrypt хэширование (cost factor 12) |
| **Токены** | JWT с HS256, Access (15 min) + Refresh (7 days) |
| **Транспорт** | HTTPS/TLS |
| **CORS** | Whitelist trusted origins |
| **Авторизация** | `[Authorize]` атрибуты + Policies |
| **Валидация** | Model Validation + FluentValidation |
| **SQL Injection** | EF Core параметризованные запросы |
| **XSS** | Blazor автоэкранирование |

---

## 📱 Адаптивный дизайн

### Breakpoints

| Размер | Ширина | Поведение |
|--------|--------|-----------|
| **Desktop** | ≥992px | 3 колонки, горизонтальное меню |
| **Tablet** | 768-991px | 2 колонки, компактные отступы |
| **Mobile** | 576-767px | 1 колонка, вертикальные кнопки |
| **Small** | <576px | iPhone SE оптимизация |

### Mobile-First CSS

```css
/* Base (mobile) */
.popular-grid {
    grid-template-columns: 1fr;
}

/* Tablet */
@media (min-width: 576px) {
    .popular-grid {
        grid-template-columns: repeat(2, 1fr);
    }
}

/* Desktop */
@media (min-width: 992px) {
    .popular-grid {
        grid-template-columns: repeat(3, 1fr);
    }
}
```

---

## 🧪 Тестирование

### Unit Tests (xUnit + Moq)

```bash
dotnet test LearningAPI.Tests
# ✅ 22 теста пройдено
```

### Покрытие

| Область | Тесты |
|---------|-------|
| AuthController | 6 |
| DictionaryController | 5 |
| RulesController | 4 |
| MarketplaceController | 4 |
| ProgressController | 3 |

### Stress Testing

```bash
cd StressTestClient
dot## Response
````````markdown
# 🔬 Техническое резюме проекта LearningTrainer

<div align="center">

*Детальное техническое описание архитектуры и технологий*

</div>

---

## 📊 Быстрый обзор

| Аспект | Описание |
|--------|---------|
| **Название** | LearningTrainer (CourseWork) |
| **Тип** | Платформа для изучения иностранных языков |
| **Платформы** | Windows Desktop (WPF) + Web (Blazor Server) + REST API |
| **Язык** | C# 12 / .NET 8 |
| **Архитектура** | N-tier + MVVM + CQRS |
| **БД** | SQL Server 2022 (API) + SQLite (Client) |
| **Контейнеризация** | Docker Compose |
| **UI** | Modern CSS (Glassmorphism, Gradients) |
| **Статус** | Активная разработка 🟢 |

---

## 📈 Статистика проекта

### Размер кодовой базы

| Проект | Файлов | Строк кода | Назначение |
|--------|--------|-----------|-----------|
| **LearningTrainer** | 70+ | ~16 000 | WPF Desktop Client |
| **LearningTrainerWeb** | 20+ | ~3 000 | Blazor Server (Маркетплейс) |
| **LearningAPI** | 12+ | ~3 500 | REST API Backend |
| **LearningTrainerShared** | 45+ | ~5 500 | Shared Models & Logic |
| **LearningAPI.Tests** | 10+ | ~1 000 | Unit Tests (22 теста) |
| **StressTestClient** | 1 | ~200 | Load Testing |
| **TOTAL** | 160+ | ~29 200 | Всё вместе |

### Распределение компонентов

```
Frontend (WPF) ─────────────────────────────────
├── Views & ViewModels       35 файлов (~8000 строк)
├── Services                 18 файлов (~4500 строк)
├── Core & Utilities         15 файлов (~2500 строк)
└── Converters & Behaviors   5 файлов (~500 строк)

Frontend (Blazor) ──────────────────────────────
├── Components/Pages         12 файлов (~1800 строк)
├── Components/Layout        4 файла (~400 строк)
├── Services                 3 файла (~800 строк)
└── CSS Styles              1 файл (~1400 строк)

Backend (API) ──────────────────────────────────
├── Controllers              10 файлов (~2500 строк)
├── Middleware               1 файл (~200 строк)
└── Configuration            2 файла (~300 строк)

Shared ─────────────────────────────────────────
├── Entities                 12 файлов (~800 строк)
├── DTOs & Requests          15 файлов (~600 строк)
├── DbContexts               3 файла (~500 строк)
├── MediatR Handlers         2 файла (~300 строк)
└── Migrations               10 файлов (~1000 строк)
```

---

## 🛠️ Технологический стек

### Backend (.NET 8)

```
ASP.NET Core Web API 8.0
├── Authentication & Authorization
│   ├── JWT Bearer (Microsoft.AspNetCore.Authentication.JwtBearer)
│   └── Role-based Access Control (RBAC)
├── Data Access
│   ├── Entity Framework Core 9.0
│   │   ├── SQL Server (Microsoft.EntityFrameworkCore.SqlServer)
│   │   ├── SQLite (Microsoft.Data.Sqlite)
│   │   └── Migrations
│   └── Connection Pooling
├── Business Logic
│   ├── MediatR 14.0 (CQRS Pattern)
│   └── Dependency Injection (Built-in)
├── API Documentation
│   └── Swagger/OpenAPI (Swashbuckle.AspNetCore)
├── Security
│   ├── BCrypt.Net - Password Hashing
│   └── HTTPS/TLS
└── Utilities
    ├── Nanoid - Unique ID Generation
    └── System.Net.Http.Json
```

### Frontend (WPF)

```
Windows Presentation Foundation
├── UI Framework
│   ├── XAML-based UI
│   ├── Data Binding (TwoWay)
│   └── Attached Behaviors
├── Architecture
│   ├── MVVM Pattern
│   │   ├── ViewModels (~18)
│   │   ├── Views (~15)
│   │   └── Converters (~8)
│   └── Services (~15)
├── Content Rendering
│   ├── Markdown (Markdig + MdXaml)
│   ├── SVG (SharpVectors.Wpf)
│   └── WebView2 (Chromium)
├── Data Access
│   ├── EF Core + SQLite
│   ├── HTTP Client for API
│   └── Local Database
└── Features
    ├── Fuzzy Search (FuzzySharp)
    ├── Spell Check
    ├── Theme Management (4 темы)
    ├── Localization (5 языков)
    └── Marketplace Publishing
```

### Frontend (Blazor Server)

```
Blazor Server .NET 8
├── Components
│   ├── Pages
│   │   ├── Home.razor (Landing Page)
│   │   ├── Dictionaries.razor (Каталог)
│   │   ├── Rules.razor (Каталог)
│   │   ├── DictionaryDetails.razor
│   │   ├── RuleDetails.razor (Адаптивный)
│   │   ├── MyContent.razor (Личный кабинет)
│   │   ├── Login.razor / Register.razor
│   │   └── ...
│   └── Layout
│       ├── MainLayout.razor (Горизонтальное меню)
│       ├── NavMenu.razor
│       └── AuthStatus.razor
├── Services
│   ├── AuthService (JWT + Session Storage)
│   └── ContentApiService (Marketplace API)
├── Styling
│   ├── app.css (~1400 строк)
│   │   ├── CSS Custom Properties
│   │   ├── Gradients & Animations
│   │   ├── Glassmorphism Cards
│   │   └── Mobile-First Responsive
│   ├── Bootstrap 5.3
│   └── Bootstrap Icons
└── Features
    ├── Каталог публичного контента
    ├── Поиск и фильтрация
    ├── Рейтинги и комментарии
    ├── Скачивание контента
    └── Личный кабинет
```

### Дизайн-система (CSS)

```css
/* Основные переменные */
:root {
    /* Цвета */
    --primary-500: #6366f1;  /* Indigo */
    --accent-purple: #a855f7; /* Violet */
    
    /* Тени */
    --shadow-card: 0 4px 20px -2px rgba(0,0,0,0.08);
    --shadow-card-hover: 0 20px 40px -8px rgba(99,102,241,0.15);
    
    /* Скругления */
    --radius-xl: 20px;
    
    /* Анимации */
    --transition: 200ms cubic-bezier(0.4, 0, 0.2, 1);
}
```

---

## 🗄️ База данных

### SQL Server 2022 (Production)

```
Tables
├── Users
│   ├── Id, Login, Email, PasswordHash, RoleId
│   ├── UserId (FK → Teacher)
│   ├── InviteCode, RefreshToken
│   └── CreatedAt
├── Dictionaries
│   ├── Id, UserId, Name, Description
│   ├── LanguageFrom, LanguageTo
│   ├── IsPublished, Rating, RatingCount, DownloadCount
│   ├── SourceDictionaryId (FK, если скачан)
│   └── IsDeleted (Soft Delete)
├── Words
│   ├── Id, DictionaryId
│   ├── OriginalWord, Translation, Example
│   ├── Phonetics (JSON)
│   └── DifficultyLevel
├── Rules
│   ├── Id, UserId, Title, MarkdownContent, HtmlContent
│   ├── Category, DifficultyLevel
│   ├── IsPublished, Rating, RatingCount, DownloadCount
│   └── SourceRuleId (FK)
├── Comments
│   ├── Id, UserId
│   ├── ContentType ("Dictionary" | "Rule")
│   ├── ContentId, Rating (1-5), Text
│   └── CreatedAt
├── Downloads
│   ├── Id, UserId
│   ├── ContentType, ContentId
│   └── DownloadedAt
├── LearningProgress
│   ├── UserId, WordId (Composite PK)
│   ├── KnowledgeLevel (0-5), NextReview
│   └── CorrectAnswers, TotalAttempts
├── DictionarySharing / RuleSharing
│   └── UserId, DictionaryId/RuleId, SharedAt
└── Roles
    └── Id, Name (Admin, Teacher, Student, User)
```

### SQLite (Local Client)
- Зеркало SQL Server для offline-режима
- Settings & Preferences
- Local-only data

---

## 🌐 API Endpoints

### Authentication
```http
POST   /api/auth/login               # Вход (Login/Email)
POST   /api/auth/register            # Регистрация
POST   /api/auth/refresh             # Обновление токена
PUT    /api/auth/change-password     # Смена пароля
POST   /api/auth/upgrade-to-teacher  # Стать учителем
```

### Dictionaries & Words
```http
GET    /api/dictionaries             # Словари (pagination)
GET    /api/dictionaries/{id}        # Детали + слова
POST   /api/dictionaries             # Создать
PUT    /api/dictionaries/{id}        # Обновить
DELETE /api/dictionaries/{id}        # Удалить

GET    /api/words/dictionary/{id}    # Слова словаря
POST   /api/words                    # Добавить слово
PUT    /api/words/{id}               # Обновить
DELETE /api/words/{id}               # Удалить
```

### Rules
```http
GET    /api/rules                    # Список правил
GET    /api/rules/{id}               # Детали
POST   /api/rules                    # Создать
PUT    /api/rules/{id}               # Обновить
DELETE /api/rules/{id}               # Удалить
```

### Marketplace
```http
# Публичный контент
GET    /api/marketplace/dictionaries           # Каталог
GET    /api/marketplace/dictionaries/{id}      # Детали
GET    /api/marketplace/rules                  # Каталог
GET    /api/marketplace/rules/{id}             # Детали

# Комментарии
GET    /api/marketplace/{type}/{id}/comments   # Список
POST   /api/marketplace/{type}/{id}/comments   # Добавить

# Действия (JWT required)
POST   /api/marketplace/{type}/{id}/download   # Скачать
POST   /api/marketplace/{type}/{id}/publish    # Опубликовать
POST   /api/marketplace/{type}/{id}/unpublish  # Снять

# Личный контент
GET    /api/marketplace/my/dictionaries        # Мои словари
GET    /api/marketplace/my/rules               # Мои правила
GET    /api/marketplace/my/downloads           # Скачанное
```

### Learning & Progress
```http
GET    /api/progress/session/{id}    # Сессия обучения
POST   /api/progress/update          # Обновить прогресс
GET    /api/progress/stats           # Статистика
```

### Sharing
```http
POST   /api/sharing/dictionary/toggle       # Расшарить
POST   /api/sharing/rule/toggle             # Расшарить
GET    /api/sharing/dictionary/{id}/status  # Статус
GET    /api/classroom/students              # Ученики
```

---

## 🐳 Docker

### docker-compose.yml

```yaml
version: '3.8'
services:
  sql_server:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=MySuperStrong!Pass123
    ports:
      - "14333:1433"
    volumes:
      - sqlserver_data:/var/opt/mssql

  learningapi:
    build: ../LearningAPI
    ports:
      - "5077:5077"
    depends_on:
      - sql_server
    environment:
      - ConnectionStrings__DefaultConnection=...

volumes:
  sqlserver_data:
```

---

## 🔐 Безопасность

| Аспект | Реализация |
|--------|------------|
| **Пароли** | BCrypt хэширование (cost factor 12) |
| **Токены** | JWT с HS256, Access (15 min) + Refresh (7 days) |
| **Транспорт** | HTTPS/TLS |
| **CORS** | Whitelist trusted origins |
| **Авторизация** | `[Authorize]` атрибуты + Policies |
| **Валидация** | Model Validation + FluentValidation |
| **SQL Injection** | EF Core параметризованные запросы |
| **XSS** | Blazor автоэкранирование |

---

## 📱 Адаптивный дизайн

### Breakpoints

| Размер | Ширина | Поведение |
|--------|--------|-----------|
| **Desktop** | ≥992px | 3 колонки, горизонтальное меню |
| **Tablet** | 768-991px | 2 колонки, компактные отступы |
| **Mobile** | 576-767px | 1 колонка, вертикальные кнопки |
| **Small** | <576px | iPhone SE оптимизация |

### Mobile-First CSS

```css
/* Base (mobile) */
.popular-grid {
    grid-template-columns: 1fr;
}

/* Tablet */
@media (min-width: 576px) {
    .popular-grid {
        grid-template-columns: repeat(2, 1fr);
    }
}

/* Desktop */
@media (min-width: 992px) {
    .popular-grid {
        grid-template-columns: repeat(3, 1fr);
    }
}
```

---

## 🧪 Тестирование

### Unit Tests (xUnit + Moq)

```bash
dotnet test LearningAPI.Tests
# ✅ 22 теста пройдено
```

### Покрытие

| Область | Тесты |
|---------|-------|
| AuthController | 6 |
| DictionaryController | 5 |
| RulesController | 4 |
| MarketplaceController | 4 |
| ProgressController | 3 |

### Stress Testing

```bash
cd StressTestClient
dotnet run -- --users 100 --duration 60

