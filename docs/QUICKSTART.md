# 🚀 Быстрый старт LearningTrainer

## ⏱️ За 5 минут

### 1. Клонирование и настройка

```bash
git clone https://github.com/Sugoman/CourseWork.git
cd CourseWork
```

### 2. Запуск API

```bash
cd LearningAPI
dotnet ef database update  # Применить миграции
dotnet run                 # Запуск на http://localhost:5077
```

### 3. Запуск WPF клиента

```bash
cd LearningTrainer
dotnet run
```

### 4. Тестирование

```bash
dotnet test LearningAPI.Tests
# Результат: 22 теста пройдено
```

---

## 📁 Структура документации

```
docs/
├── PROJECT_ANALYSIS.md         - Полный анализ проекта
├── TECHNICAL_SUMMARY.md        - Техническое описание
├── FIXING_GUIDE.md             - Руководство по исправлениям
├── FIXES_REPORT.md             - Отчёт об исправлениях
├── FEATURES_RECOMMENDATIONS.md - Рекомендации по функциям
├── SOLUTION_CARD.md            - Карточка решения
├── IMPLEMENTATION_COMPLETE.md  - Отчёт о реализации
└── README.md                   - Индекс документации
```

---

## 👥 Роли в системе

| Роль | Описание | Как получить |
|------|----------|--------------|
| **User** | Базовый пользователь | Регистрация без кода |
| **Teacher** | Учитель с учениками | Settings → "Become Teacher" |
| **Student** | Ученик учителя | Регистрация с invite-кодом |
| **Admin** | Полный доступ | Назначается в БД |

---

## 🔧 Требования

- **.NET 8.0 SDK**
- **SQL Server 2019+** (или Docker)
- **Visual Studio 2022** / **JetBrains Rider**

---

## 🐳 Docker (альтернатива)

```bash
docker-compose up
```

API доступен на `http://localhost:5077`

---

## 📚 Дальнейшее чтение

1. **[TECHNICAL_SUMMARY.md](TECHNICAL_SUMMARY.md)** — архитектура и endpoints
2. **[PROJECT_ANALYSIS.md](PROJECT_ANALYSIS.md)** — полный обзор
3. **[FEATURES_RECOMMENDATIONS.md](FEATURES_RECOMMENDATIONS.md)** — идеи развития

