// Developer: heaplyn
// Date: 2026-08-09
// Summary: Interactive Glassmorphic Music Player & Playlist Manager GUI featuring custom song folders, track organization, media playback controls, file import, and online URL stream link adding.

using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace JarvisLauncher
{
    public class MusicPlaylistOverlay : BaseOverlay
    {
        private static MusicPlaylistOverlay? _instance;

        private MusicLibraryData _library;
        private MusicFolder? _activeFolder;
        private MusicTrack? _currentTrack;

        private readonly MediaPlayer _mediaPlayer = new MediaPlayer();
        private readonly DispatcherTimer _playTimer = new DispatcherTimer();

        private readonly ComboBox _folderComboBox;
        private readonly StackPanel _tracksPanel;
        private readonly TextBlock _nowPlayingTitle;
        private readonly TextBlock _nowPlayingArtist;
        private readonly Slider _positionSlider;
        private readonly TextBlock _timeLabel;
        private readonly Button _playPauseBtn;

        private bool _isPlaying = false;
        private bool _isDraggingSlider = false;

        public static void OpenPlayer()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_instance == null)
                {
                    _instance = new MusicPlaylistOverlay();
                }

                _instance.Show();

                if (_instance.WindowState == WindowState.Minimized)
                {
                    _instance.WindowState = WindowState.Normal;
                }

                _instance.Activate();
                _instance.Focus();
            });
        }

        private MusicPlaylistOverlay()
            : base("🎵 JARVIS MUSIC PLAYER & PLAYLIST ORGANIZER", width: 680, height: 520)
        {
            _library = MusicPlaylistManager.LoadLibrary();
            _activeFolder = _library.Folders.FirstOrDefault(f => f.Id == _library.LastActiveFolderId) 
                            ?? _library.Folders.FirstOrDefault();

            this.Closed += (s, e) =>
            {
                _playTimer.Stop();
                _mediaPlayer.Close();
                _instance = null;
            };

            // Setup Playhead Position Timer
            _playTimer.Interval = TimeSpan.FromMilliseconds(500);
            _playTimer.Tick += PlayTimer_Tick;

            _mediaPlayer.MediaEnded += (s, e) => PlayNextTrack();
            _mediaPlayer.MediaFailed += (s, e) =>
            {
                TextOverlay.Show($"⚠️ Media Playback Error: {e.ErrorException?.Message ?? "Invalid format"}", 3500);
            };

            var mainGrid = new Grid { Margin = new Thickness(10) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                     // Folder Selection Bar & Add Buttons
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Track List View
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                     // Player Controls & Progress Bar

            // 1. Top Folder Bar
            var topBarBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var topGrid = new Grid();
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var folderLabel = new TextBlock
            {
                Text = "📁 Folder: ",
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 6, 0)
            };
            folderLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            Grid.SetColumn(folderLabel, 0);
            topGrid.Children.Add(folderLabel);

            _folderComboBox = new ComboBox
            {
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI"),
                Padding = new Thickness(6, 4, 6, 4)
            };
            RefreshFoldersDropDown();
            _folderComboBox.SelectionChanged += FolderComboBox_SelectionChanged;
            Grid.SetColumn(_folderComboBox, 1);
            topGrid.Children.Add(_folderComboBox);

            var buttonStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 0, 0) };

            var newFolderBtn = CreateButton("➕ New Folder", (s, e) => CreateNewFolderPrompt());
            var addFileBtn = CreateButton("🎵 Add Audio File", (s, e) => BrowseAndAddFile());
            var addUrlBtn = CreateButton("🔗 Add Link/URL", (s, e) => AddUrlStreamPrompt());

            buttonStack.Children.Add(newFolderBtn);
            buttonStack.Children.Add(addFileBtn);
            buttonStack.Children.Add(addUrlBtn);

            Grid.SetColumn(buttonStack, 2);
            topGrid.Children.Add(buttonStack);

            topBarBorder.Child = topGrid;
            Grid.SetRow(topBarBorder, 0);
            mainGrid.Children.Add(topBarBorder);

            // 2. Center Track List
            var listScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 0, 0, 10)
            };

            _tracksPanel = new StackPanel();
            listScrollViewer.Content = _tracksPanel;
            Grid.SetRow(listScrollViewer, 1);
            mainGrid.Children.Add(listScrollViewer);

            // 3. Player Bar (Now Playing + Controls + Progress Slider)
            var playerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Padding = new Thickness(12, 10, 12, 10),
                CornerRadius = new CornerRadius(8)
            };

            var playerStack = new StackPanel();

            // Track info labels
            _nowPlayingTitle = new TextBlock
            {
                Text = "No track selected",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _nowPlayingTitle.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            playerStack.Children.Add(_nowPlayingTitle);

            _nowPlayingArtist = new TextBlock
            {
                Text = "Select a song from your playlist folders to play",
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 6)
            };
            _nowPlayingArtist.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            playerStack.Children.Add(_nowPlayingArtist);

            // Slider & Time
            var sliderGrid = new Grid();
            sliderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sliderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _positionSlider = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                IsMoveToPointEnabled = true
            };
            _positionSlider.PreviewMouseDown += (s, e) => _isDraggingSlider = true;
            _positionSlider.PreviewMouseUp += (s, e) =>
            {
                _isDraggingSlider = false;
                if (_mediaPlayer.NaturalDuration.HasTimeSpan)
                {
                    double targetSec = (_positionSlider.Value / 100.0) * _mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                    _mediaPlayer.Position = TimeSpan.FromSeconds(targetSec);
                }
            };
            Grid.SetColumn(_positionSlider, 0);
            sliderGrid.Children.Add(_positionSlider);

            _timeLabel = new TextBlock
            {
                Text = "00:00 / 00:00",
                FontSize = 11,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _timeLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            Grid.SetColumn(_timeLabel, 1);
            sliderGrid.Children.Add(_timeLabel);

            playerStack.Children.Add(sliderGrid);

            // Buttons Bar (Prev, Play/Pause, Next)
            var controlsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };

            var prevBtn = CreateButton("⏮️ Prev", (s, e) => PlayPrevTrack());
            _playPauseBtn = CreateButton("▶️ Play", (s, e) => TogglePlayPause());
            var nextBtn = CreateButton("⏭️ Next", (s, e) => PlayNextTrack());

            controlsPanel.Children.Add(prevBtn);
            controlsPanel.Children.Add(_playPauseBtn);
            controlsPanel.Children.Add(nextBtn);

            playerStack.Children.Add(controlsPanel);

            playerBorder.Child = playerStack;
            Grid.SetRow(playerBorder, 2);
            mainGrid.Children.Add(playerBorder);

            this.UserContent = mainGrid;

            RenderTracksList();
        }

        private void RefreshFoldersDropDown()
        {
            _folderComboBox.Items.Clear();
            foreach (var f in _library.Folders)
            {
                _folderComboBox.Items.Add(f.FolderName);
            }

            if (_activeFolder != null)
            {
                _folderComboBox.SelectedItem = _activeFolder.FolderName;
            }
        }

        private void FolderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_folderComboBox.SelectedItem is string name)
            {
                var folder = _library.Folders.FirstOrDefault(f => f.FolderName == name);
                if (folder != null)
                {
                    _activeFolder = folder;
                    _library.LastActiveFolderId = folder.Id;
                    MusicPlaylistManager.SaveLibrary(_library);
                    RenderTracksList();
                }
            }
        }

        private void RenderTracksList()
        {
            _tracksPanel.Children.Clear();

            if (_activeFolder == null || _activeFolder.Tracks.Count == 0)
            {
                var emptyLabel = new TextBlock
                {
                    Text = "📂 This folder is empty. Click 'Add Audio File' or 'Add Link/URL' to import music!",
                    FontSize = 13,
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(12)
                };
                emptyLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                _tracksPanel.Children.Add(emptyLabel);
                return;
            }

            for (int i = 0; i < _activeFolder.Tracks.Count; i++)
            {
                var track = _activeFolder.Tracks[i];

                var trackItemBorder = new Border
                {
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 0, 0, 4),
                    CornerRadius = new CornerRadius(4),
                    Background = (_currentTrack?.Id == track.Id)
                        ? new SolidColorBrush(Color.FromArgb(50, 128, 80, 230))
                        : new SolidColorBrush(Color.FromArgb(15, 255, 255, 255)),
                    Cursor = Cursors.Hand
                };

                var itemGrid = new Grid();
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var iconText = new TextBlock
                {
                    Text = track.IsStreamUrl ? "🔗 " : "🎵 ",
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(iconText, 0);
                itemGrid.Children.Add(iconText);

                var titleStack = new StackPanel();
                var titleBlock = new TextBlock
                {
                    Text = track.Title,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold
                };
                titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                titleStack.Children.Add(titleBlock);

                var pathBlock = new TextBlock
                {
                    Text = track.PathOrUrl,
                    FontSize = 10,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                pathBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                titleStack.Children.Add(pathBlock);

                Grid.SetColumn(titleStack, 1);
                itemGrid.Children.Add(titleStack);

                var deleteBtn = CreateButton("❌", (s, e) =>
                {
                    _activeFolder.Tracks.Remove(track);
                    MusicPlaylistManager.SaveLibrary(_library);
                    RenderTracksList();
                });
                deleteBtn.Padding = new Thickness(6, 2, 6, 2);
                Grid.SetColumn(deleteBtn, 2);
                itemGrid.Children.Add(deleteBtn);

                trackItemBorder.Child = itemGrid;

                // Click track to play
                trackItemBorder.MouseLeftButtonDown += (s, e) => PlayTrack(track);

                _tracksPanel.Children.Add(trackItemBorder);
            }
        }

        private void PlayTrack(MusicTrack track)
        {
            try
            {
                _currentTrack = track;
                _nowPlayingTitle.Text = track.Title;
                _nowPlayingArtist.Text = track.IsStreamUrl ? $"Stream Link: {track.PathOrUrl}" : $"Local File: {track.PathOrUrl}";

                if (track.IsStreamUrl || track.PathOrUrl.StartsWith("http://") || track.PathOrUrl.StartsWith("https://"))
                {
                    // Open web streams natively in browser
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = track.PathOrUrl,
                        UseShellExecute = true
                    });
                    TextOverlay.Show($"🌐 Opening web stream: {track.Title}", 3000);
                    RenderTracksList();
                    return;
                }

                // Play local MP3 file using WPF MediaPlayer
                if (!File.Exists(track.PathOrUrl))
                {
                    TextOverlay.Show($"⚠️ Media File Not Found:\n{Path.GetFileName(track.PathOrUrl)}", 3500);
                    return;
                }

                _mediaPlayer.Stop();
                _mediaPlayer.Close();

                Uri targetUri = new Uri(Path.GetFullPath(track.PathOrUrl), UriKind.Absolute);

                _mediaPlayer.Open(targetUri);
                _mediaPlayer.Play();
                _isPlaying = true;
                _playPauseBtn.Content = "⏸️ Pause";
                _playTimer.Start();

                RenderTracksList();
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Playback failed: {ex.Message}", 3000);
            }
        }

        private void TogglePlayPause()
        {
            if (_currentTrack == null)
            {
                if (_activeFolder != null && _activeFolder.Tracks.Count > 0)
                {
                    PlayTrack(_activeFolder.Tracks[0]);
                }
                return;
            }

            if (_isPlaying)
            {
                _mediaPlayer.Pause();
                _isPlaying = false;
                _playPauseBtn.Content = "▶️ Play";
            }
            else
            {
                _mediaPlayer.Play();
                _isPlaying = true;
                _playPauseBtn.Content = "⏸️ Pause";
            }
        }

        private void PlayNextTrack()
        {
            if (_activeFolder == null || _activeFolder.Tracks.Count == 0) return;
            int index = _activeFolder.Tracks.FindIndex(t => t.Id == _currentTrack?.Id);
            int nextIndex = (index + 1) % _activeFolder.Tracks.Count;
            PlayTrack(_activeFolder.Tracks[nextIndex]);
        }

        private void PlayPrevTrack()
        {
            if (_activeFolder == null || _activeFolder.Tracks.Count == 0) return;
            int index = _activeFolder.Tracks.FindIndex(t => t.Id == _currentTrack?.Id);
            int prevIndex = index <= 0 ? _activeFolder.Tracks.Count - 1 : index - 1;
            PlayTrack(_activeFolder.Tracks[prevIndex]);
        }

        private void PlayTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isDraggingSlider && _mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                double total = _mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                double current = _mediaPlayer.Position.TotalSeconds;
                if (total > 0)
                {
                    _positionSlider.Value = (current / total) * 100.0;
                    _timeLabel.Text = $"{_mediaPlayer.Position:mm\\:ss} / {_mediaPlayer.NaturalDuration.TimeSpan:mm\\:ss}";
                }
            }
        }

        private void CreateNewFolderPrompt()
        {
            InputPromptOverlay.Show("Enter new Playlist Folder name:", (name) =>
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    var newF = new MusicFolder { FolderName = name.Trim() };
                    _library.Folders.Add(newF);
                    _activeFolder = newF;
                    _library.LastActiveFolderId = newF.Id;
                    MusicPlaylistManager.SaveLibrary(_library);
                    RefreshFoldersDropDown();
                    RenderTracksList();
                    TextOverlay.Show($"📁 Created Playlist Folder: '{name}'", 2500);
                }
            });
        }

        private void BrowseAndAddFile()
        {
            if (_activeFolder == null) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Music Audio File",
                Filter = "Audio Files (*.mp3;*.wav;*.flac;*.m4a;*.ogg;*.wma)|*.mp3;*.wav;*.flac;*.m4a;*.ogg;*.wma|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                string fn = Path.GetFileNameWithoutExtension(dialog.FileName);
                var track = new MusicTrack
                {
                    Title = fn,
                    PathOrUrl = dialog.FileName,
                    IsStreamUrl = false
                };
                _activeFolder.Tracks.Add(track);
                MusicPlaylistManager.SaveLibrary(_library);
                RenderTracksList();
                TextOverlay.Show($"🎵 Added track '{fn}'!", 2500);
            }
        }

        private void AddUrlStreamPrompt()
        {
            if (_activeFolder == null) return;

            InputPromptOverlay.Show("Enter Audio Link / YouTube / Soundcloud URL:", (input) =>
            {
                if (!string.IsNullOrWhiteSpace(input))
                {
                    string url = input.Trim();
                    if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                    {
                        url = "https://" + url;
                    }

                    TextOverlay.Show("📥 Downloading & Converting MP3...", 4000);

                    System.Threading.Tasks.Task.Run(async () =>
                    {
                        try
                        {
                            string musicDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Music", _activeFolder.FolderName);
                            if (!Directory.Exists(musicDir)) Directory.CreateDirectory(musicDir);

                            string outputTemplate = Path.Combine(musicDir, "%(title)s.%(ext)s");

                            // Use yt-dlp to download and convert to valid playable MP3
                            var psi = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "yt-dlp",
                                Arguments = $"-x --audio-format mp3 -o \"{outputTemplate}\" \"{url}\"",
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            };

                            using var proc = System.Diagnostics.Process.Start(psi);
                            if (proc != null)
                            {
                                await proc.WaitForExitAsync();

                                var downloadedFiles = Directory.GetFiles(musicDir, "*.mp3");
                                string? newestMp3 = downloadedFiles.OrderByDescending(File.GetLastWriteTime).FirstOrDefault();

                                if (!string.IsNullOrEmpty(newestMp3) && File.Exists(newestMp3))
                                {
                                    string songTitle = Path.GetFileNameWithoutExtension(newestMp3);

                                    var track = new MusicTrack
                                    {
                                        Title = songTitle,
                                        PathOrUrl = newestMp3,
                                        IsStreamUrl = false
                                    };

                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        _activeFolder.Tracks.Add(track);
                                        MusicPlaylistManager.SaveLibrary(_library);
                                        RenderTracksList();
                                        TextOverlay.Show($"✅ Downloaded MP3: '{songTitle}'!", 3000);
                                    });
                                    return;
                                }
                            }

                            // Fallback to direct HTTP download if yt-dlp is not present
                            string fileName = "Track_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mp3";
                            string localPath = Path.Combine(musicDir, fileName);

                            using var client = new System.Net.Http.HttpClient();
                            byte[] audioBytes = await client.GetByteArrayAsync(url);
                            File.WriteAllBytes(localPath, audioBytes);

                            string directTitle = Path.GetFileNameWithoutExtension(localPath);
                            var directTrack = new MusicTrack
                            {
                                Title = directTitle,
                                PathOrUrl = localPath,
                                IsStreamUrl = false
                            };

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _activeFolder.Tracks.Add(directTrack);
                                MusicPlaylistManager.SaveLibrary(_library);
                                RenderTracksList();
                                TextOverlay.Show($"✅ Downloaded Direct Audio: '{directTitle}'!", 3000);
                            });
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                string streamTitle = url;
                                try { streamTitle = new Uri(url).Host; } catch { }
                                var streamTrack = new MusicTrack
                                {
                                    Title = $"Stream ({streamTitle})",
                                    PathOrUrl = url,
                                    IsStreamUrl = true
                                };
                                _activeFolder.Tracks.Add(streamTrack);
                                MusicPlaylistManager.SaveLibrary(_library);
                                RenderTracksList();
                                TextOverlay.Show($"🔗 Added Stream Link ({ex.Message})", 3000);
                            });
                        }
                    });
                }
            });
        }

        private Button CreateButton(string content, RoutedEventHandler onClick)
        {
            var btn = new Button
            {
                Content = content,
                Margin = new Thickness(4, 0, 4, 0),
                Padding = new Thickness(10, 4, 10, 4),
                Cursor = Cursors.Hand,
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI")
            };
            btn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            btn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            btn.Click += onClick;
            return btn;
        }
    }
}
