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
builder.Services.AddScoped<StatisticsApiService>();
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
