# 📚 Ingat

<div align="center">

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4?style=for-the-badge&logo=blazor&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-Desktop-0078D4?style=for-the-badge&logo=windows&logoColor=white)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL_Server-2022-CC2927?style=for-the-badge&logo=microsoftsqlserver&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-Cache-DC382D?style=for-the-badge&logo=redis&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?style=for-the-badge&logo=docker&logoColor=white)
![AI](https://img.shields.io/badge/AI-Service-FF6F61?style=for-the-badge&logo=openai&logoColor=white)

### 🌍 Универсальная платформа для изучения иностранных языков

*Интервальное повторение • 8 режимов тренировки • AI-генерация • Маркетплейс • Сообщество*

![Build Status](https://img.shields.io/badge/build-passing-brightgreen?style=flat-square)
![Tests](https://img.shields.io/badge/tests-229%20passed-success?style=flat-square)
![Coverage](https://img.shields.io/badge/coverage-100%25-brightgreen?style=flat-square)
![License](https://img.shields.io/badge/license-MIT-blue?style=flat-square)

</div>

---

## ✨ Что такое Ingat?

**Ingat** — кроссплатформенная система для изучения иностранных языков с современным веб-интерфейсом в стиле Stripe/Dribbble:

| Платформа | Описание |
|-----------|----------|
| 🖥️ **WPF Desktop** | Полнофункциональный клиент для Windows с offline-режимом |
| 🌐 **Blazor Web** | Современное SPA с 8 режимами тренировки, AI-генерацией и маркетплейсом |
| 🔧 **REST API** | Бэкенд на ASP.NET Core с JWT-аутентификацией |
| 🤖 **Ingat.AI** | Микросервис AI-генерации словарей и мнемоник |

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
| 👤 **Профили** | Публичные профили авторов с достижениями и контентом |

---

## 👤 Профили пользователей

Каждый пользователь имеет публичный профиль, доступный по ссылке `/profile/{id}`:

| Элемент | Описание |
|---------|----------|
| 🏆 **Достижения** | 19 достижений в 6 категориях (Learning, Consistency, Accuracy, Speed, Explorer, Social) с 5 уровнями редкости — от Common до Legendary |
| 🔥 **Streak** | Текущая серия дней + личный рекорд в одном блоке |
| 📚 **Контент** | Опубликованные словари и правила с рейтингом и счётчиком скачиваний |
| 📄 **Пагинация** | Автоматическая при большом количестве опубликованного контента (6 на страницу) |
| 📱 **Адаптивность** | Полная адаптация для мобильных, планшетов и десктопа |

**Точки входа в профиль:**
- Аватарка в шапке сайта (`AuthStatus`) → собственный профиль
- Карточка автора на странице словаря / правила → профиль автора
- Аватарки в комментариях → профиль комментатора

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
| 🔐 **Аутентификация** | JWT токены, регистрация (Username, Email), сессии переживают рестарт контейнера (Data Protection) |
| 👥 **Роли** | User, Teacher, Student, Admin |
| 📚 **Словари** | Любые языковые пары, импорт/экспорт JSON/CSV, импорт из Anki (.apkg) |
| 📝 **Правила** | Markdown-редактор с live-preview |
| 🧠 **Обучение** | Полноценный SM-2 алгоритм (EaseFactor, адаптивные интервалы, leech-detection) |
| 🃏 **8 режимов тренировки** | Flashcards, MCQ, Typing, Listening, Matching, Cloze (пропуски), Spelling Bee, Mixed |
| ⚡ **Speed Round** | Режим на скорость с таймером и подсчётом очков |
| 🎯 **Дневная цель** | Настраиваемая цель + прогресс-бар + ежедневные челленджи |
| 🐛 **Leech Management** | Автоматическое обнаружение «слов-пиявок» с AI-мнемониками |
| 🍅 **Pomodoro** | Встроенные перерывы после длительных сессий |
| 🔊 **Озвучка** | Локальный нейро-TTS через Piper (ONNX, офлайн) + Web Speech API (Blazor) |
| 📊 **Статистика** | XP-система, графики прогресса, streak, подробная аналитика |
| 🏆 **Достижения** | 19 достижений в 6 категориях (5 уровней редкости) |
| 🤖 **AI-генерация** | Автоматическая генерация словарей и мнемоник через Ingat.AI |
| 📝 **Заметки** | Персональные мнемоники и заметки к каждому слову |
| 🔤 **Транскрипция** | IPA, ромадзи, пиньинь |
| 🎨 **Темы** | Light, Dark, Dracula, Forest |
| 🌍 **Локализация** | EN, RU, ES, DE, ZH |
| 📤 **Sharing** | Учитель → Ученики |
| 👤 **Профили** | Публичные профили с достижениями, streak, опубликованным контентом |
| 🐳 **Docker** | Полная контейнеризация с persistent volumes |

---

## 🧠 Алгоритм интервального повторения (SM-2)

Обучение построено на полноценной реализации алгоритма **SuperMemo 2** с персонализацией:

| Параметр | Описание |
|----------|----------|
| **EaseFactor** | Коэффициент лёгкости (≥ 1.3, начальное 2.5). Определяет скорость роста интервалов |
| **IntervalDays** | Текущий интервал повторения в днях |
| **KnowledgeLevel** | Количество успешных повторений (repetition count) |

**Формула SM-2:**
```
EF' = EF + (0.1 − (5 − q) × (0.08 + (5 − q) × 0.02))

Интервалы:
  n = 1  →  1 день
  n = 2  →  6 дней
  n > 2  →  I(n-1) × EF
```

**Маппинг качества ответа:**
| Кнопка | SM-2 (q) | Эффект на EaseFactor | Интервал |
|--------|----------|---------------------|----------|
| 🔴 Again | q=1 | EF ↓↓ (−0.54) | Сброс → 10 мин |
| 🟡 Hard | q=3 | EF ↓ (−0.14) | I(n) по формуле |
| 🟢 Good | q=4 | EF ≈ (−0.04) | I(n) по формуле |
| 🔵 Easy | q=5 | EF ↑ (+0.10) | I(n) × 1.3 бонус |

> **Ключевое отличие от упрощённого алгоритма:** Hard теперь снижает EaseFactor — сложные слова будут повторяться чаще. Интервалы не имеют потолка и растут адаптивно для каждого слова.

---

## 🃏 Режимы тренировки

Веб-приложение поддерживает **8 режимов** обучения с возможностью микширования:

| Режим | Описание |
|-------|----------|
| 🃏 **Flashcard** | Классические карточки с 4 кнопками оценки (Again / Hard / Good / Easy) |
| 🔤 **MCQ** | Выбор из 4 вариантов ответа |
| ⌨️ **Typing** | Ввод перевода с клавиатуры (с обнаружением «почти верно» через Левенштейна) |
| 👂 **Listening** | Прослушивание слова + выбор перевода на слух |
| 🔗 **Matching** | Соединение слов с переводами в парах (5 слов за раунд) |
| 📝 **Cloze** | Заполнение пропуска в контекстном предложении |
| 🐝 **Spelling Bee** | Ввод слова по буквам после прослушивания (мобильно-адаптивный) |
| 🎲 **Mixed** | Случайное чередование всех активных режимов |

### Дополнительные механики

| Механика | Описание |
|----------|----------|
| ⚡ **Speed Round** | Режим на скорость: 60 сек, авто-показ ответа, подсчёт очков |
| 🎯 **Daily Goal** | Настраиваемая дневная цель (по умолчанию 20 слов) с прогресс-баром |
| 🏅 **Daily Challenge** | Ежедневные испытания с бонусным XP |
| 🐛 **Leech Detection** | Слова с 4+ сбросами автоматически помечаются; AI предлагает мнемоники |
| 🍅 **Pomodoro Break** | Автоматические перерывы после каждых 25 слов |
| ⭐ **Mastery Indicator** | Визуальный индикатор уровня освоения (звёзды) для каждого слова |
| 📝 **User Notes** | Персональные заметки/мнемоники к словам, доступные во время тренировки |
| 🚀 **Starter Pack** | Автоматический стартовый словарь для новых пользователей |

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
│   │   ├── Layout/             # MainLayout, NavMenu, AuthStatus, TrainingReminder
│   │   ├── Shared/             # CommentsSection, ShareDialog, AppToast
│   │   └── Pages/              # 16 страниц:
│   │       ├── Home             # Landing page
│   │       ├── Dictionaries     # Каталог словарей
│   │       ├── DictionaryDetails # Детали словаря + предпросмотр слов
│   │       ├── Rules            # Каталог правил
│   │       ├── RuleDetails      # Детали правила + упражнения
│   │       ├── MyRuleDetails    # Редактирование своего правила
│   │       ├── Profile          # Публичный профиль пользователя
│   │       ├── MyContent        # Личный кабинет контента
│   │       ├── Training         # 8 режимов тренировки + Speed Round
│   │       ├── Statistics       # Статистика, достижения, XP
│   │       ├── Classroom        # Управление классом (Teacher/Student)
│   │       ├── AiDictionary     # AI-генерация словарей
│   │       └── Login/Register   # Аутентификация
│   ├── Services/               # AuthService, ContentApiService, TrainingApiService,
│   │                           # StatisticsApiService, ClassroomApiService, AiApiService
│   └── wwwroot/css/            # Современные CSS стили
├── 🔧 LearningAPI/             # REST API Backend
│   ├── Controllers/            # 18 контроллеров
│   │   ├── AuthController      # JWT аутентификация
│   │   ├── DictionaryController # CRUD словарей
│   │   ├── RuleController      # CRUD правил
│   │   ├── WordController      # CRUD слов
│   │   ├── TrainingController  # Сессии тренировок, Daily Plan
│   │   ├── MarketplaceController # Маркетплейс
│   │   ├── ProgressController  # Прогресс обучения (SM-2)
│   │   ├── StatisticsController # Статистика, достижения, XP
│   │   ├── UserProfileController # Публичные профили
│   │   ├── SharingController   # Шаринг контента
│   │   ├── ClassroomController # Управление классом
│   │   ├── AdminUsersController # Админ-панель
│   │   ├── TokenController     # Refresh токены
│   │   ├── HealthController    # Health checks + метрики
│   │   └── Import/Export       # JSON/CSV
│   └── Middleware/             # Exception handling
├── 🤖 Ingat.AI/                # AI-микросервис
│   └── Controllers/            # Генерация словарей и мнемоник
├── 📦 LearningTrainerShared/   # Общая библиотека
│   ├── Models/                 # Entities, DTOs, Features
│   ├── Context/                # EF DbContext + миграции
│   └── Services/               # TokenService
├── 🧪 LearningAPI.Tests/       # Unit-тесты (229 тестов)
│   ├── Controllers/            # 15 тестовых классов
│   ├── Services/               # TokenServiceTests, PasswordValidatorTests
│   ├── Models/                 # EntityTests
│   └── Helpers/                # TestDbContextFactory, TestDataSeeder
├── 📈 StressTestClient/        # Нагрузочное тестирование
├── 🔊 setup_piper.ps1          # Установка Piper TTS + голосовых моделей
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
| 🌐 Web App | http://localhost:8081 |
| 🗄️ SQL Server | localhost:14333 |
| 📦 Redis | localhost:6379 |

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

# 6. (Опционально) Установить Piper TTS для озвучки слов
cd ..
.\setup_piper.ps1
```

---

## 🔊 Озвучка слов (Piper TTS)

WPF-клиент использует **Piper** — локальный нейро-TTS движок на ONNX Runtime. Работает **полностью офлайн**, без API-ключей и серверов.

### Установка (один раз)

```powershell
# Из корня репозитория
.\setup_piper.ps1
```

Скрипт скачает в `%LOCALAPPDATA%\LearningTrainer\piper\`:

| Компонент | Размер | Описание |
|-----------|--------|----------|
| `piper.exe` | ~22 MB | Движок синтеза речи |
| `en_US-lessac-medium` | ~60 MB | Английский нейро-голос |
| `ru_RU-irina-medium` | ~60 MB | Русский нейро-голос |

### Особенности

- ⚡ Синтез за ~40 мс (real-time factor 0.04x)
- 🎚️ Регулировка громкости в настройках (0–100%)
- 🎧 Поддержка Bluetooth-наушников (silent padding для пробуждения кодека)
- 🔇 Без интернета, без ключей, без подписок

---

## 🛠️ Технологии

| Слой | Стек |
|------|------|
| **Desktop** | WPF, MVVM, WebView2, LiveCharts2, Piper TTS (ONNX) |
| **Web** | Blazor Server, Bootstrap 5, CSS3 (Custom Properties, Gradients, Animations) |
| **Backend** | ASP.NET Core 8.0, EF Core 8.0, MediatR |
| **AI** | Ingat.AI микросервис (ASP.NET Core), LLM-интеграция |
| **Database** | SQL Server 2022, SQLite (offline), Redis (кэширование) |
| **Auth** | JWT Bearer Tokens, BCrypt, Refresh Tokens, ASP.NET Data Protection |
| **Container** | Docker, Docker Compose, Persistent Volumes |
| **Markdown** | Markdig |
| **Tests** | xUnit, FluentAssertions, Moq |

---

## 📊 API Endpoints

### Аутентификация
| Method | Endpoint | Описание |
|--------|----------|----------|
| POST | `/api/auth/login` | Вход (Username или Email) |
| POST | `/api/auth/register` | Регистрация (Username, Email, Password) |
| POST | `/api/auth/refresh` | Обновление токена |
| POST | `/api/auth/change-password` | Смена пароля |

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
| GET | `/api/dictionaries/{id}/review` | Сессия обучения |
| GET | `/api/training/daily-plan` | Дневной план (review + new + difficult) |
| GET | `/api/training/words` | Слова для тренировки (фильтр по режиму) |
| POST | `/api/training/starter-pack` | Стартовый словарь для новых пользователей |
| POST | `/api/progress/update` | Обновить прогресс (SM-2 + время ответа + тип упражнения) |
| GET | `/api/progress/stats` | Статистика пользователя |

### Импорт/Экспорт
| Method | Endpoint | Описание |
|--------|----------|----------|
| POST | `/api/dictionaries/import/json` | Импорт из JSON |
| POST | `/api/dictionaries/import/json/auto` | Импорт из JSON (автоопределение полей) |
| POST | `/api/dictionaries/import/csv` | Импорт из CSV |
| POST | `/api/dictionaries/import/anki` | Импорт из Anki (.apkg) |
| GET | `/api/dictionaries/export/{id}/json` | Экспорт в JSON |
| GET | `/api/dictionaries/export/{id}/csv` | Экспорт в CSV |

### Профили пользователей
| Method | Endpoint | Описание |
|--------|----------|----------|
| GET | `/api/users/{id}/profile` | Публичный профиль (без авторизации) |

### Класс (Teacher → Students)
| Method | Endpoint | Описание |
|--------|----------|----------|
| GET | `/api/classroom/invite-code` | Получить код приглашения |
| POST | `/api/classroom/join` | Присоединиться к классу |
| GET | `/api/classroom/students` | Список учеников |
| POST | `/api/sharing/dictionary` | Поделиться словарём |
| POST | `/api/sharing/rule` | Поделиться правилом |
| POST | `/api/auth/upgrade-to-teacher` | Стать учителем |

---

## 🧪 Тестирование

### Unit-тесты

```bash
# Запуск всех тестов
dotnet test LearningAPI.Tests

# Запуск с подробным выводом
dotnet test LearningAPI.Tests --logger "console;verbosity=detailed"
```

### Покрытие тестами

| Категория | Тестов | Статус |
|-----------|--------|--------|
| **Controllers** | 175+ | ✅ |
| **Services** | 20+ | ✅ |
| **Models** | 20+ | ✅ |
| **Helpers** | 15+ | ✅ |
| **Всего** | **229** | ✅ 100% |

Тестируемые контроллеры:
- `AuthController` - регистрация, вход, JWT токены, смена пароля
- `DictionaryController` - CRUD словарей, сессии обучения
- `RuleController` - CRUD правил, Markdown
- `TrainingController` - сессии тренировок, Daily Plan, Starter Pack
- `MarketplaceController` - публикация, скачивание, комментарии
- `ProgressController` - прогресс изучения, SM-2, статистика
- `SharingController` - шаринг контента ученикам
- `ClassroomController` - управление классом
- `AdminUsersController` - управление пользователями
- `TokenController` - refresh tokens
- `HealthController` - health checks, метрики
- `ImportController` / `ExportController` - JSON/CSV
- `UserProfileController` - публичные профили пользователей
- `PasswordValidator` - валидация паролей, оценка сложности

### 🚀 Нагрузочное тестирование

Проект включает фазовый стресс-тест (`StressTestClient`), имитирующий реалистичное поведение пользователей с нарастающей нагрузкой.

```bash
cd StressTestClient
dotnet run
```

**Конфигурация теста:**
- 150 виртуальных пользователей
- 4 фазы: разогрев → нарастание → пиковые всплески → устойчивая нагрузка
- 17 типов действий (CRUD словарей, тренировки, маркетплейс, статистика)
- Длительность: 90 секунд

**Результаты (локальный запуск, .NET 8, SQL Server 2022):**

| Метрика | Значение |
|---------|----------|
| 📊 Всего запросов | **64 657** |
| ⚡ Пропускная способность | **716 RPS** |
| ✅ Успешных | **98.9%** |
| ⏱️ Среднее время ответа | **109 мс** |
| 📈 Медиана | **51 мс** |
| 📦 Данных передано | **482 MB** |
| 🔀 Throughput | **5.4 MB/s** |

**Перцентили времени ответа:**

| P50 | P90 | P95 | P99 | Max |
|-----|-----|-----|-----|-----|
| 51 мс | 268 мс | 406 мс | 798 мс | 2.8 с |

**Производительность по эндпоинтам (топ-5 по нагрузке):**

| Эндпоинт | Запросов | Среднее | P95 |
|----------|----------|---------|-----|
| GetDictionaries | 7 816 | 209 мс | 604 мс |
| MarketplaceDicts | 6 526 | 28 мс | 70 мс |
| GetRules | 6 496 | 45 мс | 145 мс |
| MarketplaceRules | 5 127 | 27 мс | 69 мс |
| TrainingDailyPlan | 5 044 | 175 мс | 489 мс |

> **Оценка: A (Отлично)** — API стабильно обслуживает 150 одновременных пользователей
> с пропускной способностью 716 запросов в секунду при 98.9% успешности.

#### Оптимизации, применённые по результатам тестирования

- **SQL roundtrip reduction** — `GetDictionaries`: 4 → 2 roundtrip (подзапросы вместо отдельных запросов, проекция `WordCount` через `d.Words.Count`)
- **DailyPlan агрегация** — 8 → 5 SQL roundtrip (объединение COUNT-запросов в один `GroupBy`)
- **Response projection** — контроллер возвращает только метаданные без массива Words

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

### 🌐 Web-приложение
- Современный Landing Page с градиентным Hero
- Каталог словарей и правил с hover-эффектами
- Детальные страницы с комментариями и кликабельными аватарками авторов
- Публичные профили пользователей — достижения (19 шт., 5 уровней редкости), streak, опубликованный контент с пагинацией
- Предпросмотр слов словаря в карточном стиле с анимацией появления
- **8 режимов тренировки:** Flashcard, MCQ, Typing, Listening, Matching, Cloze, Spelling Bee, Mixed
- Speed Round с таймером, Daily Challenge, Pomodoro-перерывы
- Leech-менеджер с AI-мнемониками, заметки к словам
- AI-генерация словарей через Ingat.AI
- XP-система, детальная статистика, достижения
- Управление классом (Teacher → Student), шаринг контента
- Адаптивная вёрстка для мобильных, планшетов и десктопа

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

MIT License © 2026

---

<div align="center">

Made with ❤️ using 

![.NET](https://img.shields.io/badge/.NET-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![Blazor](https://img.shields.io/badge/Blazor-512BD4?style=flat-square&logo=blazor&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-0078D4?style=flat-square&logo=windows&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-DC382D?style=flat-square&logo=redis&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=flat-square&logo=docker&logoColor=white)

**🌍 Learn any language you want! Share with the community!**

</div>
