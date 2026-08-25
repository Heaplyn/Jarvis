// Developer: heaplyn
// Date: 2026-08-16
// Summary: Core Interface for Jarvis dynamic plugins.

using System;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public interface IJarvisPlugin
    {
        string PluginName { get; }
        string Description { get; }
        string Author { get; }
        Version Version { get; }

        /// <summary>
        /// Called when the plugin is first loaded.
        /// </summary>
        void OnInitialize();

        /// <summary>
        /// Called when Jarvis is shutting down.
        /// </summary>
        void OnShutdown();

        /// <summary>
        /// Allows the plugin to register custom command keywords.
        /// </summary>
        IEnumerable<CommandDesc> GetPluginCommands();
    }
}
