# 📚 LearningTrainer

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-Desktop-0078D4?style=for-the-badge&logo=windows&logoColor=white)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL_Server-2019+-CC2927?style=for-the-badge&logo=microsoftsqlserver&logoColor=white)

> 🌍 Универсальное приложение для изучения иностранных языков с интервальным повторением

![Build Status](https://img.shields.io/badge/build-passing-brightgreen?style=flat-square)
![Tests](https://img.shields.io/badge/tests-22%20passed-success?style=flat-square)
![License](https://img.shields.io/badge/license-MIT-blue?style=flat-square)

---

## 📖 О проекте

**LearningTrainer** — десктопное WPF-приложение для изучения иностранных языков с использованием алгоритма интервального повторения SM-2. Поддерживает любые языковые пары (английский, немецкий, французский, японский и др.). Включает REST API на ASP.NET Core и систему ролей (Учитель/Ученик).

### 🌐 Поддерживаемые языки для изучения

Приложение не ограничено конкретными языками — вы можете создавать словари для любой языковой пары:

| Примеры | Описание |
|---------|----------|
| 🇬🇧 English → 🇷🇺 Русский | Английский для русскоговорящих |
| 🇩🇪 Deutsch → 🇬🇧 English | Немецкий для англоговорящих |
| 🇯🇵 日本語 → 🇷🇺 Русский | Японский для русскоговорящих |
| 🇫🇷 Français → 🇪🇸 Español | Французский для испаноговорящих |
| 🇨🇳 中文 → 🇩🇪 Deutsch | Китайский для немецкоговорящих |

### ✨ Ключевые возможности

| Функция | Описание |
|---------|----------|
| 🔐 **Аутентификация** | JWT токены, регистрация с кодом учителя |
| 👥 **Роли** | User, Teacher, Student, Admin |
| 📚 **Словари** | Создание для любых языковых пар, импорт/экспорт JSON |
| 📝 **Правила** | Markdown-редактор с live-preview для грамматики любого языка |
| 🎓 **Обучение** | Интервальное повторение (SM-2 алгоритм) |
| 📊 **Статистика** | Графики прогресса, streak |
| 🔤 **Транскрипция** | Поддержка IPA, ромадзи, пиньинь и др. |
| 🎨 **Темы** | Light, Dark, Dracula, Forest |
| 🌍 **Интерфейс** | EN, RU, ES, DE, ZH |
| 🔔 **Уведомления** | Toast-notifications |
| 📤 **Sharing** | Учитель → Ученики |

---

## 📁 Структура проекта

```
CourseWork/
├── LearningTrainer/           # 🖥️ WPF Desktop клиент
│   ├── Views/                 # XAML представления
│   ├── ViewModels/            # MVVM ViewModels
│   ├── Services/              # API, Settings, Permissions
│   └── Resources/             # Темы, языки интерфейса, иконки
├── LearningAPI/               # 🌐 REST API Backend
│   ├── Controllers/           # API контроллеры
│   └── Middleware/            # Exception handling
├── LearningTrainerShared/     # 📦 Общая библиотека
│   ├── Models/                # Entity модели, DTOs
│   └── Context/               # EF DbContext
├── LearningAPI.Tests/         # 🧪 Unit-тесты (22)
├── StressTestClient/          # 📈 Нагрузочное тестирование
└── docs/                      # 📄 Документация
```

---

## 🚀 Быстрый старт

### Требования

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server 2019+ (или Docker)
- Visual Studio 2022 / JetBrains Rider

### Запуск

```bash
# 1. Клонировать репозиторий
git clone https://github.com/Sugoman/CourseWork.git
cd CourseWork

# 2. Применить миграции
cd LearningAPI
dotnet ef database update

# 3. Запустить API
dotnet run

# 4. Запустить WPF клиент (в новом терминале)
cd ../LearningTrainer
dotnet run
```

### Docker (альтернатива)

```bash
docker-compose up
```

API доступен на `http://localhost:5077/swagger/index.html`

---

## 🛠️ Технологии

| Слой | Технология |
|------|------------|
| **Frontend** | WPF, MVVM, WebView2, LiveCharts2 |
| **Backend** | ASP.NET Core 8.0, EF Core 8.0 |
| **Database** | SQL Server 2019+ |
| **Auth** | JWT Bearer Tokens |
| **Markdown** | Markdig |
| **Tests** | xUnit, Moq |

---

## ⚙️ Настройки приложения

| Секция | Параметры |
|--------|-----------|
| **General** | Daily Goal, Sound Effects, Show Transcription, Interface Language |
| **Appearance** | Theme, Font Family, Font Size, Animations |
| **Notifications** | Enable, Duration |
| **Account** | Keep Logged In, Auto Sync, Password |

---

## 📊 API Endpoints

| Категория | Endpoints |
|-----------|-----------|
| **Auth** | `POST /api/auth/login`, `POST /api/auth/register` |
| **Dictionaries** | `GET/POST/PUT/DELETE /api/dictionary` |
| **Words** | `GET/POST/DELETE /api/word` |
| **Rules** | `GET/POST/PUT/DELETE /api/rule` |
| **Progress** | `GET /api/progress/session/{id}`, `POST /api/progress/update` |
| **Sharing** | `POST /api/sharing/dictionary/toggle` |
| **Health** | `GET /api/health` |

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
| [docs/EXPERIMENTS.md](docs/EXPERIMENTS.md) | 🧪 Эксперименты (с бейджами) |
| [docs/TECHNICAL_SUMMARY.md](docs/TECHNICAL_SUMMARY.md) | 🔬 Техническое описание |

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

Made with ❤️ using ![.NET](https://img.shields.io/badge/.NET-512BD4?style=flat-square&logo=dotnet&logoColor=white) and ![WPF](https://img.shields.io/badge/WPF-0078D4?style=flat-square&logo=windows&logoColor=white)

**🌍 Learn any language you want!**

</div>
