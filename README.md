# 📚 Документация LearningTrainer

## 📑 Содержание

### 📖 ТЕХНИЧЕСКАЯ ДОКУМЕНТАЦИЯ
- **[PROJECT_ANALYSIS.md](PROJECT_ANALYSIS.md)** — Полный анализ проекта, архитектура, технологии
- **[TECHNICAL_SUMMARY.md](TECHNICAL_SUMMARY.md)** — Техническое описание, стек, endpoints
- **[QUICKSTART.md](QUICKSTART.md)** — Быстрый старт разработки
- **[SOLUTION_CARD.md](SOLUTION_CARD.md)** — Карточка решения

### 🔧 РУКОВОДСТВА
- **[FIXING_GUIDE.md](FIXING_GUIDE.md)** — Руководство по исправлению критических ошибок
- **[FIXES_REPORT.md](FIXES_REPORT.md)** — Отчёт об исправленных ошибках
- **[IMPLEMENTATION_COMPLETE.md](IMPLEMENTATION_COMPLETE.md)** — Отчёт о реализации

### 💡 РАЗВИТИЕ
- **[FEATURES_RECOMMENDATIONS.md](FEATURES_RECOMMENDATIONS.md)** — Идеи и рекомендации для новых функций

---

## 🚀 Основные функции системы

| Функция | Описание |
|---------|----------|
| 🔐 JWT Authentication | Аутентификация с Access токенами |
| 👥 RBAC | Роли: User, Admin, Teacher, Student |
| 📚 Словари | Создание, редактирование, импорт/экспорт, sharing |
| 📝 Правила | Markdown-правила грамматики с live-preview |
| 🎓 Обучение | Интервальное повторение (SM-2 алгоритм) |
| 📊 Статистика | Прогресс обучения, графики активности |
| 🎨 Темы | Light, Dark, Forest, Dracula |
| 🌍 Локализация | Русский, English, Español, Deutsch, 中文 |
| 🔔 Уведомления | Toast-notifications для всех действий |
| 📤 Sharing | Учитель → Студент для словарей и правил |

---

## 📊 API Endpoints

| Категория | Endpoints |
|-----------|-----------|
| Auth | `/api/auth/login`, `/api/auth/register`, `/api/auth/upgrade-to-teacher` |
| Dictionaries | `/api/dictionary` (GET, POST, PUT, DELETE) |
| Words | `/api/word` (GET, POST, DELETE) |
| Rules | `/api/rule` (GET, POST, PUT, DELETE) |
| Progress | `/api/progress/session/{id}`, `/api/progress/update`, `/api/progress/stats` |
| Sharing | `/api/sharing/dictionary/toggle`, `/api/sharing/rule/toggle` |
| Classroom | `/api/classroom/students` |
| Health | `/api/health` |

---

## 📁 Структура проекта

```
CourseWork/
├── LearningTrainer/          # WPF Desktop клиент
│   ├── Views/                # XAML представления
│   ├── ViewModels/           # MVVM ViewModels
│   ├── Services/             # API, Settings, Permissions
│   ├── Converters/           # XAML Converters
│   ├── Core/                 # Commands, EventAggregator
│   └── Resources/            # Темы, языки, иконки
├── LearningAPI/              # REST API Backend
│   ├── Controllers/          # API контроллеры
│   ├── Middleware/           # Exception handling
│   └── Services/             # TokenService
├── LearningTrainerShared/    # Общие модели
│   ├── Models/               # Entity модели, DTOs
│   ├── Context/              # ApiDbContext
│   └── Migrations/           # EF Migrations
├── LearningAPI.Tests/        # Unit-тесты (22 теста)
├── StressTestClient/         # Нагрузочное тестирование
└── docs/                     # Документация (вы здесь)
```

---

## 🛠 Технологический стек

| Компонент | Технология | Версия |
|-----------|-----------|--------|
| Runtime | .NET | 8.0 |
| Desktop | WPF (MVVM) | .NET 8 |
| API | ASP.NET Core | 8.0 |
| ORM | Entity Framework Core | 8.0 |
| Database | SQL Server | 2019+ |
| Auth | JWT Bearer | - |
| Markdown | Markdig + WebView2 | - |
| Charts | LiveChartsCore + SkiaSharp | - |
| Tests | xUnit | - |

---

## 🎯 Как начать

1. Прочитайте **[QUICKSTART.md](QUICKSTART.md)** (5 минут)
2. Изучите **[TECHNICAL_SUMMARY.md](TECHNICAL_SUMMARY.md)** для понимания архитектуры
3. Смотрите **[PROJECT_ANALYSIS.md](PROJECT_ANALYSIS.md)** для полного обзора

---

## ✅ Тестирование

```bash
dotnet test LearningAPI.Tests
```

**Результат:** 22 теста пройдено

---

## 📞 Контакты

**Студент:** Речицкий Александр Валентинович  
**Группа:** ИСПП-21  
**GitHub:** [Sugoman](https://github.com/Sugoman)
