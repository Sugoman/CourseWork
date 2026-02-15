using Ganss.Xss;

namespace LearningTrainerShared.Services
{
    /// <summary>
    /// Factory for creating a consistently configured HtmlSanitizer across all projects.
    /// </summary>
    public static class SharedSanitizerFactory
    {
        public static HtmlSanitizer Create()
        {
            var sanitizer = new HtmlSanitizer();

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

            sanitizer.AllowedAttributes.Clear();
            sanitizer.AllowedAttributes.Add("href");
            sanitizer.AllowedAttributes.Add("class");

            sanitizer.AllowedSchemes.Clear();
            sanitizer.AllowedSchemes.Add("http");
            sanitizer.AllowedSchemes.Add("https");

            return sanitizer;
        }
    }
}
