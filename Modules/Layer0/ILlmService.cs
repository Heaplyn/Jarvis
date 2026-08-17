// Developer: heaplyn
// Date: 2026-08-17
// Summary: Interface for Large Language Model operations.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public interface ILlmService
    {
        Task<string> AskAsync(string prompt, List<ChatTurn>? history = null, CancellationToken ct = default);
        Task<bool> IsLocalAvailableAsync();
        Task<List<string>> GetLocalModelsAsync();
    }
}
