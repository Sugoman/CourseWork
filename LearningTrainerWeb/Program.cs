using LearningTrainerWeb.Components;
using LearningTrainerWeb.Services;
using Microsoft.AspNetCore.DataProtection;
using System.Text;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Increase SignalR max message size for large rule content (Markdown up to 50 KB)
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 512 * 1024; // 512 KB
});

// Data Protection: persist keys so encrypted sessions survive container restarts
var keysPath = builder.Configuration["DataProtection:KeysPath"] ?? "/app/keys";
builder.Services.AddDataProtection()
    .SetApplicationName("LearningTrainerWeb")
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5077";
var aiBaseUrl = builder.Configuration["AiService:BaseUrl"] ?? "http://localhost:5200";

// Централизованное управление токеном (scoped = один на Blazor circuit)
builder.Services.AddScoped<AuthTokenProvider>();

// Регистрируем typed HttpClient через IHttpClientFactory
// Примечание: DelegatingHandler НЕ используется, т.к. IHttpClientFactory создаёт хэндлеры
// вне DI-скоупа Blazor circuit, и scoped AuthTokenProvider будет другим экземпляром.
// Вместо этого сервисы сами устанавливают заголовок из AuthTokenProvider перед каждым запросом.
builder.Services.AddHttpClient<IContentApiService, ContentApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<IAuthService, AuthService>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<ITrainingApiService, TrainingApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<IStatisticsApiService, StatisticsApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<IClassroomApiService, ClassroomApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<IGrammarApiService, GrammarApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<IKnowledgeTreeApiService, KnowledgeTreeApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddSingleton<IHtmlSanitizerService, HtmlSanitizerService>();
builder.Services.AddScoped<ITrainingReminderService, TrainingReminderService>();

// AI-сервис — обращается напрямую к Ingat.AI
builder.Services.AddHttpClient<IAiApiService, AiApiService>(c =>
{
    c.BaseAddress = new Uri(aiBaseUrl);
    c.Timeout = TimeSpan.FromSeconds(120);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
