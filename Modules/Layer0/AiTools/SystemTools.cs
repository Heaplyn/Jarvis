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
            string cmd = m.Groups["c"].Value.Trim();
            if (!executedTags.Add("PS:" + cmd.GetHashCode())) return Task.FromResult("");
            string output = AgentExecutor.ExecutePowerShellDirect(cmd);
            return Task.FromResult($"[PS OUTPUT]:\n{output}\n");
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
            int killed = 0;
            foreach (var p in Process.GetProcessesByName(name)) { p.Kill(); killed++; }
            return Task.FromResult($"[KILLED: {name} ({killed} instances)]\n");
        }
    }
}
