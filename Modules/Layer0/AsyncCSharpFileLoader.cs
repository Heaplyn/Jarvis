// Developer: GitHub Copilot
// Date: 2026-08-11
// Summary: Lazily loads C# file structure and optionally compiles methods on demand using Roslyn.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace JarvisLauncher
{
    public sealed class AsyncCSharpFileLoader
    {
        private readonly Dictionary<string, Assembly> _assemblyCache = new();

        public async Task<FileOutline> LoadFileOutlineAsync(string filePath, CancellationToken cancellationToken = default)
        {
            string text = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            var tree = CSharpSyntaxTree.ParseText(text, path: filePath);
            return ParseOutline(tree, filePath);
        }

        public async Task<Assembly> CompileFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (_assemblyCache.TryGetValue(filePath, out var cached))
            {
                return cached;
            }

            string sourceText = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, path: filePath);

            var compilation = CSharpCompilation.Create(
                assemblyName: Path.GetFileNameWithoutExtension(filePath) + "_" + Guid.NewGuid(),
                syntaxTrees: new[] { syntaxTree },
                references: GetMetadataReferences(),
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);
            if (!result.Success)
            {
                var errors = string.Join(Environment.NewLine, result.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString()));
                throw new InvalidOperationException($"Compilation failed for {filePath}:\n{errors}");
            }

            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());
            _assemblyCache[filePath] = assembly;
            return assembly;
        }

        public async Task<object?> InvokeMethodAsync(
            string filePath,
            string typeName,
            string methodName,
            object?[]? arguments = null,
            CancellationToken cancellationToken = default)
        {
            var assembly = await CompileFileAsync(filePath, cancellationToken).ConfigureAwait(false);
            var type = assembly.GetType(typeName) ?? throw new InvalidOperationException($"Type '{typeName}' not found.");
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Method '{methodName}' not found on '{typeName}'.");

            object? instance = method.IsStatic ? null : Activator.CreateInstance(type);
            return method.Invoke(instance, arguments);
        }

        private static FileOutline ParseOutline(SyntaxTree syntaxTree, string filePath)
        {
            var root = syntaxTree.GetCompilationUnitRoot();
            var typeNodes = root.DescendantNodes().OfType<TypeDeclarationSyntax>();
            var types = new List<TypeOutline>();

            foreach (var typeNode in typeNodes)
            {
                var typeName = typeNode.Identifier.Text;
                var kind = typeNode.Kind().ToString().Replace("Declaration", string.Empty);
                var members = new List<MethodOutline>();

                foreach (var methodNode in typeNode.Members.OfType<BaseMethodDeclarationSyntax>())
                {
                    string methodName = methodNode switch
                    {
                        MethodDeclarationSyntax m => m.Identifier.Text,
                        ConstructorDeclarationSyntax c => c.Identifier.Text,
                        _ => methodNode.Kind().ToString()
                    };

                    string returnType = methodNode switch
                    {
                        MethodDeclarationSyntax m => m.ReturnType.ToString(),
                        ConstructorDeclarationSyntax _ => "void",
                        _ => "unknown"
                    };

                    var parameters = new List<ParameterOutline>();
                    if (methodNode is BaseMethodDeclarationSyntax method)
                    {
                        foreach (var parameter in method.ParameterList.Parameters)
                        {
                            parameters.Add(new ParameterOutline(parameter.Identifier.Text, parameter.Type?.ToString() ?? "var"));
                        }
                    }

                    int lineNumber = methodNode.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    members.Add(new MethodOutline(methodName, returnType, parameters, lineNumber));
                }

                types.Add(new TypeOutline(typeName, kind, members));
            }

            return new FileOutline(Path.GetFullPath(filePath), types);
        }

        private static IEnumerable<MetadataReference> GetMetadataReferences()
        {
            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .ToList();

            var objectAssemblyPath = typeof(object).Assembly.Location;
            if (!allAssemblies.Any(r => ((PortableExecutableReference)r).FilePath == objectAssemblyPath))
            {
                allAssemblies.Add(MetadataReference.CreateFromFile(objectAssemblyPath));
            }

            return allAssemblies;
        }
    }

    public sealed record FileOutline(string FilePath, IReadOnlyList<TypeOutline> Types);
    public sealed record TypeOutline(string Name, string Kind, IReadOnlyList<MethodOutline> Methods);
    public sealed record MethodOutline(string Name, string ReturnType, IReadOnlyList<ParameterOutline> Parameters, int LineNumber);
    public sealed record ParameterOutline(string Name, string Type);
}
