// Developer: copilot
// Date: 2026-08-12
// Summary: Generic HTML web scraper — extracts title, meta description, headings, and links from any public webpage.

using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public class ScrapeResult
    {
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Headings { get; set; } = new List<string>();
        public List<(string Text, string Href)> Links { get; set; } = new List<(string, string)>();
        public string TextPreview { get; set; } = string.Empty;
    }

    public static class WebScraperManager
    {
        private static readonly HttpClient _client = new HttpClient();

        static WebScraperManager()
        {
            _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) JarvisLauncher/1.0");
        }

        public static async Task<ScrapeResult> ScrapePageAsync(string url)
        {
            if (!url.StartsWith("http://") && !url.StartsWith("https://")) url = "https://" + url;

            string html = await _client.GetStringAsync(url);
            var result = new ScrapeResult { Url = url };

            var titleMatch = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (titleMatch.Success) result.Title = CleanText(titleMatch.Groups[1].Value);

            var descMatch = Regex.Match(html, @"<meta\s+(?=[^>]*name=[""']description[""'])[^>]*content=[""']([^""']*)[""']", RegexOptions.IgnoreCase);
            if (!descMatch.Success)
                descMatch = Regex.Match(html, @"<meta\s+(?=[^>]*content=[""']([^""']*)[""'])[^>]*name=[""']description[""']", RegexOptions.IgnoreCase);
            if (descMatch.Success) result.Description = CleanText(descMatch.Groups[1].Value);

            foreach (Match m in Regex.Matches(html, @"<h[1-3][^>]*>(.*?)</h[1-3]>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                string text = CleanText(StripTags(m.Groups[1].Value));
                if (!string.IsNullOrWhiteSpace(text)) result.Headings.Add(text);
                if (result.Headings.Count >= 25) break;
            }

            foreach (Match m in Regex.Matches(html, @"<a\s+[^>]*href=[""']([^""'#][^""']*)[""'][^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                string href = m.Groups[1].Value.Trim();
                string text = CleanText(StripTags(m.Groups[2].Value));
                if (string.IsNullOrWhiteSpace(href) || href.StartsWith("javascript:")) continue;
                if (string.IsNullOrWhiteSpace(text)) text = href;
                result.Links.Add((text, href));
                if (result.Links.Count >= 60) break;
            }

            string textOnly = CleanText(StripTags(Regex.Replace(html, @"<script.*?</script>|<style.*?</style>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline)));
            result.TextPreview = textOnly.Length > 2000 ? textOnly.Substring(0, 2000) : textOnly;

            return result;
        }

        public static string FormatReport(ScrapeResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=========================================================================================");
            sb.AppendLine($" WEB SCRAPE REPORT: {r.Url}");
            sb.AppendLine("=========================================================================================");
            sb.AppendLine($"Title:       {r.Title}");
            if (!string.IsNullOrWhiteSpace(r.Description)) sb.AppendLine($"Description: {r.Description}");
            sb.AppendLine();

            if (r.Headings.Count > 0)
            {
                sb.AppendLine($"--- Headings ({r.Headings.Count}) ---");
                foreach (var h in r.Headings) sb.AppendLine($"  • {h}");
                sb.AppendLine();
            }

            if (r.Links.Count > 0)
            {
                sb.AppendLine($"--- Links ({r.Links.Count}) ---");
                foreach (var (text, href) in r.Links) sb.AppendLine($"  {text,-40} -> {href}");
                sb.AppendLine();
            }

            sb.AppendLine("--- Text Preview ---");
            sb.AppendLine(r.TextPreview);
            sb.AppendLine("=========================================================================================");
            return sb.ToString();
        }

        private static string StripTags(string html) => Regex.Replace(html, "<.*?>", " ");

        private static string CleanText(string text)
        {
            text = System.Net.WebUtility.HtmlDecode(text);
            return Regex.Replace(text, @"\s+", " ").Trim();
        }
    }
}
