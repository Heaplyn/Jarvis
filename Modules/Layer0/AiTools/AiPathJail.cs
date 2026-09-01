// Developer: heaplyn
// Date: 2026-08-31
// Summary: SECURITY - confines model-driven file tools (@rf/@wf/@rf_b/@wf_b/@ls) to a
//          workspace root so the model cannot read or write arbitrary paths on disk
//          (e.g. C:\Windows, the Startup folder, browser cookies, SSH keys).

using System;
using System.IO;

namespace JarvisLauncher.AiTools
{
    public static class AiPathJail
    {
        // Workspace root the model is allowed to touch. Defaults to the Jarvis install dir.
        public static string Root { get; } =
            Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);

        /// <summary>
        /// Resolves a model-supplied path against the workspace and rejects anything that
        /// escapes the root (via absolute paths, .. traversal, symlinks, etc.).
        /// </summary>
        public static bool TryResolve(string requested, out string fullPath, out string error)
        {
            fullPath = string.Empty;
            error = string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(requested))
                { error = "[ERROR: empty path blocked]\n"; return false; }

                string combined = Path.IsPathRooted(requested)
                    ? requested
                    : Path.Combine(Root, requested);
                string full = Path.GetFullPath(combined);

                string rootWithSep = Root.EndsWith(Path.DirectorySeparatorChar)
                    ? Root : Root + Path.DirectorySeparatorChar;

                if (!string.Equals(full, Root, StringComparison.OrdinalIgnoreCase) &&
                    !full.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"[ERROR: path '{requested}' is outside the AI workspace and was blocked]\n";
                    return false;
                }

                fullPath = full;
                return true;
            }
            catch (Exception ex)
            {
                error = $"[ERROR: invalid path '{requested}': {ex.Message}]\n";
                return false;
            }
        }
    }
}
