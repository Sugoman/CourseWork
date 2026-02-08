# 📚 Документация LearningTrainer

<div align="center">

*Полная документация платформы для изучения иностранных языков*

</div>

---

## 📑 Содержание

### 🚀 НАЧАЛО РАБОТЫ
| Документ | Описание |
|----------|----------|
| **[QUICKSTART.md](QUICKSTART.md)** | Быстрый старт за 5 минут |
| **[SOLUTION_CARD.md](SOLUTION_CARD.md)** | Карточка решения |

### 📖 ТЕХНИЧЕСКАЯ ДОКУМЕНТАЦИЯ
| Документ | Описание |
|----------|----------|
| **[PROJECT_ANALYSIS.md](PROJECT_ANALYSIS.md)** | Полный анализ проекта, архитектура |
| **[TECHNICAL_SUMMARY.md](TECHNICAL_SUMMARY.md)** | Стек технологий, API endpoints, статистика кода |

### 🛒 МАРКЕТПЛЕЙС
| Документ | Описание |
|----------|----------|
| **[MARKETPLACE.md](MARKETPLACE.md)** | API маркетплейса, модели данных, интеграция |

### 🔧 РУКОВОДСТВА
| Документ | Описание |
|----------|----------|
| **[FIXING_GUIDE.md](FIXING_GUIDE.md)** | Руководство по исправлению ошибок |
| **[FIXES_REPORT.md](FIXES_REPORT.md)** | Отчёт об исправленных ошибках |
| **[IMPLEMENTATION_COMPLETE.md](IMPLEMENTATION_COMPLETE.md)** | Отчёт о реализации |

### 💡 РАЗВИТИЕ
| Документ | Описание |
|----------|----------|
| **[FEATURES_RECOMMENDATIONS.md](FEATURES_RECOMMENDATIONS.md)** | Идеи для новых функций |
| **[EXPERIMENTS.md](EXPERIMENTS.md)** | Экспериментальные функции, метрики |

---

## 🎯 Архитектура системы

```
┌─────────────────────────────────────────────────────────────┐
│                        КЛИЕНТЫ                               │
├─────────────────────────────────────────────────────────────┤
│  🖥️ WPF Desktop          │  🌐 Blazor Web                   │
│  ├── MVVM Pattern        │  ├── Components/Pages            │
│  ├── Views & ViewModels  │  ├── Modern CSS (Glassmorphism)  │
│  ├── Services Layer      │  ├── AuthService                 │
│  └── SQLite (offline)    │  └── ContentApiService           │
├─────────────────────────────────────────────────────────────┤
│                      🔧 REST API                             │
│  ├── ASP.NET Core 8.0                                       │
│  ├── JWT Authentication                                      │
│  ├── MediatR (CQRS)                                          │
│  └── Controllers: Auth, Marketplace, Progress, Sharing       │
├─────────────────────────────────────────────────────────────┤
│                      📦 SHARED                               │
│  ├── Entity Models                                           │
│  ├── DTOs & Requests                                         │
│  └── EF Core DbContext                                       │
├─────────────────────────────────────────────────────────────┤
│                    🗄️ SQL SERVER                             │
│  └── Docker Container (port 14333)                           │
└─────────────────────────────────────────────────────────────┘
```

---

## 🌟 Основные функции

| Функция | Описание | Платформа |
|---------|----------|-----------|
| 🔐 **JWT Authentication** | Access/Refresh токены | API |
| 👥 **RBAC** | User, Admin, Teacher, Student | Все |
| 📚 **Словари** | Создание, редактирование, импорт/экспорт | WPF, Web |
| 📝 **Правила** | Markdown с live-preview | WPF, Web |
| 🎓 **Обучение** | SM-2 алгоритм, интервальное повторение | WPF |
| 📊 **Статистика** | Графики, streak, прогресс | WPF |
| 🛒 **Маркетплейс** | Публикация, скачивание, рейтинги | Web |
| 🎨 **Темы** | Light, Dark, Forest, Dracula | WPF |
| 🌍 **Локализация** | RU, EN, ES, DE, ZH | WPF |
| 📤 **Sharing** | Учитель → Студенты | WPF |

