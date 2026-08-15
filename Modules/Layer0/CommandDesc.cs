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
        public string COMMAND_NAME { get; set; } = string.Empty;
        public string COMMAND_DESCRIPTION { get; set; } = string.Empty;
        public string COMMAND_EXAMPLE { get; set; } = string.Empty;
        public bool SHOW { get; set; } = true;

        public CommandDesc(string Name, string Description, string Example)
        {
            COMMAND_NAME = Name;
            COMMAND_DESCRIPTION = Description;
            COMMAND_EXAMPLE = Example;
        }

        public CommandDesc(string Name, string Description, string Example, bool ShowParam)
        {
            COMMAND_NAME = Name;
            COMMAND_DESCRIPTION = Description;
            COMMAND_EXAMPLE = Example;
            SHOW = ShowParam;
        }

        public CommandDesc(bool ShowParam)
        {
            SHOW = ShowParam;
        }
    }
}