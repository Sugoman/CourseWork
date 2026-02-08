using Ganss.Xss;
using LearningTrainerShared.Models;
using Markdig;
using Microsoft.Web.WebView2.Wpf;
using System.Diagnostics;
using System.Globalization;
using System.Windows;

namespace LearningTrainer.Behaviors
{
    public static class WebBrowserBehavior
    {
        private static readonly HtmlSanitizer _htmlSanitizer = CreateSanitizer();

        private static HtmlSanitizer CreateSanitizer()
        {
            var sanitizer = new HtmlSanitizer();

            // Разрешаем только безопасные теги для Markdown контента
            sanitizer.AllowedTags.Clear();
            sanitizer.AllowedTags.Add("h1");
            sanitizer.AllowedTags.Add("h2");
            sanitizer.AllowedTags.Add("h3");
            sanitizer.AllowedTags.Add("h4");
            sanitizer.AllowedTags.Add("h5");
            sanitizer.AllowedTags.Add("h6");
            sanitizer.AllowedTags.Add("p");
            sanitizer.AllowedTags.Add("br");
            sanitizer.AllowedTags.Add("hr");
            sanitizer.AllowedTags.Add("strong");
            sanitizer.AllowedTags.Add("b");
            sanitizer.AllowedTags.Add("em");
            sanitizer.AllowedTags.Add("i");
            sanitizer.AllowedTags.Add("u");
            sanitizer.AllowedTags.Add("code");
            sanitizer.AllowedTags.Add("pre");
            sanitizer.AllowedTags.Add("ul");
            sanitizer.AllowedTags.Add("ol");
            sanitizer.AllowedTags.Add("li");
            sanitizer.AllowedTags.Add("blockquote");
            sanitizer.AllowedTags.Add("table");
            sanitizer.AllowedTags.Add("thead");
            sanitizer.AllowedTags.Add("tbody");
            sanitizer.AllowedTags.Add("tr");
            sanitizer.AllowedTags.Add("th");
            sanitizer.AllowedTags.Add("td");
            sanitizer.AllowedTags.Add("a");

            // Разрешаем только безопасные атрибуты
            sanitizer.AllowedAttributes.Clear();
            sanitizer.AllowedAttributes.Add("href");
            sanitizer.AllowedAttributes.Add("class");

            // Разрешаем только безопасные схемы ссылок
            sanitizer.AllowedSchemes.Clear();
            sanitizer.AllowedSchemes.Add("http");
            sanitizer.AllowedSchemes.Add("https");

            return sanitizer;
        }

        public static readonly DependencyProperty MarkdownProperty =
            DependencyProperty.RegisterAttached("Markdown", typeof(string), typeof(WebBrowserBehavior),
            new PropertyMetadata(null, OnMarkdownChanged));

        public static readonly DependencyProperty HtmlContentProperty =
            DependencyProperty.RegisterAttached("HtmlContent", typeof(string), typeof(WebBrowserBehavior),
            new PropertyMetadata(null, OnHtmlContentChanged));

        public static readonly DependencyProperty ConfigProperty =
            DependencyProperty.RegisterAttached("Config", typeof(MarkdownConfig), typeof(WebBrowserBehavior),
            new PropertyMetadata(new MarkdownConfig(), OnConfigChanged));

        public static string GetMarkdown(DependencyObject obj) => (string)obj.GetValue(MarkdownProperty);
        public static void SetMarkdown(DependencyObject obj, string value) => obj.SetValue(MarkdownProperty, value);
        public static string GetHtmlContent(DependencyObject obj) => (string)obj.GetValue(HtmlContentProperty);
        public static void SetHtmlContent(DependencyObject obj, string value) => obj.SetValue(HtmlContentProperty, value);
        public static MarkdownConfig GetConfig(DependencyObject obj) => (MarkdownConfig)obj.GetValue(ConfigProperty);
        public static void SetConfig(DependencyObject obj, MarkdownConfig value) => obj.SetValue(ConfigProperty, value);

        private static async void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            await UpdateWebViewContent(d as WebView2);
        }

