// Developer: heaplyn
// Date: 2026-08-08
// Summary: Houses Win32 P/Invoke signatures and constants for hotkeys, window focus control, and system locking.

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace JarvisLauncher
{
    internal static class NativeMethods
    {
        // Global Hotkey Constants
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_NONE = 0x0000;
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool LockWorkStation();

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        public const uint KEYEVENTF_KEYUP = 0x0002;

        public static void SendKeyCombo(byte modifierVk, byte keyVk)
        {
            try
            {
                keybd_event(modifierVk, 0, 0, UIntPtr.Zero);
                keybd_event(keyVk, 0, 0, UIntPtr.Zero);
                Thread.Sleep(50);
                keybd_event(keyVk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(modifierVk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch { }
        }

        // Memory structure for GlobalMemoryStatusEx
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetSystemTimes(
            out System.Runtime.InteropServices.ComTypes.FILETIME lpIdleTime,
            out System.Runtime.InteropServices.ComTypes.FILETIME lpKernelTime,
            out System.Runtime.InteropServices.ComTypes.FILETIME lpUserTime);

        public static void Restart()
        {
            string projectRoot = @"C:\Users\Kyle\Downloads\Projects\Jarvis";

            // Dynamically search upwards for the project folder containing the .csproj file
            string checkDir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 5; i++)
            {
                if (System.IO.File.Exists(System.IO.Path.Combine(checkDir, "JarvisLauncher.csproj")))
                {
                    projectRoot = checkDir;
                    break;
                }
                var parent = System.IO.Directory.GetParent(checkDir);
                if (parent == null) break;
                checkDir = parent.FullName;
            }

            try
            {
                // Launch a new command prompt to build and run the app
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c cd /d \"{projectRoot}\" && dotnet run",
                    CreateNoWindow = true, // Hide the console window entirely
                    UseShellExecute = false
                });
                
                // Terminate current process immediately to free hotkeys and resources
                Environment.Exit(0);
            }
            catch
            {
                // Fallback to launching the built executable directly if cmd fails
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true
                    });
                    Environment.Exit(0);
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        public static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

        public const uint SHERB_NOCONFIRMATION = 0x00000001;
        public const uint SHERB_NOPROGRESSUI = 0x00000002;
        public const uint SHERB_NOSOUND = 0x00000004;
    }
}
