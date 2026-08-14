// Developer: heaplyn
// Date: 2026-08-14
// Summary: High-performance C# to iOS IPA compiler utility.
// Invokes the local .NET MAUI / iOS build chain and stores the resulting IPA package for remote transfer.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public static class IpaCompilerService
    {
        public static string LastCompiledIpaPath { get; set; } = string.Empty;
        public static string CompileStatus { get; set; } = "Idle. Select a C# project and click Compile.";
        public static event Action<string>? OnCompileLogUpdated;

        public static async Task<bool> CompileProjectAsync(string csprojPath, string certificateName = "", string provisioningProfileName = "")
        {
            if (!File.Exists(csprojPath))
            {
                CompileStatus = "Error: Selected project file (.csproj) does not exist.";
                OnCompileLogUpdated?.Invoke(CompileStatus);
                return false;
            }

            CompileStatus = "Compiling C# project to iOS IPA target...";
            OnCompileLogUpdated?.Invoke("🚀 Initializing .NET iOS MSBuild compilation pipeline...\n");

            string projectDir = Path.GetDirectoryName(csprojPath)!;
            string buildLogs = "";

            return await Task.Run(() =>
            {
                try
                {
                    // Clean previous IPA builds
                    string outputDir = Path.Combine(projectDir, "bin", "Release", "net8.0-ios");
                    if (Directory.Exists(outputDir))
                    {
                        foreach (var file in Directory.GetFiles(outputDir, "*.ipa", SearchOption.AllDirectories))
                        {
                            try { File.Delete(file); } catch { }
                        }
                    }

                    // Build Arguments: Supports hot-restart local provisioning or standard key/profile association
                    var args = new StringBuilder();
                    args.Append($"publish \"{csprojPath}\" -f net8.0-ios -c Release -p:BuildIpa=true");
                    
                    if (!string.IsNullOrEmpty(certificateName))
                    {
                        args.Append($" -p:CodesignKey=\"{certificateName}\"");
                    }
                    if (!string.IsNullOrEmpty(provisioningProfileName))
                    {
                        args.Append($" -p:CodesignProvision=\"{provisioningProfileName}\"");
                    }

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = args.ToString(),
                        WorkingDirectory = projectDir,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (var process = new Process { StartInfo = startInfo })
                    {
                        process.OutputDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                OnCompileLogUpdated?.Invoke(e.Data + "\n");
                            }
                        };
                        process.ErrorDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                OnCompileLogUpdated?.Invoke("⚠️ " + e.Data + "\n");
                            }
                        };

                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        process.WaitForExit();

                        if (process.ExitCode == 0)
                        {
                            // Find the resulting .ipa file
                            if (Directory.Exists(outputDir))
                            {
                                var ipaFile = Directory.GetFiles(outputDir, "*.ipa", SearchOption.AllDirectories)
                                    .FirstOrDefault();

                                if (ipaFile != null && File.Exists(ipaFile))
                                {
                                    LastCompiledIpaPath = ipaFile;
                                    CompileStatus = "Success";
                                    OnCompileLogUpdated?.Invoke($"\n🎉 SUCCESS! Compiled IPA path: {ipaFile}\nIt is now ready to download via your Jarvis Mobile Companion!\n");
                                    return true;
                                }
                            }
                            CompileStatus = "Error: Compilation finished but no .ipa output file was found in bin output directory.";
                            OnCompileLogUpdated?.Invoke($"\n❌ {CompileStatus}\n");
                            return false;
                        }
                        else
                        {
                            CompileStatus = $"Error: .NET compiler exited with code {process.ExitCode}.";
                            OnCompileLogUpdated?.Invoke($"\n❌ Compilation failed with exit code: {process.ExitCode}\n");
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    CompileStatus = $"Error: {ex.Message}";
                    OnCompileLogUpdated?.Invoke($"\n❌ Critical build exception: {ex.Message}\n");
                    return false;
                }
            });
        }
    }
}
