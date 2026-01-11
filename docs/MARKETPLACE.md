# ?? Маркетплейс контента

<div align="center">

*Делитесь знаниями с сообществом*

</div>

---

## ?? Обзор

Маркетплейс LearningTrainer — платформа для обмена учебными материалами:
- ?? Публикуйте словари и правила
- ?? Скачивайте материалы других пользователей
- ? Оценивайте и комментируйте контент
- ?? Находите лучшие материалы по рейтингу

---

## ?? Возможности

### Для авторов
| Функция | Описание |
|---------|----------|
| ?? **Публикация** | Одним кликом в WPF или через "Мой контент" в Web |
| ?? **Статистика** | Количество скачиваний, средний рейтинг |
| ?? **Обратная связь** | Комментарии и отзывы пользователей |
| ?? **Управление** | Снятие с публикации в любой момент |

### Для пользователей
| Функция | Описание |
|---------|----------|
| ?? **Поиск** | По ключевым словам, названию, автору |
| ??? **Фильтрация** | По языку, категории, уровню сложности |
| ? **Оценки** | От 1 до 5 звёзд |
| ?? **Комментарии** | Текстовые отзывы |
| ?? **Скачивание** | Добавление в личную библиотеку |

---

## ?? Веб-интерфейс

### Страницы маркетплейса

| Путь | Описание | Авторизация |
|------|----------|-------------|
| `/` | Landing Page с популярным контентом | Нет |
| `/dictionaries` | Каталог словарей с поиском | Нет |
| `/rules` | Каталог правил с фильтрами | Нет |
| `/dictionary/{id}` | Детальная страница словаря | Нет |
| `/rule/{id}` | Детальная страница правила | Нет |
| `/my-content` | Личный кабинет, управление публикациями | Да |

### Дизайн карточек

```css
/* Современный стиль карточек */
.popular-card {
    background: white;
    border-radius: 20px;
    box-shadow: 0 4px 20px -2px rgba(0,0,0,0.08);
    transition: all 0.3s ease;
}

.popular-card:hover {
    transform: translateY(-6px);
    box-shadow: 0 20px 40px -8px rgba(99,102,241,0.15);
}
```

### Адаптивность

| Размер экрана | Поведение |
|---------------|-----------|
| Desktop (992px+) | 3 колонки карточек, горизонтальное меню |
| Tablet (768-991px) | 2 колонки, уменьшенные отступы |
| Mobile (< 768px) | 1 колонка, вертикальные кнопки |
| iPhone SE (375px) | Полноширинная статистика, компактный hero |

---

## ?? API Endpoints

### Публичный контент (без авторизации)

```http
# Словари
GET /api/marketplace/dictionaries?search=english&page=1&pageSize=8
GET /api/marketplace/dictionaries/{id}
GET /api/marketplace/dictionaries/{id}/comments

# Правила
GET /api/marketplace/rules?category=Grammar&difficulty=2&page=1&pageSize=8
GET /api/marketplace/rules/{id}
GET /api/marketplace/rules/{id}/comments
GET /api/marketplace/rules/{id}/related?category=Grammar
```

### Действия пользователя (JWT required)

```http
# Скачивание
POST /api/marketplace/dictionaries/{id}/download
POST /api/marketplace/rules/{id}/download

# Комментарии
POST /api/marketplace/dictionaries/{id}/comments
Body: { "rating": 5, "text": "Отличный словарь!" }

POST /api/marketplace/rules/{id}/comments
Body: { "rating": 4, "text": "Хорошее объяснение" }

# Публикация (только владелец)
POST /api/marketplace/dictionaries/{id}/publish
POST /api/marketplace/dictionaries/{id}/unpublish
POST /api/marketplace/rules/{id}/publish
POST /api/marketplace/rules/{id}/unpublish
```

### Личный контент (JWT required)

```http
GET /api/marketplace/my/dictionaries   # Мои словари
GET /api/marketplace/my/rules          # Мои правила
GET /api/marketplace/my/downloads      # Скачанный контент
```

---

## ?? Модели данных

### Расширенные поля для маркетплейса

```csharp
public class Dictionary
{
    // Стандартные поля...
    
    // Маркетплейс
    public bool IsPublished { get; set; }
    public double Rating { get; set; }
    public int RatingCount { get; set; }
    public int DownloadCount { get; set; }
    public int? SourceDictionaryId { get; set; } // Если скачан
}

public class Rule
{
    // Стандартные поля...
    
    // Маркетплейс
    public bool IsPublished { get; set; }
    public double Rating { get; set; }
    public int RatingCount { get; set; }
    public int DownloadCount { get; set; }
    public int? SourceRuleId { get; set; }
}
```

