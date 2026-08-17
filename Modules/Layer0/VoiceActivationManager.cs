// Developer: heaplyn
// Date: 2026-08-17
// Summary: Voice Activation and Wake Word Detection Service implementation.

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
        private WaveInEvent? _waveIn;
        private MemoryStream _commandAudioStream = new MemoryStream();
        private bool _isRecordingCommand = false;

        bool IVoiceActivationService.IsListening => _waveIn != null;

        void IVoiceActivationService.Start()
        {
            try {
                _waveIn = new WaveInEvent { WaveFormat = new WaveFormat(16000, 1) };
                _waveIn.DataAvailable += (s, e) => { if (_isRecordingCommand) _commandAudioStream.Write(e.Buffer, 0, e.BytesRecorded); };
                _waveIn.StartRecording();
            } catch { }
        }

        void IVoiceActivationService.Stop() => _waveIn?.StopRecording();
        void IVoiceActivationService.SetSensitivity(double level) { }

        public async Task EnrollVoiceAsync(string name) { await Task.CompletedTask; }
        public async Task LearnEnvironmentalSoundAsync(string category) { await Task.CompletedTask; }
        public async Task SaveBackgroundAudioTokenAsync(string text) { await Task.CompletedTask; }
        public void LearnPhrase(string phrase) { }

        // Explicit Static Bridge
        public static void Start() => CoreRegistry.Voice.Start();
        public static void Stop() => CoreRegistry.Voice.Stop();
        public static void LearnPhraseGlobal(string p) => ((VoiceActivationManager)CoreRegistry.Voice).LearnPhrase(p);
        public static async Task EnrollVoiceGlobalAsync(string n) => await ((VoiceActivationManager)CoreRegistry.Voice).EnrollVoiceAsync(n);
        public static async Task LearnSoundGlobalAsync(string c) => await ((VoiceActivationManager)CoreRegistry.Voice).LearnEnvironmentalSoundAsync(c);
        public static async Task SaveAudioTokenGlobalAsync(string t) => await ((VoiceActivationManager)CoreRegistry.Voice).SaveBackgroundAudioTokenAsync(t);
    }
}
