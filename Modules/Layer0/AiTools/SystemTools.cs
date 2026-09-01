using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JarvisLauncher.AiTools
{
    public class PowerShellTool : IAiTool
    {
        public string Tag => "PS";
        public string RegexPattern => @"@ps\{(?<c>.*?)\}";
        public Task<string> ExecuteAsync(Match m, HashSet<string> executedTags)
        {
            // SECURITY: model-emitted PowerShell is disabled. The model may not run arbitrary shell.
            return Task.FromResult("[BLOCKED: @ps is disabled — the model may not run PowerShell]\n");
        }
    }

    public class ProcessListTool : IAiTool
    {
        public string Tag => "PL";
        public string RegexPattern => @"@proc_list";
        public Task<string> ExecuteAsync(Match m, HashSet<string> executedTags)
        {
            if (!executedTags.Add("PL")) return Task.FromResult("");
            var procs = Process.GetProcesses().Select(p => p.ProcessName).Distinct().OrderBy(n => n).Take(100);
            return Task.FromResult($"[PROCESSES]:\n{string.Join(", ", procs)}\n");
        }
    }

    public class ProcessKillTool : IAiTool
    {
        public string Tag => "PK";
        public string RegexPattern => @"@proc_kill\{(?<n>.*?)\}";
        public Task<string> ExecuteAsync(Match m, HashSet<string> executedTags)
        {
            string name = m.Groups["n"].Value.Trim().Trim('"', '\'');
            if (!executedTags.Add("PK:" + name)) return Task.FromResult("");
            // SECURITY: model-initiated process termination requires explicit human confirmation.
            if (!HumanConfirm.Ask($"Jarvis (AI) wants to force-kill all '{name}' processes. Allow?"))
                return Task.FromResult($"[DENIED: user declined to kill {name}]\n");
            int killed = 0;
            foreach (var p in Process.GetProcessesByName(name)) { try { p.Kill(); killed++; } catch { } }
            return Task.FromResult($"[KILLED: {name} ({killed} instances)]\n");
        }
    }
}
