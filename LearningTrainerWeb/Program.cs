using LearningTrainerWeb.Components;
using LearningTrainerWeb.Services;
using System.Text;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5077";

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
builder.Services.AddSingleton<IHtmlSanitizerService, HtmlSanitizerService>();

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
