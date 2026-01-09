# ?? Документация новых функций (Фаза 4)

## ?? Обзор

В проекте реализованы **4 крупные функции**:

1. **Health Check Endpoint** ?
2. **Refresh Token механизм** ?
3. **Экспорт/Импорт словарей** ?
4. **RBAC (Role-Based Access Control)** ?

---

## ? Функция 1: Health Check Endpoint

### Назначение
Проверка состояния API и его зависимостей (БД, память, диск).

### Endpoints

#### Базовая проверка
```
GET /api/health
```

**Ответ (Healthy)**:
```json
{
  "status": "Healthy",
  "timestamp": "2026-01-09T12:34:56.789Z",
  "services": {
    "database": "Healthy",
    "memory": "150 MB",
    "diskSpace": "150 GB available"
  }
}
```

#### Расширенная проверка
```
GET /api/health/detailed
```

**Ответ**:
```json
{
  "status": "Healthy",
  "timestamp": "2026-01-09T12:34:56.789Z",
  "version": "1.0.0.0",
  "environment": "Production",
  "database": {
    "status": "Healthy",
    "message": "Connected",
    "metrics": {
      "users": 150,
      "dictionaries": 500
    }
  },
  "system": {
    "upTime": "02:15:30",
    "processors": 8,
    "memoryUsageMB": 150,
    "dotNetVersion": ".NET 8.0"
  }
}
```

### Статус коды
- `200 OK` - Healthy или Degraded
- `503 Service Unavailable` - Unhealthy

### Использование
```bash
# Мониторинг
curl http://localhost:5077/api/health

# Детальная информация
curl http://localhost:5077/api/health/detailed
```

---

## ? Функция 2: Refresh Token механизм

### Назначение
Автоматическое обновление access token без переввода пароля.

### Как это работает

1. **При логине** пользователь получает:
   - `AccessToken` (2 часа)
   - `RefreshToken` (7 дней)

2. **Когда AccessToken истекает**, клиент использует RefreshToken

3. **Refresh возвращает новую пару** токенов

### Endpoints

#### Логин (возвращает токены)
```
POST /api/auth/login
Content-Type: application/json

{
  "username": "teacher@example.com",
  "password": "password123"
}
```

**Ответ**:
```json
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "base64string...",
  "tokenType": "Bearer",
  "expiresIn": 7200,
  "userLogin": "teacher@example.com",
  "userRole": "Teacher",
  "userId": 1,
  "inviteCode": "TR-ABC123"
}
```

#### Обновить Access Token
```
POST /api/token/refresh
Content-Type: application/json

{
  "refreshToken": "base64string..."
}
```

**Ответ**:
```json
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "newbase64string...",
  "expiresIn": 7200,
  "tokenType": "Bearer"
}
```

#### Отозвать Refresh Token (Logout)
```
POST /api/token/revoke
Authorization: Bearer <accessToken>
Content-Type: application/json

{
  "refreshToken": "base64string..."
}
```

#### Отозвать все Refresh Tokens (выход из всех устройств)
```
POST /api/token/revoke-all
Authorization: Bearer <accessToken>
```

### Конфигурация

В `appsettings.json`:
```json
{
  "Jwt": {
    "Key": "...",
    "Issuer": "EnglishLearningAPI",
    "Audience": "WpfClient",
    "ExpiresHours": "2",
    "RefreshTokenExpiryDays": "7"
  }
}
```

### Использование в WPF

```csharp
// 1. Логин
var response = await _apiService.LoginAsync(credentials);
var accessToken = response.AccessToken;
var refreshToken = response.RefreshToken;

// 2. Сохранить токены локально
_localStorage.SetToken("accessToken", accessToken);
_localStorage.SetToken("refreshToken", refreshToken);

// 3. Когда AccessToken истекает
var newTokens = await _apiService.RefreshTokenAsync(refreshToken);
_localStorage.SetToken("accessToken", newTokens.AccessToken);
```

---

## ? Функция 3: Экспорт/Импорт словарей

### Назначение
Резервное копирование и обмен словарями между пользователями.

### Endpoints

#### Экспорт в JSON
```
GET /api/dictionaries/export/{dictionaryId}/json
Authorization: Bearer <accessToken>
```

**Ответ**: Файл `dictionary_export_20260109_123456.json`

#### Экспорт в CSV
```
GET /api/dictionaries/export/{dictionaryId}/csv
Authorization: Bearer <accessToken>
```

**Ответ**: Файл `dictionary_export_20260109_123456.csv`

#### Экспорт все словари в ZIP
```
GET /api/dictionaries/export/all/zip
Authorization: Bearer <accessToken>
```

