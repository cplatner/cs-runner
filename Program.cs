using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsRunner
{
    public class CsRunner
    {
        public static void Main(string[] args)
        {
            if (args.Length == 0) {
                // print help
                Console.Error.WriteLine("cs [command] [file]");
                Environment.Exit(1);
            }

            string command = args[0];
            switch (command) {
                case "run":
                    if (args.Length >= 2) {
                        string csharpSourceFile = args[1];
                        RunCsharpFile(csharpSourceFile);
                    }
                    break;
                case "compile":
                    break;
            }
        }

        private static void RunCsharpFile(string file)
        {
            string compiledName = Path.GetFileNameWithoutExtension(file);

            var syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));

            //* Need to get the static Main() method here
            var methods = syntaxTree.GetRoot().DescendantNodes()
                .Where(node => node is MethodDeclarationSyntax && ((MethodDeclarationSyntax)node).Identifier.ValueText == "Main")
                .ToList();

            var mainMethod = methods.FirstOrDefault();
            string mainClassName = ((ClassDeclarationSyntax)mainMethod.Parent).Identifier.ValueText;

            bool hasMain = methods.Count() == 1;

            var classnames = syntaxTree.GetRoot().DescendantNodes()
                .Where(node => node is ClassDeclarationSyntax)
                .Select(node => ((ClassDeclarationSyntax)node).Identifier.ValueText)
                .ToList();

            if (!hasMain || mainClassName == null) {
                Console.Error.WriteLine("No Main() or no classname");
                return;
            }

            var references = GetReferencesFromUsings(syntaxTree);

            var options = new CSharpCompilationOptions(OutputKind.ConsoleApplication);
            var compilation = CSharpCompilation.Create(compiledName, new[] { syntaxTree }, references, options);
            var model = compilation.GetSemanticModel(syntaxTree);
            var classSymbol = model.GetDeclaredSymbol(((ClassDeclarationSyntax)mainMethod.Parent));
            string qualifiedType = classSymbol.OriginalDefinition.ToString();

            using (var ms = new MemoryStream()) {
                var result = compilation.Emit(ms);

                if (!result.Success) {
                    var failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    foreach (var diagnostic in failures) {
                        Console.Error.WriteLine("{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
                    }
                }
                else {
                    ms.Seek(0, SeekOrigin.Begin);
                    var assembly = Assembly.Load(ms.ToArray());
                    var type = assembly.GetType(qualifiedType);

                    object obj = Activator.CreateInstance(type);
                    type.InvokeMember("Main",
                        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod,
                        null,
                        obj,
                        Array.Empty<string>());
                }
            }
        }

        private static List<MetadataReference> GetReferencesFromUsings(SyntaxTree syntaxTree)
        {
            //* Need to get all of the usings here
            var usings = syntaxTree.GetRoot().DescendantNodes()
                .Where(node => node is UsingDirectiveSyntax)
                .Select(node => ((UsingDirectiveSyntax)node).Name.ToString())
                .ToList();

            //* Should be only the referenced usings, plus mscorlib
            var references = new List<MetadataReference>();

            //* Always load mscorlib (System)
            var coreAssembly = Assembly.Load("mscorlib.dll");
            references.Add(MetadataReference.CreateFromFile(coreAssembly.Location));
            string codebase = Path.GetDirectoryName(coreAssembly.Location);

            foreach (var u in usings) {
                if (u != "System") {
                    try {
                        //* Some references are to classes that don't match 
                        references.Add(MetadataReference.CreateFromFile(Path.Combine(codebase, u + ".dll")));
                    }
                    catch { }
                }
            }

            return references;
        }
    }
}
