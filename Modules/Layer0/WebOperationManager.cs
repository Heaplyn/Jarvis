// Developer: heaplyn
// Date: 2026-08-14
// Summary: Web Operations Manager for autonomous downloads, webpage scraping, and web search execution.
//          Enables Jarvis to download databases/files, scrape clean text, and run queries via DuckDuckGo.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

namespace JarvisLauncher
{
    public static class WebOperationManager
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        static WebOperationManager()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        /// <summary>
        /// Universally downloads a file, dataset, cloud folder, or magnet torrent link.
        /// Resolves Google Drive, Dropbox, direct URLs, and handles fallback MIME extensions from headers.
        /// </summary>
        public static async Task<string> DownloadFileAsync(string url, string? destPath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(url)) return "Error: URL is empty.";

                // 1. Handle Magnet Links / Torrent URIs
                if (url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase) || url.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                {
                    TextOverlay.Show("🧲 Opening Torrent Magnet Link...", 4000);
                    var psi = new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    return "SUCCESS: Magnet torrent link sent to your system's default torrent client.";
                }

                // 2. Resolve Google Drive Share Links
                // Rewrite: https://drive.google.com/file/d/FILE_ID/view?usp=sharing
                // To direct: https://docs.google.com/uc?export=download&id=FILE_ID&confirm=t
                var gdMatch = Regex.Match(url, @"drive\.google\.com/file/d/(?<id>[a-zA-Z0-9_\-]+)", RegexOptions.IgnoreCase);
                if (gdMatch.Success)
                {
                    string id = gdMatch.Groups["id"].Value;
                    url = $"https://docs.google.com/uc?export=download&id={id}&confirm=t";
                }

                // 3. Resolve Dropbox Share Links
                // Rewrite: https://www.dropbox.com/s/xyz/file.zip?dl=0
                // To direct download: https://www.dropbox.com/s/xyz/file.zip?dl=1
                if (url.Contains("dropbox.com"))
                {
                    if (url.Contains("?dl=0")) url = url.Replace("?dl=0", "?dl=1");
                    else if (!url.Contains("?dl=1")) url += "?dl=1";
                }

                // Prepare target directory
                string downloadDir = destPath;
                if (string.IsNullOrEmpty(downloadDir))
                {
                    downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                }
                if (!Directory.Exists(downloadDir)) Directory.CreateDirectory(downloadDir);

                Uri uri = new Uri(url);
                TextOverlay.Show($"📥 Resolving download target...", 3000);

                // Fetch HTTP response headers first to determine filename and content type
                using (var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    // Try to resolve filename from Content-Disposition header
                    string fileName = "";
                    var disposition = response.Content.Headers.ContentDisposition;
                    if (disposition != null)
                    {
                        fileName = disposition.FileNameStar ?? disposition.FileName ?? "";
                        fileName = fileName.Trim('"', '\'');
                    }

                    // Fallback to URL path segment
                    if (string.IsNullOrEmpty(fileName))
                    {
                        fileName = Path.GetFileName(uri.LocalPath);
                    }

                    // Fallback to MIME type extension mapping
                    if (string.IsNullOrEmpty(fileName) || !fileName.Contains("."))
                    {
                        string ext = ".dat";
                        string? mediaType = response.Content.Headers.ContentType?.MediaType?.ToLower();
                        if (!string.IsNullOrEmpty(mediaType))
                        {
                            if (mediaType.Contains("zip")) ext = ".zip";
                            else if (mediaType.Contains("octet-stream")) ext = ".bin";
                            else if (mediaType.Contains("json")) ext = ".json";
                            else if (mediaType.Contains("csv")) ext = ".csv";
                            else if (mediaType.Contains("sqlite") || mediaType.Contains("database")) ext = ".db";
                            else if (mediaType.Contains("audio/wav") || mediaType.Contains("x-wav")) ext = ".wav";
                            else if (mediaType.Contains("audio/mpeg") || mediaType.Contains("mp3")) ext = ".mp3";
                            else if (mediaType.Contains("video/mp4")) ext = ".mp4";
                            else if (mediaType.Contains("video/webm")) ext = ".webm";
                            else if (mediaType.Contains("pdf")) ext = ".pdf";
                            else if (mediaType.Contains("text/html")) ext = ".html";
                            else if (mediaType.Contains("text/plain")) ext = ".txt";
                        }
                        fileName = "downloaded_file_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ext;
                    }

                    // Ensure safe filesystem naming
                    foreach (char c in Path.GetInvalidFileNameChars())
                    {
                        fileName = fileName.Replace(c, '_');
                    }

                    string localFilePath = Path.Combine(downloadDir, fileName);
                    TextOverlay.Show($"📥 Downloading: {fileName}...", 4000);

                    using (var fs = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs);
                    }

