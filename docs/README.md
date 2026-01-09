# 📚 Документация LearningTrainer

## 📑 Содержание

### 📖 ТЕХНИЧЕСКАЯ ДОКУМЕНТАЦИЯ
- **[PROJECT_ANALYSIS.md](PROJECT_ANALYSIS.md)** — Полный анализ проекта, архитектура, технологии
- **[TECHNICAL_SUMMARY.md](TECHNICAL_SUMMARY.md)** — Техническое описание, стек, endpoints
- **[QUICKSTART.md](QUICKSTART.md)** — Быстрый старт разработки

### 🔧 РУКОВОДСТВА
- **[FIXING_GUIDE.md](FIXING_GUIDE.md)** — Руководство по исправлению критических ошибок
- **[FIXES_REPORT.md](FIXES_REPORT.md)** — Отчёт об исправленных ошибках

### 💡 РАЗВИТИЕ
- **[FEATURES_RECOMMENDATIONS.md](FEATURES_RECOMMENDATIONS.md)** — Идеи и рекомендации для новых функций

---

## 🚀 Быстрые ссылки

### Основные функции системы

| Функция | Описание |
|---------|----------|
| 🔐 JWT Authentication | Аутентификация с Access + Refresh токенами |
| 👥 RBAC | Роли: Admin, Teacher, Student |
| 📚 Словари | Создание, редактирование, шаринг между пользователями |
| 📝 Правила | Markdown-правила грамматики |
| 🎓 Обучение | Интервальное повторение (SRS) |
| 📊 Статистика | Прогресс обучения, графики активности |
| 🎨 Темы | Светлая, тёмная, Forest, Dracula |
| 🌍 Локализация | Русский, English |

### API Endpoints

| Категория | Примеры |
|-----------|---------|
| Auth | `POST /api/auth/login`, `POST /api/auth/register` |
| Dictionaries | `GET /api/dictionaries`, `POST /api/dictionaries` |
| Words | `GET /api/words`, `POST /api/words` |
| Rules | `GET /api/rules`, `POST /api/rules` |
| Progress | `POST /api/progress/update`, `GET /api/progress/stats` |
| Sharing | `POST /api/sharing/dictionary/toggle` |
| Health | `GET /api/health`, `GET /api/health/detailed` |

---

## 📊 Структура проекта

```
CourseWork/
├── LearningTrainer/          # WPF Desktop клиент
│   ├── Views/                # XAML представления
│   ├── ViewModels/           # MVVM ViewModels
│   ├── Services/             # Бизнес-логика
│   └── Resources/            # Темы, локализация
├── LearningAPI/              # REST API Backend
│   ├── Controllers/          # API контроллеры
│   └── Services/             # Сервисы
├── LearningTrainerShared/    # Общие модели и логика
│   ├── Models/               # Entity модели
│   └── Context/              # DbContext
├── StressTestClient/         # Нагрузочное тестирование
└── docs/                     # Документация (вы здесь)
```

---

## 🛠 Технологический стек

| Компонент | Технология |
|-----------|-----------|
| Runtime | .NET 8 |
| Desktop | WPF (MVVM) |
| API | ASP.NET Core |
| ORM | Entity Framework Core 9 |
| DB (API) | SQL Server |
| DB (Client) | SQLite |
| Cache | Redis |
| Auth | JWT Bearer |
| Charts | LiveChartsCore + SkiaSharp |

---

## 🎯 Как начать

1. Прочитайте **[QUICKSTART.md](QUICKSTART.md)** (5 минут)
2. Изучите **[TECHNICAL_SUMMARY.md](TECHNICAL_SUMMARY.md)** для понимания архитектуры
3. Смотрите **[PROJECT_ANALYSIS.md](PROJECT_ANALYSIS.md)** для полного обзора
