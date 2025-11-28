using LearningTrainerShared.Models;
using Markdig;
using Microsoft.Web.WebView2.Wpf;
using System.Diagnostics;
using System.Windows;

namespace LearningTrainer.Behaviors
{
    public static class WebBrowserBehavior
    {
        public static readonly DependencyProperty MarkdownProperty =
            DependencyProperty.RegisterAttached("Markdown", typeof(string), typeof(WebBrowserBehavior), new PropertyMetadata(null, OnChanged));

        public static readonly DependencyProperty ConfigProperty =
            DependencyProperty.RegisterAttached("Config", typeof(MarkdownConfig), typeof(WebBrowserBehavior), new PropertyMetadata(new MarkdownConfig(), OnChanged));

        public static string GetMarkdown(DependencyObject obj) => (string)obj.GetValue(MarkdownProperty);
        public static void SetMarkdown(DependencyObject obj, string value) => obj.SetValue(MarkdownProperty, value);
        public static MarkdownConfig GetConfig(DependencyObject obj) => (MarkdownConfig)obj.GetValue(ConfigProperty);
        public static void SetConfig(DependencyObject obj, MarkdownConfig value) => obj.SetValue(ConfigProperty, value);

        private static async void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WebView2 webView)
            {
                Debug.WriteLine($"[WebBrowserBehavior] Property Changed: {e.Property.Name}");
                if (e.Property.Name == "Config" && e.NewValue is MarkdownConfig cfg)
                {
                    Debug.WriteLine($"[WebBrowserBehavior] New Config: BG={cfg.BackgroundColor}, Text={cfg.TextColor}, Accent={cfg.AccentColor}");
                }

                if (webView.CoreWebView2 == null)
                {
                    await webView.EnsureCoreWebView2Async();
                    webView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
                }

                var markdown = GetMarkdown(webView);
                var config = GetConfig(webView) ?? new MarkdownConfig();

                var html = GenerateHtml(markdown, config);

                webView.NavigateToString(html);
            }
        }

        private static string GenerateHtml(string markdown, MarkdownConfig config)
        {
            if (string.IsNullOrEmpty(markdown)) return "";

            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var htmlContent = Markdig.Markdown.ToHtml(markdown, pipeline);

            return $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8'>
                <style>
                    :root {{
                        --bg-color: {config.BackgroundColor};
                        --text-color: {config.TextColor};
                        --accent-color: {config.AccentColor};
                        --font-size: {config.FontSize}px;
                        --table-border-color: rgba(0, 0, 0, 0.2);
                        --paragraph-color: {config.TextColor};
                        --code-color: #e06c75;
                        --code-background-color: #282c34;
                    }}

                    body {{
                        font-family: 'Segoe UI', sans-serif;
                        background-color: var(--bg-color);
                        color: var(--text-color);
                        font-size: var(--font-size); 
                        line-height: 1.6;
                        margin: 0;
                        padding: 0 20px 20px 20px;
                        overflow-x: hidden;
                        transition: background-color 0.3s ease, color 0.3s ease;
                    }}
                    
                    a {{ color: #bf94e4; text-decoration: none; }}
                    
                    blockquote {{
                        margin: 1em 0;
                        padding-left: 20px;
                        border-left: 4px solid var(--accent-color);
                        background-color: rgba(255,255,255, 0.05);
                        color: var(--paragraph-color);
                        padding: 10px 20px;
                    }}

                    h1, h2, h3 {{ color: var(--accent-color); margin-top: 1.5em; }}
                    table {{
                        width: 100%;
                        border-collapse: collapse; /* Убирает двойные линии */
                        margin: 1.5em 0; /* Отступ сверху и снизу */
                    }}
                
                    th, td {{
                        padding: 10px;
                        text-align: left;
                        /* Основная фишка: добавляем границу! */
                        border: 1px solid var(--table-border-color); 
                    }}

                    th {{
                        background-color: rgba(255, 255, 255, 0.08); /* Чуть темнее фон для заголовков */
                        color: var(--accent-color);
                        font-weight: bold;
                    }}

                    code {{
                        background-color: var(--code-background-color);
                        padding: 2px 4px;
                        border-radius: 4px;
                        font-family: 'Consolas', monospace;
                        color: var(--code-color);
                    }}
                    
                    ::-webkit-scrollbar {{ width: 8px; }}
                    ::-webkit-scrollbar-track {{ background: var(--bg-color); }}
                    ::-webkit-scrollbar-thumb {{ background: #444; border-radius: 4px; }}
                    ::-webkit-scrollbar-thumb:hover {{ background: var(--accent-color); }} 
                </style>
            </head>
            <body>
                {htmlContent}
            </body>
            </html>";
        }
    }
}