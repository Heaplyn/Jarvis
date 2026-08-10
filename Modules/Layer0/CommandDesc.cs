// Developer: heaplyn
// Date: 2026-08-09
// Summary: Background listener and persistent manager for Clipboard History.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace JarvisLauncher
{
    public class CommandDesc
    {
        public string CommandName { get; set; } = string.Empty;
        public string CommandDescription { get; set; } = string.Empty;
        public string CommandExample { get; set; } = string.Empty;
        public bool Show { get; set; } = true;

        public CommandDesc(string name, string description, string example) 
        {
            CommandName = name;
            CommandDescription = description;
            CommandExample = example;
        }

        public CommandDesc(string name, string description, string example, bool show) 
        {
            CommandName = name;
            CommandDescription = description;
            CommandExample = example;
            Show = show;
        }

        public CommandDesc(bool show)
        {
            Show = show;
        }
    }
}