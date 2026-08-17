// Developer: heaplyn
// Date: 2026-08-17
// Summary: Interface for voice activation and wake word detection.

namespace JarvisLauncher
{
    public interface IVoiceActivationService
    {
        void Start();
        void Stop();
        bool IsListening { get; }
        void SetSensitivity(double level);
    }
}