---

## 📊 Ключевые API Endpoints

### Аутентификация
```http
POST /api/auth/login              # Вход (Username или Email)
POST /api/auth/register           # Регистрация (Username, Email, Password)
POST /api/auth/refresh            # Обновление токена
POST /api/auth/upgrade-to-teacher # Стать учителем
POST /api/auth/change-password    # Смена пароля
```

### Маркетплейс
```http
GET  /api/marketplace/dictionaries         # Публичные словари
GET  /api/marketplace/rules                # Публичные правила
GET  /api/marketplace/dictionaries/{id}    # Детали словаря
POST /api/marketplace/{type}/{id}/download # Скачать
POST /api/marketplace/{type}/{id}/publish  # Опубликовать
POST /api/marketplace/{type}/{id}/comments # Добавить отзыв
```

### Контент пользователя
```http
GET/POST /api/dictionaries    # Словари
GET/POST /api/rules           # Правила
GET/POST /api/words           # Слова в словаре
```

### Обучение
```http
GET  /api/progress/session/{id}  # Сессия обучения
POST /api/progress/update        # Обновить прогресс
GET  /api/progress/stats         # Статистика
```

---

## 🎨 Веб-интерфейс

Blazor Web-приложение построено с использованием современного дизайна:

### Дизайн-система
- **Шрифты:** Inter (body), Poppins (headings)
- **Цвета:** Indigo/Violet градиент (`#6366f1` → `#a855f7`)
- **Карточки:** `border-radius: 20px`, многослойные тени
- **Анимации:** `translateY` при hover, плавные transitions

### Компоненты
| Страница | Путь | Описание |
|----------|------|----------|
| Landing | `/` | Hero + Features + Popular + CTA |
| Словари | `/dictionaries` | Каталог с поиском и фильтрами |
| Правила | `/rules` | Каталог правил |
| Детали | `/dictionary/{id}`, `/rule/{id}` | Страница с комментариями |
| Мой контент | `/my-content` | Личный кабинет |
| Авторизация | `/login`, `/register` | Формы входа |

### Адаптивность
- **Desktop:** горизонтальное меню, 3 колонки карточек
- **Tablet:** 2 колонки, уменьшенные отступы
- **Mobile (iPhone SE):** 1 колонка, вертикальные кнопки, полноширинная статистика

---

## 📁 Структура проекта

```
CourseWork/
├── LearningTrainer/          # 🖥️ WPF Desktop
│   ├── Views/                # XAML представления
│   ├── ViewModels/           # MVVM ViewModels
│   ├── Services/             # API, Settings, Permissions
│   └── Resources/            # Темы, языки, иконки
├── LearningTrainerWeb/       # 🌐 Blazor Server
│   ├── Components/
│   │   ├── Layout/           # MainLayout, NavMenu, AuthStatus
│   │   └── Pages/            # Home, Dictionaries, Rules, etc.
│   ├── Services/             # AuthService, ContentApiService
│   └── wwwroot/css/          # app.css (современные стили)
├── LearningAPI/              # 🔧 REST API
│   ├── Controllers/          # API контроллеры
│   └── Middleware/           # Exception handling
├── LearningTrainerShared/    # 📦 Shared Library
│   ├── Models/               # Entities, DTOs
│   └── Context/              # EF DbContext
├── LearningAPI.Tests/        # 🧪 Unit Tests (173+)
├── StressTestClient/         # 📈 Load Testing
└── docs/                     # 📄 Документация
```

---

## 🧪 Тестирование

```bash
# Unit тесты
dotnet test LearningAPI.Tests

# Результат: ✅ 173+ тестов пройдено
```

---

## 🔗 Ссылки

- **GitHub:** https://github.com/Sugoman/CourseWork
- **Swagger:** http://localhost:5077/swagger (после запуска)
- **Web App:** http://localhost:5078 (после запуска)
