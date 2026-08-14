// Developer: heaplyn
// Date: 2026-08-13
// Summary: Offline Mode & Wi-Fi Pre-Caching Studio Overlay.
// Provides 1-click pre-caching for Vosk speech models, GitHub TTS voice samples, & GGUF models for offline use.

using System;
using System.IO;
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

        public OfflineStudioOverlay()
            : base("OFFLINE MODE & PRE-CACHING STUDIO", width: 520, height: 600)
        {
            var workArea = SystemParameters.WorkArea;
            this.Left = (workArea.Width - this.Width) / 2;
            this.Top = (workArea.Height - this.Height) / 2;

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root = new StackPanel { Margin = new Thickness(6) };
            scroll.Content = root;

            // ── Header & Info ─────────────────────────────────────────────────────────
            root.Children.Add(CreateHeader("📶 Offline Mode & Wi-Fi Pre-Caching Studio"));

            var info = new TextBlock
            {
                Text = "Pre-download speech recognition models, custom TTS voice samples, and local LLM GGUF models over Wi-Fi so Jarvis is 100% functional without internet.",
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            info.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            root.Children.Add(info);

            // ── Status Dashboard ──────────────────────────────────────────────────────
            root.Children.Add(CreateHeader("📊 Connection & Cache Status"));

            _connectionStatus = new TextBlock { FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 2, 0, 4) };
            _connectionStatus.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            root.Children.Add(_connectionStatus);

            _voskStatus = new TextBlock { FontSize = 12, Margin = new Thickness(0, 2, 0, 4) };
            _voskStatus.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            root.Children.Add(_voskStatus);

            _ttsStatus = new TextBlock { FontSize = 12, Margin = new Thickness(0, 2, 0, 8) };
            _ttsStatus.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            root.Children.Add(_ttsStatus);

            // ── Pre-Cache Action Button ───────────────────────────────────────────────
            root.Children.Add(CreateHeader("⚡ Wi-Fi Pre-Caching Actions"));

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
            root.Children.Add(preCacheBtn);

            _progressText = new TextBlock
            {
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            };
            _progressText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            root.Children.Add(_progressText);

            this.UserContent = scroll;
            RefreshStatus();
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

        public static void ShowOverlay()
        {
            if (_instance == null || !_instance.IsLoaded)
            {
                _instance = new OfflineStudioOverlay();
                _instance.Show();
            }
            else
            {
                _instance.Activate();
            }
        }
    }
}
