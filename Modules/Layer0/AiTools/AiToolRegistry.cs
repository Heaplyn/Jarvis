// Developer: heaplyn
// Date: 2026-08-19
// Summary: Centralized Registry for Static and Dynamic AI Tools.
//          Enables discovery, activation, and hot-loading of new tools.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JarvisLauncher.AiTools
{
    public static class AiToolRegistry
    {
        private static readonly List<IAiTool> _tools = new();
        private static readonly object _lock = new();

        static AiToolRegistry()
        {
            // Register Built-in Tools
            RegisterCoreTools();
        }

        private static void RegisterCoreTools()
        {
            // File Tools
            Register(new ReadFileTool());
            Register(new WriteFileTool());
            Register(new ListFilesTool());
            Register(new ReadBinaryTool());
            Register(new WriteBinaryTool());

            // System & Automation Tools
            Register(new MouseControlTool());
            Register(new KeyboardTool());
            Register(new AppFocusTool());

            // Web Tools (Assuming implementation exists in WebTools.cs)
            // Register(new WebScrapeTool());
        }

        public static void Register(IAiTool tool)
        {
            lock (_lock)
            {
                if (!_tools.Any(t => t.Tag == tool.Tag))
                {
                    _tools.Add(tool);
                }
            }
        }

        public static void Unregister(string tag)
        {
            lock (_lock)
            {
                _tools.RemoveAll(t => t.Tag == tag);
            }
        }

        public static IReadOnlyList<IAiTool> GetAllTools()
        {
            lock (_lock) return _tools.ToList().AsReadOnly();
        }

        public static IAiTool? GetToolByTag(string tag)
        {
            lock (_lock) return _tools.FirstOrDefault(t => t.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));
        }
    }
}
