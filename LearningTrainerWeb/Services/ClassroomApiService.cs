using System.Net.Http.Json;

namespace LearningTrainerWeb.Services;

public interface IClassroomApiService
{
    Task<UpgradeToTeacherResult> UpgradeToTeacherAsync();
    Task<string> GetMyInviteCodeAsync();
    Task<JoinClassResult> JoinClassAsync(string code);
    Task<JoinClassResult> LeaveClassAsync();
    Task<SimpleResult> KickStudentAsync(int studentId);
    Task<TeacherInfo> GetMyTeacherAsync();
    Task<List<StudentInfo>> GetMyStudentsAsync();
    Task<List<int>> GetDictionarySharingStatusAsync(int dictionaryId);
    Task<List<int>> GetRuleSharingStatusAsync(int ruleId);
    Task<SharingResult> ToggleDictionarySharingAsync(int dictionaryId, int studentId);
    Task<SharingResult> ToggleRuleSharingAsync(int ruleId, int studentId);
}

public class ClassroomApiService : IClassroomApiService
{
    private readonly HttpClient _httpClient;
    private readonly AuthTokenProvider _tokenProvider;

    public ClassroomApiService(HttpClient httpClient, AuthTokenProvider tokenProvider)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
    }

    private async Task ApplyAuthAsync() => await _tokenProvider.EnsureValidTokenAsync(_httpClient);

    public async Task<UpgradeToTeacherResult> UpgradeToTeacherAsync()
    {
        await ApplyAuthAsync();
        var response = await _httpClient.PostAsync("api/auth/upgrade-to-teacher", null);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            return new UpgradeToTeacherResult { Success = false, Message = error };
        }

        var result = await response.Content.ReadFromJsonAsync<UpgradeToTeacherResponse>();
        return new UpgradeToTeacherResult
        {
            Success = true,
            Message = result?.Message ?? "Вы стали учителем!",
            InviteCode = result?.InviteCode ?? "",
            AccessToken = result?.AccessToken ?? "",
            UserRole = result?.UserRole ?? "Teacher"
        };
    }

    public async Task<string> GetMyInviteCodeAsync()
    {
        await ApplyAuthAsync();
        var result = await _httpClient.GetFromJsonAsync<InviteCodeResponse>("api/classroom/my-code");
        return result?.Code ?? "";
    }

    public async Task<JoinClassResult> JoinClassAsync(string code)
    {
        await ApplyAuthAsync();
        var response = await _httpClient.PostAsJsonAsync("api/classroom/join", new { Code = code });

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            return new JoinClassResult { Success = false, Message = error };
        }

        var result = await response.Content.ReadFromJsonAsync<JoinClassMessageResponse>();
        return new JoinClassResult
        {
            Success = true,
            Message = result?.Message ?? "Успешно!",
            AccessToken = result?.AccessToken ?? "",
            UserRole = result?.UserRole ?? ""
        };
    }

    public async Task<JoinClassResult> LeaveClassAsync()
    {
        await ApplyAuthAsync();
        var response = await _httpClient.PostAsync("api/classroom/leave", null);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            return new JoinClassResult { Success = false, Message = error };
        }

        var result = await response.Content.ReadFromJsonAsync<JoinClassMessageResponse>();
        return new JoinClassResult
        {
            Success = true,
            Message = result?.Message ?? "Вы вышли из класса.",
            AccessToken = result?.AccessToken ?? "",
            UserRole = result?.UserRole ?? ""
        };
    }

    public async Task<SimpleResult> KickStudentAsync(int studentId)
    {
        await ApplyAuthAsync();
        var response = await _httpClient.PostAsync($"api/classroom/kick/{studentId}", null);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            return new SimpleResult { Success = false, Message = error };
        }

        var result = await response.Content.ReadFromJsonAsync<SimpleMessageResponse>();
        return new SimpleResult { Success = true, Message = result?.Message ?? "Ученик удалён." };
    }

    public async Task<TeacherInfo> GetMyTeacherAsync()
    {
        await ApplyAuthAsync();
        var result = await _httpClient.GetFromJsonAsync<TeacherInfo>("api/classroom/my-teacher");
        return result ?? new();
    }

    public async Task<List<StudentInfo>> GetMyStudentsAsync()
    {
        await ApplyAuthAsync();
        var result = await _httpClient.GetFromJsonAsync<List<StudentInfo>>("api/classroom/students");
        return result ?? new();
    }

    public async Task<List<int>> GetDictionarySharingStatusAsync(int dictionaryId)
    {
        await ApplyAuthAsync();
        var result = await _httpClient.GetFromJsonAsync<List<int>>($"api/sharing/dictionary/{dictionaryId}/status");
        return result ?? new();
    }

    public async Task<List<int>> GetRuleSharingStatusAsync(int ruleId)
    {
        await ApplyAuthAsync();
        var result = await _httpClient.GetFromJsonAsync<List<int>>($"api/sharing/rule/{ruleId}/status");
        return result ?? new();
    }

    public async Task<SharingResult> ToggleDictionarySharingAsync(int dictionaryId, int studentId)
    {
        await ApplyAuthAsync();
        var response = await _httpClient.PostAsJsonAsync("api/sharing/dictionary/toggle",
            new { ContentId = dictionaryId, StudentId = studentId });

        if (!response.IsSuccessStatusCode)
            return new SharingResult { Success = false, Message = "Ошибка при изменении доступа" };

        var result = await response.Content.ReadFromJsonAsync<SharingToggleResponse>();
        return new SharingResult
        {
            Success = true,
            Message = result?.Message ?? "",
            Status = result?.Status ?? ""
        };
    }

    public async Task<SharingResult> ToggleRuleSharingAsync(int ruleId, int studentId)
    {
        await ApplyAuthAsync();
        var response = await _httpClient.PostAsJsonAsync("api/sharing/rule/toggle",
            new { ContentId = ruleId, StudentId = studentId });

        if (!response.IsSuccessStatusCode)
            return new SharingResult { Success = false, Message = "Ошибка при изменении доступа" };

        var result = await response.Content.ReadFromJsonAsync<SharingToggleResponse>();
        return new SharingResult
        {
            Success = true,
            Message = result?.Message ?? "",
            Status = result?.Status ?? ""
        };
    }
}

