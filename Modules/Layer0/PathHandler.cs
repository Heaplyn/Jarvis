using System;
using System.IO;
using System.Runtime.CompilerServices;
namespace JarvisLauncher; 

public class PathHandler
{
/// <summary>
/// Returns the directory path of the source code file where this method is called.
/// </summary>
public string GetCurrentSourceDirectory([CallerFilePath] string callerPath = "")
{
    return Path.GetDirectoryName(callerPath) ?? string.Empty;
}

}