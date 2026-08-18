using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JarvisLauncher.AiTools
{
    public class ReadFileTool : IAiTool
    {
        public string Tag => "RF";
        public string RegexPattern => @"@rf\{(?<p>.*?)\}";
        public async Task<string> ExecuteAsync(Match m, HashSet<string> executedTags)
        {
            string p = m.Groups["p"].Value.Trim().Trim('"', '\'');
            if (!executedTags.Add("RF:" + p)) return "";
            if (File.Exists(p)) {
                string content = await File.ReadAllTextAsync(p);
                return $"[FILE: {p}]\n{(content.Length > 3000 ? content.Substring(0, 3000) + "... [Truncated]" : content)}\n[END]\n";
            }
            return $"[ERROR: File {p} not found]\n";
        }
    }

    public class WriteFileTool : IAiTool
    {
        public string Tag => "WF";
        public string RegexPattern => @"@wf\{(?<p>.*?)\}\{(?<c>.*?)\}";
        public async Task<string> ExecuteAsync(Match m, HashSet<string> executedTags)
        {
            string p = m.Groups["p"].Value.Trim().Trim('"', '\'');
            string c = m.Groups["c"].Value;
            if (!executedTags.Add("WF:" + p + c.GetHashCode())) return "";
            Directory.CreateDirectory(Path.GetDirectoryName(p) ?? ".");
            await File.WriteAllTextAsync(p, c);
            SemanticMemoryManager.AddTrackedFile(p);
            return $"[WRITTEN: {p}]\n";
        }
    }

    public class ListFilesTool : IAiTool
    {
        public string Tag => "LS";
        public string RegexPattern => @"@ls\{(?<p>.*?)\}";
        public Task<string> ExecuteAsync(Match m, HashSet<string> executedTags)
        {
            string p = m.Groups["p"].Value.Trim().Trim('"', '\'');
            if (Directory.Exists(p)) {
                var entries = Directory.GetFileSystemEntries(p).Select(Path.GetFileName).Take(50);
                return Task.FromResult($"[DIR {p}]:\n{string.Join("\n", entries)}\n");
            }
            return Task.FromResult($"[ERROR: Dir {p} not found]\n");
        }
    }

    public class ReadBinaryTool : IAiTool
    {
        public string Tag => "RF_B";
        public string RegexPattern => @"@rf_b\{(?<p>.*?)\}";
        public async Task<string> ExecuteAsync(Match m, HashSet<string> executedTags)
        {
            string p = m.Groups["p"].Value.Trim().Trim('"', '\'');
            if (!executedTags.Add("RF_B:" + p)) return "";
            if (File.Exists(p)) {
                byte[] data = await File.ReadAllBytesAsync(p);
                string b64 = Convert.ToBase64String(data);
                return $"[BINARY FILE: {p}]\n[BASE64]: {b64}\n[END]\n";
            }
            return $"[ERROR: File {p} not found]\n";
        }
    }

    public class WriteBinaryTool : IAiTool
    {
        public string Tag => "WF_B";
        public string RegexPattern => @"@wf_b\{(?<p>.*?)\}\{(?<b>.*?)\}";
        public async Task<string> ExecuteAsync(Match m, HashSet<string> executedTags)
        {
            string p = m.Groups["p"].Value.Trim().Trim('"', '\'');
            string b = m.Groups["b"].Value.Trim();
            if (!executedTags.Add("WF_B:" + p)) return "";
            try {
                byte[] data = Convert.FromBase64String(b);
                Directory.CreateDirectory(Path.GetDirectoryName(p) ?? ".");
                await File.WriteAllBytesAsync(p, data);
                return $"[WRITTEN BINARY: {p}]\n";
            } catch (Exception ex) { return $"[ERROR WF_B]: {ex.Message}\n"; }
        }
    }
}
