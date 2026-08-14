// Developer: heaplyn
// Date: 2026-08-10
// Summary: Handles CLI commands to execute FFmpeg video/audio conversions, MP3 extraction, GIF creation, video compression, and trimming.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace JarvisLauncher
{
    public class FFMpegCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            string q = query.Trim().ToLower();
            if (string.IsNullOrEmpty(q)) return false;

            return q.StartsWith("ffmpeg") || q.StartsWith("convert") || q.Contains("to")
                || q == "webp2png" || q == "gif2mp4" || q == "png2webp" || q == "mp42gif" || q == "mp32wav"
                || q == "mediaconvert" || q == "convertmedia";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            string trimmed = query.Trim();
            string lower = trimmed.ToLower();
            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string firstWord = parts.Length > 0 ? parts[0].ToLower() : "";

            double similarity = 3.5;

            // Universal Media Converter Studio Trigger
            if (lower == "mediaconvert" || lower == "convertmedia" || lower == "convert" || lower.Contains("media conversion"))
            {
                suggestions.Add(new CommandResult
                {
                    Title = "⚡ Open Universal Media Converter Studio",
                    Description = "Convert WEBP to PNG, GIF to MP4, MP4 to GIF, PNG to WEBP, MP3 to WAV",
                    Similarity = 5.0,
                    Execute = () => MediaConverterOverlay.ShowOverlay()
                });
            }

            // WEBP to PNG
            if (lower.Contains("webp to png") || lower.Contains("webp2png") || lower.Contains("convert webp"))
            {
                suggestions.Add(new CommandResult
                {
                    Title = "🖼️ Convert WEBP Image ➔ PNG",
                    Description = "Open Media Converter for WEBP ➔ PNG lossless format",
                    Similarity = 4.5,
                    Execute = () => MediaConverterOverlay.ShowOverlay(defaultTargetFormat: "png")
                });
            }

            // GIF to MP4
            if (lower.Contains("gif to mp4") || lower.Contains("gif2mp4") || lower.Contains("convert gif"))
            {
                suggestions.Add(new CommandResult
                {
                    Title = "🎞️ Convert Animated GIF ➔ MP4 Video",
                    Description = "Convert GIF animations to compressed H.264 MP4 videos",
                    Similarity = 4.5,
                    Execute = () => MediaConverterOverlay.ShowOverlay(defaultTargetFormat: "mp4")
                });
            }

            // MP4 to GIF
            if (lower.Contains("mp4 to gif") || lower.Contains("mp42gif"))
            {
                suggestions.Add(new CommandResult
                {
                    Title = "🎬 Convert MP4 Video ➔ Animated GIF",
                    Description = "Create animated GIF clips from MP4 video files",
                    Similarity = 4.5,
                    Execute = () => MediaConverterOverlay.ShowOverlay(defaultTargetFormat: "gif")
                });
            }

            // PNG to WEBP
            if (lower.Contains("png to webp") || lower.Contains("png2webp"))
            {
                suggestions.Add(new CommandResult
                {
                    Title = "🌐 Convert PNG Image ➔ WEBP",
                    Description = "Optimize PNG images into compact WEBP web format",
                    Similarity = 4.5,
                    Execute = () => MediaConverterOverlay.ShowOverlay(defaultTargetFormat: "webp")
                });
            }

            // MP3 to WAV
            if (lower.Contains("mp3 to wav") || lower.Contains("mp32wav"))
            {
                suggestions.Add(new CommandResult
                {
                    Title = "🎵 Convert MP3 Audio ➔ Uncompressed WAV",
                    Description = "Convert MP3 files into 16-bit 44.1kHz PCM WAV audio",
                    Similarity = 4.5,
                    Execute = () => MediaConverterOverlay.ShowOverlay(defaultTargetFormat: "wav")
                });
            }

            if ("ffmpeg".StartsWith(firstWord) || "convert".StartsWith(firstWord) || lower == "ffmpeg" || lower == "convert")
            {
                suggestions.Add(new CommandResult
                {
                    Title = "🎵 FFmpeg: Extract MP3 Audio...",
                    Description = "Select a video/audio file to extract 192k MP3 audio track",
                    Similarity = similarity + 0.5,
                    Execute = InteractiveExtractMp3
                });

                suggestions.Add(new CommandResult
                {
                    Title = "🎞️ FFmpeg: Convert Video to Animated GIF...",
                    Description = "Select a video file to convert to high quality animated GIF",
                    Similarity = similarity + 0.4,
                    Execute = InteractiveConvertToGif
                });

                suggestions.Add(new CommandResult
                {
                    Title = "📉 FFmpeg: Compress Video File Size...",
                    Description = "Select a video file to compress using H.264 (CRF 28)",
                    Similarity = similarity + 0.3,
                    Execute = InteractiveCompressVideo
                });

                suggestions.Add(new CommandResult
                {
                    Title = "🔇 FFmpeg: Mute Video (Remove Audio)...",
                    Description = "Select a video file to strip its audio stream",
                    Similarity = similarity + 0.2,
                    Execute = InteractiveMuteVideo
                });

                suggestions.Add(new CommandResult
                {
                    Title = "🔄 FFmpeg: Convert Media Format...",
                    Description = "Select input file and output format to convert",
                    Similarity = similarity + 0.1,
                    Execute = InteractiveConvertFormat
                });

                return suggestions;
            }

            // "ffmpeg mp3 [path]"
            if (lower.StartsWith("ffmpeg mp3") || lower.StartsWith("mp3 "))
            {
                string target = parts.Length > 2 ? trimmed.Substring(trimmed.IndexOf("mp3", StringComparison.OrdinalIgnoreCase) + 3).Trim().Trim('"', '\'') : "";
                if (!string.IsNullOrEmpty(target) && File.Exists(target))
                {
                    string output = Path.ChangeExtension(target, ".mp3");
                    suggestions.Add(new CommandResult
                    {
                        Title = $"🎵 Extract MP3: {Path.GetFileName(target)}",
                        Description = $"Save to {Path.GetFileName(output)}",
                        Similarity = 3.0,
                        Execute = () => ExecuteExtractMp3(target, output)
                    });
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        Title = "🎵 Extract MP3 Audio...",
                        Description = "Pick file to extract MP3",
                        Similarity = 2.8,
                        Execute = InteractiveExtractMp3
                    });
                }
                return suggestions;
            }

            // "ffmpeg gif [path]"
            if (lower.StartsWith("ffmpeg gif"))
            {
                string target = parts.Length > 2 ? trimmed.Substring(11).Trim().Trim('"', '\'') : "";
                if (!string.IsNullOrEmpty(target) && File.Exists(target))
                {
                    string output = Path.ChangeExtension(target, ".gif");
                    suggestions.Add(new CommandResult
                    {
                        Title = $"🎞️ Convert GIF: {Path.GetFileName(target)}",
                        Description = $"Save to {Path.GetFileName(output)}",
                        Similarity = 3.0,
                        Execute = () => ExecuteConvertToGif(target, output)
                    });
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        Title = "🎞️ Convert Video to GIF...",
                        Description = "Pick video file to convert to GIF",
                        Similarity = 2.8,
                        Execute = InteractiveConvertToGif
                    });
                }
                return suggestions;
            }

            // "ffmpeg compress [path]"
            if (lower.StartsWith("ffmpeg compress"))
            {
                string target = parts.Length > 2 ? trimmed.Substring(16).Trim().Trim('"', '\'') : "";
                if (!string.IsNullOrEmpty(target) && File.Exists(target))
                {
                    string dir = Path.GetDirectoryName(target) ?? "";
                    string fileName = Path.GetFileNameWithoutExtension(target);
                    string ext = Path.GetExtension(target);
                    string output = Path.Combine(dir, $"{fileName}_compressed{ext}");

                    suggestions.Add(new CommandResult
                    {
                        Title = $"📉 Compress Video: {Path.GetFileName(target)}",
                        Description = $"Save to {Path.GetFileName(output)}",
                        Similarity = 3.0,
                        Execute = () => ExecuteCompressVideo(target, output)
                    });
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        Title = "📉 Compress Video File Size...",
                        Description = "Pick video file to compress",
                        Similarity = 2.8,
                        Execute = InteractiveCompressVideo
                    });
                }
                return suggestions;
            }

            // "ffmpeg mute [path]"
            if (lower.StartsWith("ffmpeg mute"))
            {
                string target = parts.Length > 2 ? trimmed.Substring(12).Trim().Trim('"', '\'') : "";
                if (!string.IsNullOrEmpty(target) && File.Exists(target))
                {
                    string dir = Path.GetDirectoryName(target) ?? "";
                    string fileName = Path.GetFileNameWithoutExtension(target);
                    string ext = Path.GetExtension(target);
                    string output = Path.Combine(dir, $"{fileName}_muted{ext}");

                    suggestions.Add(new CommandResult
                    {
                        Title = $"🔇 Mute Video: {Path.GetFileName(target)}",
                        Description = $"Save to {Path.GetFileName(output)}",
                        Similarity = 3.0,
                        Execute = () => ExecuteMuteVideo(target, output)
                    });
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        Title = "🔇 Mute Video...",
                        Description = "Pick video file to remove audio stream",
                        Similarity = 2.8,
                        Execute = InteractiveMuteVideo
                    });
                }
                return suggestions;
            }

            // "ffmpeg convert <in> <out>" or "ffmpeg <raw_args>"
            if (lower.StartsWith("ffmpeg "))
            {
                string rawArgs = trimmed.Substring(7).Trim();
                suggestions.Add(new CommandResult
                {
                    Title = $"🎬 Execute FFmpeg Command: ffmpeg {rawArgs}",
                    Description = "Run custom FFmpeg parameters",
                    Similarity = similarity,
                    Execute = () => RunFFmpegCommandAsync(rawArgs, rawArgs)
                });
            }

            return suggestions;
        }

        private static void InteractiveExtractMp3()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Video/Audio File to Extract MP3",
                Filter = "Media Files (*.mp4;*.mov;*.mkv;*.avi;*.webm;*.wav;*.flac;*.m4a)|*.mp4;*.mov;*.mkv;*.avi;*.webm;*.wav;*.flac;*.m4a|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                string input = dlg.FileName;
                string output = Path.ChangeExtension(input, ".mp3");
                ExecuteExtractMp3(input, output);
            }
        }

        private static void ExecuteExtractMp3(string input, string output)
        {
            _ = RunFFmpegCommandAsync($"-i \"{input}\" -vn -ar 44100 -ac 2 -b:a 192k \"{output}\" -y", $"Extract MP3: {Path.GetFileName(input)}");
        }

        private static void InteractiveConvertToGif()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Video File to Convert to GIF",
                Filter = "Video Files (*.mp4;*.mov;*.mkv;*.avi;*.webm)|*.mp4;*.mov;*.mkv;*.avi;*.webm|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                string input = dlg.FileName;
                string output = Path.ChangeExtension(input, ".gif");
                ExecuteConvertToGif(input, output);
            }
        }

        private static void ExecuteConvertToGif(string input, string output)
        {
            _ = RunFFmpegCommandAsync($"-i \"{input}\" -vf \"fps=15,scale=480:-1:flags=lanczos\" \"{output}\" -y", $"Convert to GIF: {Path.GetFileName(input)}");
        }

        private static void InteractiveCompressVideo()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Video File to Compress",
                Filter = "Video Files (*.mp4;*.mov;*.mkv;*.avi;*.webm)|*.mp4;*.mov;*.mkv;*.avi;*.webm|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                string input = dlg.FileName;
                string dir = Path.GetDirectoryName(input) ?? "";
                string fileName = Path.GetFileNameWithoutExtension(input);
                string ext = Path.GetExtension(input);
                string output = Path.Combine(dir, $"{fileName}_compressed{ext}");
                ExecuteCompressVideo(input, output);
            }
        }

        private static void ExecuteCompressVideo(string input, string output)
        {
            _ = RunFFmpegCommandAsync($"-i \"{input}\" -vcodec libx264 -crf 28 \"{output}\" -y", $"Compress Video: {Path.GetFileName(input)}");
        }

        private static void InteractiveMuteVideo()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Video File to Mute (Remove Audio)",
                Filter = "Video Files (*.mp4;*.mov;*.mkv;*.avi;*.webm)|*.mp4;*.mov;*.mkv;*.avi;*.webm|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                string input = dlg.FileName;
                string dir = Path.GetDirectoryName(input) ?? "";
                string fileName = Path.GetFileNameWithoutExtension(input);
                string ext = Path.GetExtension(input);
                string output = Path.Combine(dir, $"{fileName}_muted{ext}");
                ExecuteMuteVideo(input, output);
            }
        }

        private static void ExecuteMuteVideo(string input, string output)
        {
            _ = RunFFmpegCommandAsync($"-i \"{input}\" -an -vcodec copy \"{output}\" -y", $"Mute Video: {Path.GetFileName(input)}");
        }

        private static void InteractiveConvertFormat()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select File to Convert Format",
                Filter = "All Media Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                string input = dlg.FileName;
                var saveDlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Save Converted Output File As",
                    FileName = Path.GetFileNameWithoutExtension(input),
                    Filter = "MP4 Video (*.mp4)|*.mp4|MP3 Audio (*.mp3)|*.mp3|WAV Audio (*.wav)|*.wav|GIF Animation (*.gif)|*.gif|All Files (*.*)|*.*"
                };

                if (saveDlg.ShowDialog() == true)
                {
                    string output = saveDlg.FileName;
                    _ = RunFFmpegCommandAsync($"-i \"{input}\" \"{output}\" -y", $"Convert Format: {Path.GetFileName(input)} → {Path.GetFileName(output)}");
                }
            }
        }

        private static async Task RunFFmpegCommandAsync(string arguments, string title)
        {
            TextOverlay.Show($"🎬 FFmpeg: {title}...", 2500);

            await Task.Run(async () =>
            {
                var output = new StringBuilder();
                var errors = new StringBuilder();

                var psi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                try
                {
                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        proc.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) errors.AppendLine(e.Data); };

                        proc.BeginOutputReadLine();
                        proc.BeginErrorReadLine();
                        
                        bool exited = proc.WaitForExit(60000);
                        if (!exited)
                        {
                            proc.Kill();
                        }

                        string result = (output.ToString() + "\n" + errors.ToString()).Trim();
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            CliOutputOverlay.Show($"FFmpeg - {title}", string.IsNullOrWhiteSpace(result) ? "Command completed with no output." : result);
                        });
                    }
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CliOutputOverlay.Show($"FFmpeg Error - {title}", $"Failed to run FFmpeg: {ex.Message}\n\nTip: Make sure FFmpeg is installed on your PC or available in PATH.");
                    });
                }
            });
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("ffmpeg", "Open FFmpeg multimedia processing menu", "ffmpeg"),
                new CommandDesc("ffmpeg mp3 [file]", "Extract MP3 audio track from video/audio file", "ffmpeg mp3 clip.mp4"),
                new CommandDesc("ffmpeg gif [file]", "Convert video clip to animated GIF", "ffmpeg gif clip.mp4"),
                new CommandDesc("ffmpeg compress [file]", "Compress video file size (H.264)", "ffmpeg compress clip.mp4"),
                new CommandDesc("ffmpeg mute [file]", "Remove audio stream from video file", "ffmpeg mute clip.mp4"),
                new CommandDesc("ffmpeg <custom_args>", "Execute custom FFmpeg CLI commands", "ffmpeg -i input.mp4 output.mp3")
            };
        }
    }
}
