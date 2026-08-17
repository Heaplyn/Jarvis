// Developer: heaplyn
// Date: 2026-08-17
// Summary: Interface for scanning and launching system applications.

using System.Collections.Generic;

namespace JarvisLauncher
{
    public interface IAppScannerService
    {
        void StartScan();
        List<AppInfo> GetMatchingApps(string name);
    }
}