        private static async void OnHtmlContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            await UpdateWebViewFromHtml(d as WebView2);
        }

        private static async void OnConfigChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (GetMarkdown(d) != null)
                await UpdateWebViewContent(d as WebView2);
            else if (GetHtmlContent(d) != null)
                await UpdateWebViewFromHtml(d as WebView2);
        }

        private static async Task UpdateWebViewContent(WebView2 webView)
        {
            if (webView == null) return;

            try
            {
                if (webView.CoreWebView2 == null)
                {
                    await webView.EnsureCoreWebView2Async();
                    webView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
                }

                var markdown = GetMarkdown(webView);
                var config = GetConfig(webView) ?? new MarkdownConfig
                {
                    BackgroundColor = "#FFFFFF",
                    TextColor = "#333333",
                    AccentColor = "#0056b3",
                    FontSize = 16
                };

                var html = GenerateHtml(markdown, config);

                webView.NavigateToString(html);
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"[WebBrowserBehavior] Error updating WebView: {ex.Message}");
            }
        }

        private static async Task UpdateWebViewFromHtml(WebView2 webView)
        {
            if (webView == null) return;

            try
            {
                if (webView.CoreWebView2 == null)
                {
                    await webView.EnsureCoreWebView2Async();
                    webView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
                }

                var htmlBody = GetHtmlContent(webView);
                var config = GetConfig(webView) ?? new MarkdownConfig
                {
                    BackgroundColor = "#FFFFFF",
                    TextColor = "#333333",
                    AccentColor = "#0056b3",
                    FontSize = 16
                };

                var html = GenerateHtmlFromContent(htmlBody, config);
                webView.NavigateToString(html);
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"[WebBrowserBehavior] Error updating WebView from HTML: {ex.Message}");
            }
        }

        private static string HexToRgba(string hexColor, double opacity)
        {
            if (string.IsNullOrEmpty(hexColor)) return $"rgba(128, 128, 128, {opacity})";

            hexColor = hexColor.Replace("#", "");

            if (hexColor.Length == 6)
            {
                if (int.TryParse(hexColor.Substring(0, 2), NumberStyles.HexNumber, null, out int r) &&
                    int.TryParse(hexColor.Substring(2, 2), NumberStyles.HexNumber, null, out int g) &&
                    int.TryParse(hexColor.Substring(4, 2), NumberStyles.HexNumber, null, out int b))
                {
                    return $"rgba({r}, {g}, {b}, {opacity.ToString(CultureInfo.InvariantCulture)})";
                }
            }
            return $"rgba(128, 128, 128, {opacity})";
        }

        private static string GenerateHtml(string markdown, MarkdownConfig config)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                markdown = "_Content is empty_";
            }

            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var htmlContent = Markdig.Markdown.ToHtml(markdown, pipeline);

            // Санитизация HTML для защиты от XSS-атак
            htmlContent = _htmlSanitizer.Sanitize(htmlContent);

            string tableBorderColor = HexToRgba(config.TextColor, 0.2);
            string quoteBgColor = HexToRgba(config.AccentColor, 0.1);
            string scrollThumbColor = HexToRgba(config.TextColor, 0.3);

            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        :root {{
            --bg-color: {config.BackgroundColor};
            --text-color: {config.TextColor};
            --accent-color: {config.AccentColor};
            --font-size: {config.FontSize}px;
            
            /* Динамические цвета */
            --table-border-color: {tableBorderColor};
            --quote-bg-color: {quoteBgColor};
            --scroll-thumb-color: {scrollThumbColor};

            --code-color: #e06c75;
            --code-bg-color: #282c34; 
        }}

        body {{
            font-family: 'Segoe UI', sans-serif;
            background-color: var(--bg-color);
            color: var(--text-color);
            font-size: var(--font-size); 
            line-height: 1.6;
            margin: 0;
            padding: 20px;
            overflow-x: hidden;
            transition: background-color 0.3s ease, color 0.3s ease;
        }}
        
        a {{ color: var(--accent-color); text-decoration: none; font-weight: 500; }}
        a:hover {{ text-decoration: underline; }}
        
        blockquote {{
            margin: 1em 0;
            padding: 10px 20px;
            border-left: 4px solid var(--accent-color);
            background-color: var(--quote-bg-color); /* Теперь динамический! */
            color: var(--text-color);
            border-radius: 0 4px 4px 0;
        }}

        h1, h2, h3, h4, h5 {{ color: var(--text-color); margin-top: 1.5em; font-weight: 600; }}
        h1 {{ border-bottom: 1px solid var(--table-border-color); padding-bottom: 0.3em; }}

        table {{
            width: 100%;
            border-collapse: collapse;
            margin: 1.5em 0;
        }}
    
        th, td {{
            padding: 12px;
            text-align: left;
            border: 1px solid var(--table-border-color); /* Теперь видно везде! */
        }}

        th {{
            background-color: var(--quote-bg-color);
            color: var(--text-color);
            font-weight: bold;
        }}

        /* Код блоки */
        code {{
            background-color: var(--quote-bg-color); /* Чтобы не было черного блока на белом фоне */
            color: var(--accent-color);
            padding: 2px 5px;
            border-radius: 4px;
            font-family: 'Consolas', monospace;
            font-size: 0.9em;
        }}

        pre {{
            background-color: #282c34; /* Большие блоки кода оставляем темными (как в VS Code) */
            padding: 15px;
            border-radius: 6px;
            overflow-x: auto;
        }}
        
        pre code {{
            background-color: transparent;
            color: #abb2bf;
            padding: 0;
        }}
        
        /* Скроллбар */
        ::-webkit-scrollbar {{ width: 10px; height: 10px; }}
        ::-webkit-scrollbar-track {{ background: transparent; }}
        ::-webkit-scrollbar-thumb {{ background: var(--scroll-thumb-color); border-radius: 5px; }}
        ::-webkit-scrollbar-thumb:hover {{ background: var(--accent-color); }} 
    </style>