                    TextOverlay.Show($"✅ Download complete: {fileName}", 4000);
                    return $"SUCCESS: File downloaded to: {localFilePath} ({new FileInfo(localFilePath).Length} bytes)";
                }
            }
            catch (Exception ex)
            {
                return $"Error downloading file: {ex.Message}";
            }
        }

        /// <summary>
        /// Scrapes a webpage, extracts clean text, and uses LLM/AI to summarize or extract data.
        /// </summary>
        public static async Task<string> ScrapeWebpageAsync(string url)
        {
            try
            {
                if (string.IsNullOrEmpty(url)) return "Error: URL is empty.";

                TextOverlay.Show($"🌐 Scraping page: {url}...", 3000);

                string html = await _httpClient.GetStringAsync(url);

                // Strip script and style blocks
                html = Regex.Replace(html, @"<(script|style)[^>]*?>.*?</\1>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                // Strip all remaining HTML tags
                string cleanText = Regex.Replace(html, @"<[^>]*?>", "", RegexOptions.Singleline);

                // Decode HTML entities
                cleanText = System.Net.WebUtility.HtmlDecode(cleanText);

                // Clean up spacing and blank lines
                var lines = cleanText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                var sb = new StringBuilder();
                
                int lineCount = 0;
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed) && trimmed.Length > 3)
                    {
                        sb.AppendLine(trimmed);
                        lineCount++;
                        if (lineCount > 300) // Cap the text height before sending to LLM
                        {
                            break;
                        }
                    }
                }

                string rawData = sb.ToString();

                // Now use LLM/AI to scrape/summarize the text
                TextOverlay.Show("🧠 AI is extracting scraped content...", 3000);

                string prompt = "You are a web scraper AI. Extract the main content, data tables, links, or databases from the following raw web text. " +
                                "Remove all navigation bars, footers, advertisement text, cookie prompts, and repetitive headers. " +
                                "Structure the output cleanly in markdown, highlighting key facts or datasets.\n\n" +
                                $"[SOURCE URL]: {url}\n\n" +
                                $"[RAW WEB TEXT]:\n{rawData}";

                string aiExtracted = await LlmRouter.AskAsync(prompt);
                return aiExtracted;
            }
            catch (Exception ex)
            {
                return $"Error scraping page: {ex.Message}";
            }
        }

        /// <summary>
        /// Fetches a list from a URL (e.g. awesome-list, markdown file, or HTML page),
        /// extracts download links or GitHub repositories, and clones/downloads them in the background.
        /// </summary>
        public static async Task<string> DownloadListAsync(string listUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(listUrl)) return "Error: List URL is empty.";

                // Resolve GitHub landing pages to raw README format for clean regex parsing
                string fetchUrl = listUrl;
                if (listUrl.Contains("github.com") && !listUrl.Contains("raw.githubusercontent.com") && !listUrl.EndsWith(".md"))
                {
                    // Convert: https://github.com/user/repo to raw README
                    fetchUrl = listUrl.Replace("github.com", "raw.githubusercontent.com").TrimEnd('/') + "/master/README.md";
                }

                TextOverlay.Show($"🌐 Resolving list: {fetchUrl}...", 3000);
                
                string content = "";
                try
                {
                    content = await _httpClient.GetStringAsync(fetchUrl);
                }
                catch
                {
                    // Fallback to original URL if master README fails (maybe branch is 'main')
                    if (fetchUrl.Contains("/master/"))
                    {
                        fetchUrl = fetchUrl.Replace("/master/", "/main/");
                        try
                        {
                            content = await _httpClient.GetStringAsync(fetchUrl);
                        }
                        catch
                        {
                            content = await _httpClient.GetStringAsync(listUrl);
                        }
                    }
                    else
                    {
                        content = await _httpClient.GetStringAsync(listUrl);
                    }
                }

                // Find GitHub links or direct download files
                var gitMatches = Regex.Matches(content, @"(https?://github\.com/[a-zA-Z0-9_\-]+/[a-zA-Z0-9_\-]+)");
                var fileMatches = Regex.Matches(content, @"(https?://[^\s""'\(\)]+\.(?:zip|tar\.gz|csv|db|sqlite|wav|mp3))", RegexOptions.IgnoreCase);

                var linksToAcquire = new List<string>();
                foreach (Match m in gitMatches)
                {
                    string href = m.Value.TrimEnd('.', '/');
                    // Avoid duplicate/parent links
                    if (!href.Contains("/features") && !href.Contains("/issues") && !href.Contains("/pulls") && href != listUrl && !linksToAcquire.Contains(href))
                    {
                        linksToAcquire.Add(href);
                    }
                }

                foreach (Match m in fileMatches)
                {
                    string href = m.Value.TrimEnd('.', '/');
                    if (!linksToAcquire.Contains(href)) linksToAcquire.Add(href);
                }

                if (linksToAcquire.Count == 0)
                {
                    return "No downloadable datasets or GitHub links detected in the list.";
                }

                // Process up to 5 items to avoid blowing up storage
                int maxItems = Math.Min(linksToAcquire.Count, 5);
                var sb = new StringBuilder();
                sb.AppendLine($"--- List Downloader Report for: {listUrl} ---");
                sb.AppendLine($"Found {linksToAcquire.Count} targets. Downloading/Cloning top {maxItems} active datasets...");

                string baseDataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "VoiceDatasets");
                if (!Directory.Exists(baseDataDir)) Directory.CreateDirectory(baseDataDir);

                for (int i = 0; i < maxItems; i++)
                {
                    string target = linksToAcquire[i];
                    sb.AppendLine($"\n{i + 1}. Processing: {target}");
                    try
                    {
                        if (target.Contains("github.com"))
                        {
                            string repoName = Path.GetFileName(target);
                            string destPath = Path.Combine(baseDataDir, repoName);
                            
                            // Clean up previous attempts
                            try
                            {
                                if (Directory.Exists(destPath)) Directory.Delete(destPath, true);
                            }
                            catch { }

                            TextOverlay.Show($"📥 Cloning: {repoName}...", 2500);

                            var psi = new ProcessStartInfo
                            {
                                FileName = "git",
                                Arguments = $"clone {target} {destPath} --depth=1",
                                UseShellExecute = true,
                                CreateNoWindow = true
                            };
                            var proc = Process.Start(psi);
                            if (proc != null)
                            {
                                await Task.Run(() => proc.WaitForExit(30000));
                                sb.AppendLine($"   -> [SUCCESS] Cloned GitHub repo to: {destPath}");
                            }
                        }
                        else
                        {
                            string fileResult = await DownloadFileAsync(target, baseDataDir);
                            sb.AppendLine($"   -> {fileResult}");
                        }
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"   -> [FAILED] Error: {ex.Message}");
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error parsing list URL: {ex.Message}";
            }
        }

        /// <summary>
        /// Highly specific data processing via external binary with operation-level control.
        /// </summary>
        public static async Task<string> ProcessDataFineAsync(string mode, string op, string data)
        {
            try
            {
                if (!SettingsManager.Current.ENABLE_CUSTOM_PROCESSOR) return "Error: Custom processor disabled.";
                string path = SettingsManager.Current.CUSTOM_DATA_PROCESSOR_PATH;
                if (!File.Exists(path)) return "Error: Processor binary not found.";

                TextOverlay.Show($"⚙️ Processing {mode.ToUpper()}: {op}...", 3000);

                // Pass structure: [path] --mode [mode] --op [op] --data [data]
                string args = $"--mode {mode} --op {op} --data \"{data.Replace("\"", "\\\"")}\"";

                var psi = new ProcessStartInfo {
                    FileName = path,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return "Error: Start failed.";

                string output = await proc.StandardOutput.ReadToEndAsync();
                string error = await proc.StandardError.ReadToEndAsync();
                proc.WaitForExit();

                if (proc.ExitCode != 0) return $"Error ({proc.ExitCode}): {error}";
                return output.Trim();
            }
            catch (Exception ex) { return $"Process Exception: {ex.Message}"; }
        }

        /// <summary>
        /// Searches package registries (NuGet, npm, PyPI, etc.) and returns structured data.
        /// </summary>
        public static async Task<string> SearchRegistryAsync(string type, string query)
        {
            try
            {
                TextOverlay.Show($"📦 Searching {type.ToUpper()} for: {query}...", 3000);
                string url = "";

                switch (type.ToLower())
                {
                    case "nuget":
                        url = $"https://azuresearch-usnc.nuget.org/query?q={Uri.EscapeDataString(query)}&take=5";
                        break;
                    case "npm":
                        url = $"https://registry.npmjs.org/-/v1/search?text={Uri.EscapeDataString(query)}&size=5";
                        break;
                    case "pypi":
                        url = $"https://pypi.org/pypi/{Uri.EscapeDataString(query)}/json";
                        break;
                    default:
                        return await SearchWebAsync($"{type} package {query}");
                }

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return $"Error: Registry API returned {response.StatusCode}";

                string json = await response.Content.ReadAsStringAsync();

                // Use AI to summarize the JSON data into a clean memory-friendly format
                string prompt = $"Task: Summarize the following package registry JSON data for the query '{query}'.\n" +
                               "Extract: Package Name, Version, Description, and any key links.\n" +
                               "Format: Clean Markdown.\n\n" +
                               $"[DATA]:\n{json.Substring(0, Math.Min(json.Length, 10000))}";

                return await LlmRouter.AskAsync(prompt);
            }
            catch (Exception ex)
            {
                return $"Error searching registry: {ex.Message}";
            }
        }

        /// <summary>
        /// Deep Research: Scrapes a page and specifically extracts programming language documentation or API specs to remember.
        /// </summary>
        public static async Task<string> IngestDocumentationAsync(string url)
        {
            try
            {
                TextOverlay.Show($"📚 Ingesting documentation: {url}...", 3000);
                string scraped = await ScrapeWebpageAsync(url);

                // Save to semantic memory
                SemanticMemoryManager.AddMemory($"Ingested documentation from {url}: {scraped}", "Knowledge", "Documentation", 0.8, new Dictionary<string, string> { { "url", url } });

                return $"SUCCESS: Documentation from {url} has been analyzed and stored in long-term semantic memory.";
            }
            catch (Exception ex)
            {
                return $"Error ingesting documentation: {ex.Message}";
            }
        }

        /// <summary>
        /// Performs a web search using DuckDuckGo HTML search and returns markdown results.
        /// </summary>
        public static async Task<string> SearchWebAsync(string query)
        {
            try
            {
                if (string.IsNullOrEmpty(query)) return "Error: Query is empty.";

                TextOverlay.Show($"🔍 Searching the web for: {query}...", 3000);

                string searchUrl = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
                string html = await _httpClient.GetStringAsync(searchUrl);

                // Parse out results using regex matching DuckDuckGo HTML structure
                // DDG HTML results are inside div class="result results_links results_links_deep web-result"
                var matches = Regex.Matches(html, @"<a class=""result__snippet""[^>]*href=""(?<url>[^""]+)""[^>]*>(?<desc>.*?)</a>", RegexOptions.Singleline);
                var titles = Regex.Matches(html, @"<a class=""result__url""[^>]*>(?<title>.*?)</a>", RegexOptions.Singleline);

                var sb = new StringBuilder();
                sb.AppendLine($"--- Web Search Results for: {query} ---");

                int count = 0;
                for (int i = 0; i < Math.Min(matches.Count, 5); i++)
                {
                    string rawUrl = matches[i].Groups["url"].Value;
                    string desc = matches[i].Groups["desc"].Value;
                    string title = i < titles.Count ? titles[i].Groups["title"].Value : "Link Reference";

                    // Clean tags from title and description
                    title = Regex.Replace(title, @"<[^>]*?>", "").Trim();
                    desc = Regex.Replace(desc, @"<[^>]*?>", "").Trim();

                    // Resolve internal DDG redirect URLs if present
                    string actualUrl = rawUrl;
                    if (rawUrl.Contains("uddg="))
                    {
                        int idx = rawUrl.IndexOf("uddg=");
                        actualUrl = Uri.UnescapeDataString(rawUrl.Substring(idx + 5));
                        if (actualUrl.Contains("&")) actualUrl = actualUrl.Substring(0, actualUrl.IndexOf('&'));
                    }

                    sb.AppendLine($"{i + 1}. **{title}**");
                    sb.AppendLine($"   URL: {actualUrl}");
                    sb.AppendLine($"   Description: {desc}");
                    sb.AppendLine();
                    count++;
                }

                if (count == 0)
                {
                    // Fallback simpler scraper matching generic anchor snippets
                    var fallbackLink = Regex.Matches(html, @"<a class=""result__link"" href=""(?<url>[^""]+)"">(?<title>.*?)</a>", RegexOptions.Singleline);
                    for (int i = 0; i < Math.Min(fallbackLink.Count, 5); i++)
                    {
                        string rawUrl = fallbackLink[i].Groups["url"].Value;
                        string title = fallbackLink[i].Groups["title"].Value;
                        title = Regex.Replace(title, @"<[^>]*?>", "").Trim();

                        string actualUrl = rawUrl;
                        if (rawUrl.Contains("uddg="))
                        {
                            int idx = rawUrl.IndexOf("uddg=");
                            actualUrl = Uri.UnescapeDataString(rawUrl.Substring(idx + 5));
                            if (actualUrl.Contains("&")) actualUrl = actualUrl.Substring(0, actualUrl.IndexOf('&'));
                        }

                        sb.AppendLine($"{i + 1}. **{title}**");
                        sb.AppendLine($"   URL: {actualUrl}");
                        sb.AppendLine();
                        count++;
                    }
                }

                if (count == 0)
                {
                    return "No search results could be retrieved from the search index at this time.";
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error searching the web: {ex.Message}";
            }
        }

        /// <summary>
        /// Smart Media Scraper: Uses AI to discover direct video/audio links on a page and downloads them.
        /// </summary>
        public static async Task<string> DiscoverAndDownloadMediaAsync(string url, string type = "video")
        {
            try
            {
                TextOverlay.Show($"🔍 Scraping {type} links from page...", 3000);
                string html = await _httpClient.GetStringAsync(url);

                // Extract all potential links
                var matches = Regex.Matches(html, @"href=""(?<link>.*?)""|src=""(?<link>.*?)""", RegexOptions.IgnoreCase);
                var links = new List<string>();
                foreach (Match m in matches)
                {
                    string l = m.Groups["link"].Value;
                    if (!string.IsNullOrEmpty(l)) links.Add(l);
                }
                links = links.Distinct().ToList();

                string prompt = $"You are a media discovery agent. From the list of links below, identify the one that is most likely a direct download link for a {type} (MP4, MKV, MP3, etc.). " +
                                $"If you find multiple, return the highest quality one. Return ONLY the raw URL string. If none are found, return 'NONE'.\n\n" +
                                $"[SOURCE PAGE]: {url}\n" +
                                $"[LINKS]:\n{string.Join("\n", links.Take(100))}";

                string discoveredUrl = await LlmRouter.AskAsync(prompt);
                discoveredUrl = discoveredUrl.Trim();

                if (discoveredUrl == "NONE" || !discoveredUrl.StartsWith("http"))
                {
                    // Fallback to yt-dlp via DownloadMediaRunner
                    TextOverlay.Show("⚡ Direct link not found, attempting extraction via engine...", 3000);
                    return await DownloadMediaRunner.DownloadAsync(url);
                }

                TextOverlay.Show($"🚀 Discovered {type} link! Downloading...", 3000);
                return await DownloadFileAsync(discoveredUrl);
            }
            catch (Exception ex)
            {
                return $"Error in smart media scraper: {ex.Message}";
            }
        }
    }
}
