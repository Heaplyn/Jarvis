// Developer: heaplyn
// Date: 2026-08-08
// Summary: Model defining suggestion outcomes with visual metadata (Title, Description), similarity score, and executable action.

using System;

namespace JarvisLauncher
{
    public class CommandResult
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Action? Execute { get; set; }
        public double Similarity { get; set; } = 0.0;
    }
}