</head>
<body>
    {htmlContent}
</body>
</html>";
        }

        private static string GenerateHtmlFromContent(string htmlBody, MarkdownConfig config)
        {
            if (string.IsNullOrEmpty(htmlBody))
            {
                htmlBody = "<em>Content is empty</em>";
            }

            // Санитизация HTML для защиты от XSS-атак
            htmlBody = _htmlSanitizer.Sanitize(htmlBody);

            string tableBorderColor = HexToRgba(config.TextColor, 0.2);
            string quoteBgColor = HexToRgba(config.AccentColor, 0.1);
            string scrollThumbColor = HexToRgba(config.TextColor, 0.3);

            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        :root {{
            --bg-color: {config.BackgroundColor};
            --text-color: {config.TextColor};
            --accent-color: {config.AccentColor};
            --font-size: {config.FontSize}px;
            --table-border-color: {tableBorderColor};
            --quote-bg-color: {quoteBgColor};
            --scroll-thumb-color: {scrollThumbColor};
        }}
        body {{
            font-family: 'Segoe UI', sans-serif;
            background-color: var(--bg-color);
            color: var(--text-color);
            font-size: var(--font-size);
            line-height: 1.6;
            margin: 0;
            padding: 20px;
            overflow-x: hidden;
        }}
        a {{ color: var(--accent-color); text-decoration: none; font-weight: 500; }}
        a:hover {{ text-decoration: underline; }}
        blockquote {{
            margin: 1em 0; padding: 10px 20px;
            border-left: 4px solid var(--accent-color);
            background-color: var(--quote-bg-color);
            color: var(--text-color); border-radius: 0 4px 4px 0;
        }}
        h1, h2, h3, h4, h5 {{ color: var(--text-color); margin-top: 1.5em; font-weight: 600; }}
        h1 {{ border-bottom: 1px solid var(--table-border-color); padding-bottom: 0.3em; }}
        table {{ width: 100%; border-collapse: collapse; margin: 1.5em 0; }}
        th, td {{ padding: 12px; text-align: left; border: 1px solid var(--table-border-color); }}
        th {{ background-color: var(--quote-bg-color); color: var(--text-color); font-weight: bold; }}
        code {{
            background-color: var(--quote-bg-color); color: var(--accent-color);
            padding: 2px 5px; border-radius: 4px;
            font-family: 'Consolas', monospace; font-size: 0.9em;
        }}
        pre {{ background-color: #282c34; padding: 15px; border-radius: 6px; overflow-x: auto; }}
        pre code {{ background-color: transparent; color: #abb2bf; padding: 0; }}
        ::-webkit-scrollbar {{ width: 10px; height: 10px; }}
        ::-webkit-scrollbar-track {{ background: transparent; }}
        ::-webkit-scrollbar-thumb {{ background: var(--scroll-thumb-color); border-radius: 5px; }}
        ::-webkit-scrollbar-thumb:hover {{ background: var(--accent-color); }}
    </style>
</head>
<body>
    {htmlBody}
</body>
</html>";
        }
    }
}