#region DTOs

public class UpgradeToTeacherResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string InviteCode { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string UserRole { get; set; } = "";
}

public class JoinClassResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string UserRole { get; set; } = "";
}

public class StudentInfo
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public int WordsLearned { get; set; }
    public int TotalWords { get; set; }
    public int CurrentStreak { get; set; }
    public DateTime? LastPracticeDate { get; set; }
    public int CorrectAnswers { get; set; }
    public int TotalAttempts { get; set; }
    public int SharedDictionariesCount { get; set; }
    public int SharedRulesCount { get; set; }

    public double AccuracyPercent => TotalAttempts > 0 ? (double)CorrectAnswers / TotalAttempts * 100 : 0;
    public int DaysSinceLastPractice => LastPracticeDate.HasValue
        ? (int)(DateTime.UtcNow.Date - LastPracticeDate.Value.Date).TotalDays
        : -1;
}

public class SharingResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string Status { get; set; } = "";
}

public class SimpleResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}

public class TeacherInfo
{
    public int? TeacherId { get; set; }
    public string? TeacherName { get; set; }
}

// Internal response DTOs for deserialization
internal class UpgradeToTeacherResponse
{
    public string? Message { get; set; }
    public string? InviteCode { get; set; }
    public string? AccessToken { get; set; }
    public string? UserRole { get; set; }
}

internal class InviteCodeResponse
{
    public string? Code { get; set; }
}

internal class JoinClassMessageResponse
{
    public string? Message { get; set; }
    public string? AccessToken { get; set; }
    public string? UserRole { get; set; }
}

internal class SharingToggleResponse
{
    public string? Message { get; set; }
    public string? Status { get; set; }
}

internal class SimpleMessageResponse
{
    public string? Message { get; set; }
}

#endregion