**Ответ**: Файл `dictionaries_export_20260109_123456.zip`

#### Импорт из JSON
```
POST /api/dictionaries/import/json
Authorization: Bearer <accessToken>
Content-Type: multipart/form-data

Form-data:
  file: <файл JSON>
```

**Ответ**:
```json
{
  "message": "Dictionary imported successfully",
  "dictionaryId": 123,
  "name": "English Vocabulary",
  "wordCount": 150
}
```

#### Импорт из CSV
```
POST /api/dictionaries/import/csv
Authorization: Bearer <accessToken>
Content-Type: multipart/form-data

Form-data:
  file: <файл CSV>
  dictionaryName: "Business English"
  languageFrom: "English"
  languageTo: "Russian"
```

### Форматы

#### JSON формат
```json
{
  "name": "English Vocabulary",
  "description": "Common words",
  "languageFrom": "English",
  "languageTo": "Russian",
  "exportDate": "2026-01-09T12:34:56.789Z",
  "words": [
    {
      "original": "hello",
      "translation": "привет",
      "partOfSpeech": "noun",
      "example": "Hello world"
    }
  ]
}
```

#### CSV формат
```
Original,Translation,Part of Speech,Example
hello,привет,noun,Hello world
world,мир,noun,Hello world
```

---

## ? Функция 4: RBAC (Role-Based Access Control)

### Роли

| Роль | Описание | Права |
|------|---------|-------|
| **Admin** | Администратор | Управление пользователями, все операции |
| **Teacher** | Учитель | Создание словарей, делиться с студентами |
| **Student** | Студент | Просмотр общих словарей, обучение |

### Защита endpoints

#### Создание словаря (только учителя)
```
POST /api/dictionaries
Authorization: Bearer <accessToken>
```
Требует: `Role = "Teacher"`

#### Делиться словарем (только учителя)
```
POST /api/sharing/dictionary/toggle
Authorization: Bearer <accessToken>
```
Требует: `Role = "Teacher"`

### Admin endpoints

#### Получить всех пользователей
```
GET /api/admin/users?page=1&pageSize=10
Authorization: Bearer <adminToken>
```

**Ответ**:
```json
{
  "data": [
    {
      "id": 1,
      "login": "teacher@example.com",
      "role": "Teacher",
      "createdAt": "2026-01-01T00:00:00Z"
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 10,
    "total": 100,
    "pageCount": 10
  }
}
```

#### Изменить роль пользователя
```
PUT /api/admin/users/{userId}/role
Authorization: Bearer <adminToken>
Content-Type: application/json

{
  "newRole": "Teacher"
}
```

#### Удалить пользователя
```
DELETE /api/admin/users/{userId}
Authorization: Bearer <adminToken>
```

#### Получить статистику
```
GET /api/admin/users/statistics
Authorization: Bearer <adminToken>
```

**Ответ**:
```json
{
  "totalUsers": 500,
  "byRole": {
    "admins": 2,
    "teachers": 50,
    "students": 448
  }
}
```

### Использование в коде

```csharp
[Authorize(Roles = "Teacher,Admin")]
public async Task<IActionResult> CreateDictionary(...)
{
    // Только учителя и админы могут создавать словари
}

[Authorize(Roles = "Admin")]
public async Task<IActionResult> GetAllUsers()
{
    // Только админы могут получать список пользователей
}
```

---

## ?? Сводка новых функций

| Функция | Endpoints | Статус |
|---------|-----------|--------|
| Health Check | 2 endpoints | ? Готово |
| Refresh Token | 3 endpoints | ? Готово |
| Export/Import | 5 endpoints | ? Готово |
| RBAC | 5 endpoints | ? Готово |
| **Всего** | **15 endpoints** | **? Готово** |

---

## ?? Конфигурация

Все параметры хранятся в `appsettings.json`:

```json
{
  "Jwt": {
    "Key": "4d9b4ec83f469d8b02e1cdb17745350b15cb532c5441be4ea1ca5d4f54881678",
    "Issuer": "EnglishLearningAPI",
    "Audience": "WpfClient",
    "ExpiresHours": "2",
    "RefreshTokenExpiryDays": "7"
  }
}
```

---

## ?? Примечания

1. **Access Token** - короткий токен (2 часа) для API запросов
2. **Refresh Token** - долгий токен (7 дней) для получения нового Access Token
3. **Export** - создает копию словаря в выбранном формате
4. **RBAC** - контроль доступа на основе ролей пользователя

---

**Версия:** 1.0  
**Последнее обновление:** 2026-01-09