### Комментарий

```csharp
public class Comment
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string ContentType { get; set; } // "Dictionary" | "Rule"
    public int ContentId { get; set; }
    public int Rating { get; set; } // 1-5 звёзд
    public string Text { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Navigation
    public User User { get; set; }
}
```

### История скачиваний

```csharp
public class Download
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string ContentType { get; set; }
    public int ContentId { get; set; }
    public DateTime DownloadedAt { get; set; }
}
```

---

## ?? Авторизация

| Действие | Требования |
|----------|------------|
| Просмотр каталога | ? Без авторизации |
| Просмотр деталей | ? Без авторизации |
| Просмотр комментариев | ? Без авторизации |
| Скачивание | ? JWT токен |
| Добавление комментария | ? JWT токен |
| Публикация/снятие | ? JWT токен + владелец |

---

## ?? Алгоритм рейтинга

При добавлении комментария автоматически пересчитывается средний рейтинг:

```csharp
private async Task UpdateRating(string contentType, int contentId)
{
    var comments = await _context.Comments
        .Where(c => c.ContentType == contentType && c.ContentId == contentId)
        .ToListAsync();

    if (!comments.Any()) return;

    var averageRating = comments.Average(c => c.Rating);
    var count = comments.Count;

    if (contentType == "Dictionary")
    {
        var dict = await _context.Dictionaries.FindAsync(contentId);
        dict.Rating = averageRating;
        dict.RatingCount = count;
    }
    else
    {
        var rule = await _context.Rules.FindAsync(contentId);
        rule.Rating = averageRating;
        rule.RatingCount = count;
    }

    await _context.SaveChangesAsync();
}
```

---

## ??? WPF интеграция

### Публикация из десктоп-клиента

1. Откройте словарь или правило для редактирования
2. Нажмите кнопку **"Опубликовать на сайте"** (зелёная)
3. После успешной публикации кнопка станет **"Снять с публикации"** (оранжевая)
4. Контент появится в веб-маркетплейсе

### Код ViewModel

```csharp
private async Task PublishToMarketplace()
{
    try
    {
        await _apiService.PublishDictionaryAsync(Dictionary.Id);
        Dictionary.IsPublished = true;
        ShowNotification("Словарь опубликован!");
    }
    catch (Exception ex)
    {
        ShowError($"Ошибка: {ex.Message}");
    }
}
```

---

## ?? UI компоненты (Blazor)

### Карточка контента

```razor
<div class="popular-card">
    <div class="popular-card-header">
        <span class="popular-type type-dictionary">
            <i class="bi bi-book-half"></i> Словарь
        </span>
        <div class="popular-rating">
            <i class="bi bi-star-fill"></i>
            <span>4.8</span>
        </div>
    </div>
    <h4 class="popular-title">Business English</h4>
    <div class="popular-author">
        <div class="author-avatar"><i class="bi bi-person"></i></div>
        <span>Teacher123</span>
    </div>
    <div class="popular-card-footer">
        <span class="popular-downloads">
            <i class="bi bi-download"></i> 156
        </span>
        <a href="dictionary/1" class="btn btn-sm btn-card">Подробнее</a>
    </div>
</div>
```

### Форма комментария

```razor
<div class="mb-3">
    <label class="form-label">Оценка</label>
    <select class="form-select" @bind="NewRating">
        <option value="5">????? Отлично</option>
        <option value="4">???? Хорошо</option>
        <option value="3">??? Нормально</option>
        <option value="2">?? Плохо</option>
        <option value="1">? Ужасно</option>
    </select>
</div>
<div class="mb-3">
    <textarea class="form-control" @bind="NewComment" 
              placeholder="Поделитесь мнением..."></textarea>
</div>
<button class="btn btn-primary" @onclick="SubmitComment">
    <i class="bi bi-send me-1"></i> Отправить
</button>
```

---

## ?? Метрики

После запуска можно отслеживать:

| Метрика | Описание |
|---------|----------|
| Топ авторов | Пользователи с наибольшим числом скачиваний |
| Популярный контент | Словари/правила с высоким рейтингом |
| Активность | Количество новых публикаций за период |
| Скачивания | Общее число скачиваний за период |
