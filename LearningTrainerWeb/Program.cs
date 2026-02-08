using LearningTrainerWeb.Components;
using LearningTrainerWeb.Services;
using System.Text;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5077";

// Регистрируем общий HttpClient
builder.Services.AddScoped(sp => 
{
    var client = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
    return client;
});

// Регистрируем сервисы
builder.Services.AddScoped<IContentApiService, ContentApiService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITrainingApiService, TrainingApiService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

// Content-Security-Policy: блокирует inline-скрипты для защиты от XSS
app.Use(async (context, next) =>
{
    context.Response.Headers.Append(
        "Content-Security-Policy",
        "default-src 'self'; script-src 'self' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; connect-src 'self' ws: wss: http: https:; font-src 'self' https:;");
    await next();
});

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
