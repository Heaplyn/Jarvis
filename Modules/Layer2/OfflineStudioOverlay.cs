// Developer: heaplyn
// Date: 2026-08-13
// Summary: Offline Mode & Wi-Fi Pre-Caching Studio Overlay.
// Provides 1-click pre-caching for Vosk speech models, GitHub TTS voice samples, & GGUF models for offline use.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace JarvisLauncher
{
    public class OfflineStudioOverlay : BaseOverlay
    {
        private static OfflineStudioOverlay? _instance;
        private TextBlock _connectionStatus = null!;
        private TextBlock _voskStatus = null!;
        private TextBlock _ttsStatus = null!;
        private TextBlock _progressText = null!;

        public static void ShowOverlay()
        {
            if (_instance == null || !_instance.IsLoaded || !_instance.IsVisible)
            {
                _instance = new OfflineStudioOverlay();
                _instance.Show();
            }
            else
            {
                _instance.Activate();
                _instance.BringToFront();
            }
        }

        public OfflineStudioOverlay()
            : base("OFFLINE MODE & PRE-CACHING STUDIO", width: 850, height: 680)
        {
            this.Closed += (s, e) => { _instance = null; };

            var workArea = SystemParameters.WorkArea;
            this.Left = (workArea.Width - this.Width) / 2;
            this.Top = (workArea.Height - this.Height) / 2;

            var tabControl = new TabControl { Margin = new Thickness(4) };
            StyleTabControl(tabControl);

            // ── Tab 1: Core Cache ──────────────────────────────────────────────────
            var corePanel = CreateTab(tabControl, "📡 Core Cache");
            corePanel.Children.Add(CreateHeader("📡 Offline Mode & Wi-Fi Pre-Caching"));

            var info = new TextBlock
            {
                Text = "Pre-download speech recognition models, custom TTS voice samples, and local LLM GGUF models over Wi-Fi so Jarvis is 100% functional without internet.",
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            info.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            corePanel.Children.Add(info);

            corePanel.Children.Add(CreateHeader("📊 Connection & Cache Status"));

            _connectionStatus = new TextBlock { FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 2, 0, 4) };
            _connectionStatus.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            corePanel.Children.Add(_connectionStatus);

            _voskStatus = new TextBlock { FontSize = 12, Margin = new Thickness(0, 2, 0, 4) };
            _voskStatus.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            corePanel.Children.Add(_voskStatus);

            _ttsStatus = new TextBlock { FontSize = 12, Margin = new Thickness(0, 2, 0, 8) };
            _ttsStatus.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            corePanel.Children.Add(_ttsStatus);

            corePanel.Children.Add(CreateHeader("⚡ Wi-Fi Pre-Caching Actions"));

            var preCacheBtn = CreateButton("📶 Pre-Cache All Features For Offline Use");
            preCacheBtn.Height = 36;
            preCacheBtn.FontWeight = FontWeights.Bold;
            preCacheBtn.Click += async (s, e) =>
            {
                preCacheBtn.IsEnabled = false;
                await OfflineCacheManager.PreCacheAllForOfflineAsync(status =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _progressText.Text = status;
                    });
                });
                RefreshStatus();
                preCacheBtn.IsEnabled = true;
            };
            corePanel.Children.Add(preCacheBtn);

            _progressText = new TextBlock
            {
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            };
            _progressText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            corePanel.Children.Add(_progressText);

            // ── Tab 2: AI & Dev Runtimes ──────────────────────────────────────────
            var aiPanel = CreateTab(tabControl, "🤖 AI & Dev");
            aiPanel.Children.Add(CreateHeader("🤖 Local AI & Developer Runtimes"));
            AddToolRow(aiPanel, "Ollama Local LLM Runner", "Ollama.Ollama", "ollama");
            AddToolRow(aiPanel, "DeepSeek-R1 Local LLM", "", "", isOllamaModel: true, modelName: "deepseek-r1:7b");
            AddToolRow(aiPanel, "Python 3.x Environment", "Python.Python.3", "python");
            AddToolRow(aiPanel, "Node.js JavaScript Runtime", "OpenJS.NodeJS", "node");
            AddToolRow(aiPanel, "Git Version Control Engine", "Git.Git", "git");

            // ── Tab 3: Programming Languages & SDKs ───────────────────────────────
            var langPanel = CreateTab(tabControl, "💻 Languages & SDKs");
            langPanel.Children.Add(CreateHeader("💻 Programming Languages & SDKs"));
            AddToolRow(langPanel, "TypeScript Language Compiler", "npm:typescript", "tsc");
            AddToolRow(langPanel, "Rustup & Cargo Toolchain (Rust)", "Rustlang.Rustup", "rustc.exe");
            AddToolRow(langPanel, "Go Programming Language", "GoLang.Go", "go.exe");
            AddToolRow(langPanel, "Dart Software Development Kit", "Dart.Dart", "dart.exe");
            AddToolRow(langPanel, ".NET 8.0 SDK (C# / F# / VB)", "Microsoft.DotNet.SDK.8", "dotnet.exe");
            AddToolRow(langPanel, "Temurin OpenJDK 21 (Java)", "Eclipse.Temurin.21.JDK", "javac.exe");
            AddToolRow(langPanel, "LLVM Toolchain (C/C++ Clang/GDB)", "LLVM.LLVM", "clang.exe");
            AddToolRow(langPanel, "MSYS2 Environment (C/C++ GCC/Make)", "MSYS2.MSYS2", "msys2.exe");
            AddToolRow(langPanel, "NASM Netwide Assembler (Assembly)", "NASM.NASM", "nasm.exe");
            AddToolRow(langPanel, "Ruby Programming Language", "Ruby-Lang.Ruby", "ruby.exe");
            AddToolRow(langPanel, "PHP Hypertext Preprocessor", "PHP.PHP", "php.exe");
            AddToolRow(langPanel, "Swift Programming Language", "Swift.Swift", "swift.exe");
            AddToolRow(langPanel, "GhcUp Haskell Compiler Suite", "Haskell.GhcUp", "ghc.exe");
            AddToolRow(langPanel, "Julia Programming Language", "JuliaLang.Julia", "julia.exe");
            AddToolRow(langPanel, "Kotlin Compiler (JVM)", "JetBrains.Kotlin", "kotlinc.exe");
            AddToolRow(langPanel, "Scala Programming Language", "Scala.Scala", "scala.exe");
            AddToolRow(langPanel, "Zig Programming Language", "zig.zig", "zig.exe");
            AddToolRow(langPanel, "Nim Programming Language", "NimProject.Nim", "nim.exe");
            AddToolRow(langPanel, "Strawberry Perl Environment", "StrawberryPerl.StrawberryPerl", "perl.exe");
            AddToolRow(langPanel, "R Language for Statistical Computing", "RProject.R", "R.exe");
            AddToolRow(langPanel, "D Programming Language (DMD)", "DLanguage.DMD", "dmd.exe");
            AddToolRow(langPanel, "Elixir Functional Language", "Elixir.Elixir", "elixir.bat");
            AddToolRow(langPanel, "Erlang OTP Concurrent System", "Erlang.OTP", "erl.exe");
            AddToolRow(langPanel, "Clojure Lisp Implementation", "Clojure.Clojure", "clj.exe");
            AddToolRow(langPanel, "OCaml Language & Compiler", "OCaml.OCaml", "ocaml.exe");
            AddToolRow(langPanel, "Lua Scripting Environment", "Lua.Lua", "lua.exe");

            // ── Tab 4: Shells & CLI Utilities ─────────────────────────────────────
            var shellPanel = CreateTab(tabControl, "🐚 Shells & CLI");
            shellPanel.Children.Add(CreateHeader("🐚 Shells & Command Line Utilities"));
            AddToolRow(shellPanel, "Bash / Windows Subsystem for Linux (WSL)", "Microsoft.WSL", "wsl");
            AddToolRow(shellPanel, "PowerShell Core (pwsh)", "Microsoft.PowerShell", "pwsh");
            AddToolRow(shellPanel, "FFmpeg Multimedia Toolchain", "Gyan.FFmpeg", "ffmpeg");
            AddToolRow(shellPanel, "7-Zip Command Line Archiver", "7zip.7zip", "7z");
            AddToolRow(shellPanel, "GnuPG Security Cryptography", "GnuPG.GnuPG", "gpg");
            AddToolRow(shellPanel, "GNU Make Automation Engine", "GnuWin32.Make", "make");

            // ── Tab 5: IDEs & Editors ─────────────────────────────────────────────
            var editorPanel = CreateTab(tabControl, "📝 IDEs & Editors");
            editorPanel.Children.Add(CreateHeader("📝 Integrated Development Environments"));
            AddToolRow(editorPanel, "Visual Studio Code", "Microsoft.VisualStudioCode", "Code.exe");
            AddToolRow(editorPanel, "Visual Studio 2022 Community", "Microsoft.VisualStudio.2022.Community", "devenv.exe");

            // ── Tab 6: Creative & Media ───────────────────────────────────────────
            var creativePanel = CreateTab(tabControl, "🎨 Creative & Media");
            creativePanel.Children.Add(CreateHeader("🎨 Photo, Media & Creative Editors"));
            AddToolRow(creativePanel, "GIMP Image Editor", "GNU.GIMP", "gimp-2.10.exe");
            AddToolRow(creativePanel, "Paint.NET Photo Editor", "dotPDN.PaintDotNet", "PaintDotNet.exe");
            AddToolRow(creativePanel, "Krita Digital Painting Studio", "Krita.Krita", "krita.exe");
            AddToolRow(creativePanel, "Inkscape Vector Graphics", "Inkscape.Inkscape", "inkscape.exe");
            AddToolRow(creativePanel, "Blender 3D Modeling Suite", "BlenderFoundation.Blender", "blender.exe");
            AddToolRow(creativePanel, "Audacity Audio Editor", "Audacity.Audacity", "Audacity.exe");
            AddToolRow(creativePanel, "VLC Media Player", "VideoLAN.VLC", "vlc.exe");

            // ── Tab 7: Games & Engines ────────────────────────────────────────────
            var gamePanel = CreateTab(tabControl, "🎮 Games & Engines");
            gamePanel.Children.Add(CreateHeader("🎮 Game Engines & Editors"));
            AddToolRow(gamePanel, "Unity Hub (Game Engine)", "Unity.UnityHub", "Unity Hub.exe");
            AddToolRow(gamePanel, "Godot Game Engine", "GodotEngine.GodotEngine", "godot");
            AddToolRow(gamePanel, "Epic Games Launcher (Unreal)", "EpicGames.EpicGamesLauncher", "EpicGamesLauncher.exe");

            gamePanel.Children.Add(CreateHeader("🕹️ Open Source Offline Games"));
            AddToolRow(gamePanel, "OpenTTD Transport Tycoon Clone", "OpenTTD.OpenTTD", "openttd.exe");
            AddToolRow(gamePanel, "Luanti (Voxel Game Engine)", "LuantiTeam.Luanti", "minetest");
            AddToolRow(gamePanel, "SuperTuxKart Racing Game", "SuperTuxKart.SuperTuxKart", "supertuxkart.exe");

            this.UserContent = tabControl;
            RefreshStatus();
        }

        private StackPanel CreateTab(TabControl tabControl, string headerText)
        {
            var tab = new TabItem { Header = headerText };
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var panel = new StackPanel { Margin = new Thickness(10) };
            scroll.Content = panel;
            tab.Content = scroll;
            tabControl.Items.Add(tab);
            return panel;
        }

        private void AddToolRow(StackPanel root, string friendlyName, string packageId, string commandCheck, bool isOllamaModel = false, string modelName = "")
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };

            var nameLabel = new TextBlock
            {
                Text = friendlyName,
                Width = 220,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11
            };
            nameLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            row.Children.Add(nameLabel);

            var statusLabel = new TextBlock
            {
                Text = "⏳ Checking...",
                Width = 140,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                FontWeight = FontWeights.Bold
            };
            statusLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            row.Children.Add(statusLabel);

            var actionBtn = CreateButton(isOllamaModel ? "📥 Pull" : "📥 Install");
            actionBtn.Width = 100;
            actionBtn.Height = 24;
            actionBtn.FontSize = 10;
            actionBtn.Click += (s, e) =>
            {
                if (isOllamaModel)
                {
                    OfflineCacheManager.PullOllamaModel(modelName);
                }
                else
                {
                    OfflineCacheManager.InstallToolViaWinget(packageId, friendlyName);
                }
            };
            row.Children.Add(actionBtn);

            root.Children.Add(row);

            // Update status asynchronously
            Task.Run(async () =>
            {
                string statusText = "🔴 Not Installed";
                SolidColorBrush color = Brushes.Red;

                if (isOllamaModel)
                {
                    bool ollamaRunning = await OfflineCacheManager.IsOllamaRunningAsync();
                    if (!ollamaRunning)
                    {
                        statusText = "⚪ Ollama Stopped";
                        color = Brushes.Gray;
                    }
                    else
                    {
                        bool modelCached = await OfflineCacheManager.IsOllamaModelCachedAsync(modelName);
                        if (modelCached)
                        {
                            statusText = "✅ Cached Offline";
                            color = Brushes.Green;
                        }
                        else
                        {
                            statusText = "🔴 Not Cached";
                            color = Brushes.OrangeRed;
                        }
                    }
                }
                else if (friendlyName.Contains("Ollama"))
                {
                    bool hasCmd = OfflineCacheManager.IsAppInstalled(commandCheck);
                    bool running = await OfflineCacheManager.IsOllamaRunningAsync();
                    if (running)
                    {
                        statusText = "🟢 Running";
                        color = Brushes.Green;
                    }
                    else if (hasCmd)
                    {
                        statusText = "🟡 Stopped";
                        color = Brushes.Orange;
                    }
                    else
                    {
                        statusText = "🔴 Not Installed";
                        color = Brushes.Red;
                    }
                }
                else
                {
                    bool installed = OfflineCacheManager.IsAppInstalled(commandCheck);
                    statusText = installed ? "🟢 Installed" : "🔴 Not Installed";
                    color = installed ? Brushes.Green : Brushes.Red;
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    statusLabel.Text = statusText;
                    statusLabel.Foreground = color;
                    if (statusText.StartsWith("🟢") || statusText.StartsWith("✅"))
                    {
                        actionBtn.IsEnabled = false;
                        actionBtn.Content = "✅ Ready";
                    }
                    else
                    {
                        actionBtn.IsEnabled = true;
                    }
                });
            });
        }

        private void RefreshStatus()
        {
            bool online = OfflineCacheManager.IsInternetAvailable();
            _connectionStatus.Text = online ? "📡 Network: 🟢 Connected (Wi-Fi / Ethernet)" : "📡 Network: 🔴 Offline Mode Active";

            bool voskReady = Directory.Exists(VoskEngine.ModelDirectory);
            _voskStatus.Text = voskReady
                ? "🎙️ Vosk Offline Neural Speech Model: ✅ Ready Offline (40MB extracted)"
                : "🎙️ Vosk Offline Neural Speech Model: ⚠️ Not Downloaded Yet";

            string voiceDir = TtsSampleDownloader.VoiceDirectory;
            int cachedVoices = Directory.Exists(voiceDir) ? Directory.GetFiles(voiceDir, "*.mp3").Length : 0;
            _ttsStatus.Text = cachedVoices > 0
                ? $"🎵 Cached GitHub TTS Voice Samples: ✅ {cachedVoices} voices cached offline"
                : "🎵 Cached GitHub TTS Voice Samples: ⚠️ No voices cached offline yet";
        }

        private static TextBlock CreateHeader(string title)
        {
            var header = new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 8, 0, 4)
            };
            header.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            return header;
        }

        private static Button CreateButton(string content)
        {
            var btn = new Button
            {
                Content = content,
                Margin = new Thickness(0, 4, 0, 4),
                Padding = new Thickness(10, 6, 10, 6),
                FontSize = 12,
                Cursor = Cursors.Hand
            };
            return btn;
        }
    }
}
