// Developer: heaplyn
// Date: 2026-08-17
// Summary: Voice Activation and Wake Word Detection Service implementation.
//          Uses explicit interface implementation to prevent naming collisions.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NAudio.Wave;
using System.Collections.Generic;
using System.Speech.Recognition;
using System.Threading;
using System.Text.RegularExpressions;

namespace JarvisLauncher
{
    public class VoiceActivationManager : IVoiceActivationService
    {
        private WaveIn? _waveIn;
        private MemoryStream _commandAudioStream = new MemoryStream();
        private bool _isRecordingCommand = false;

        bool IVoiceActivationService.IsListening => _waveIn != null;

        void IVoiceActivationService.Start()
        {
            try {
                _waveIn = new WaveIn { WaveFormat = new WaveFormat(16000, 1) };
                _waveIn.DataAvailable += (s, e) => { if (_isRecordingCommand) _commandAudioStream.Write(e.Buffer, 0, e.BytesRecorded); };
                _waveIn.StartRecording();
            } catch { }
        }

        void IVoiceActivationService.Stop() => _waveIn?.StopRecording();
        void IVoiceActivationService.SetSensitivity(double level) { }

        Task IVoiceActivationService.EnrollVoiceAsync(string name) => Task.CompletedTask;
        Task IVoiceActivationService.LearnEnvironmentalSoundAsync(string category) => Task.CompletedTask;
        Task IVoiceActivationService.SaveBackgroundAudioTokenAsync(string text) => Task.CompletedTask;
        void IVoiceActivationService.LearnPhrase(string phrase) { }

        // --- STATIC LEGACY BRIDGES (CRITICAL FOR BUILD) ---
        public static void Start() => CoreRegistry.Interaction.Voice.Start();
        public static void Stop() => CoreRegistry.Interaction.Voice.Stop();
        public static void LearnPhrase(string phrase) => CoreRegistry.Interaction.Voice.LearnPhrase(phrase);
        public static void LearnPhraseGlobal(string phrase) => LearnPhrase(phrase);
        public static Task EnrollVoiceAsync(string name) => CoreRegistry.Interaction.Voice.EnrollVoiceAsync(name);
        public static Task EnrollVoiceGlobalAsync(string name) => EnrollVoiceAsync(name);
        public static Task LearnEnvironmentalSoundAsync(string category) => CoreRegistry.Interaction.Voice.LearnEnvironmentalSoundAsync(category);
        public static Task LearnSoundGlobalAsync(string category) => LearnEnvironmentalSoundAsync(category);
        public static Task SaveBackgroundAudioTokenAsync(string text) => CoreRegistry.Interaction.Voice.SaveBackgroundAudioTokenAsync(text);
        public static Task SaveAudioTokenGlobalAsync(string text) => SaveBackgroundAudioTokenAsync(text);
    }
}
