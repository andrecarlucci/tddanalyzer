using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using TddAnalyzer.Frameworks;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using AppDomainToolkit;
using TddAnalyzer.CodeCompilation;
using System.Text;

namespace TddAnalyzer {
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class TddAnalyzerAnalyzer : DiagnosticAnalyzer {
        public const string DiagnosticId = "TddAnalyzer";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Testing";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private static List<TestFramework> TestFrameworks => new List<TestFramework> {
            new Nunit()
        };

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(c => Analyze(c), SyntaxKind.MethodDeclaration);
        }

        private static void Analyze(SyntaxNodeAnalysisContext context) {
            var method = (MethodDeclarationSyntax)context.Node;
            var semanticModel = context.SemanticModel;
            var methodSymbol = semanticModel.GetDeclaredSymbol(method);
            var theClass = methodSymbol.ContainingType;
            if (theClass.TypeKind != TypeKind.Class) return;
            var testFramework = ChooseTestFramework(method);
            if (testFramework == null) {
                return;
            }
            var hashcode = context.SemanticModel.SyntaxTree.GetHashCode();
            var fullMethodName = theClass.ContainingNamespace.Name + "." + theClass.Name + "." + methodSymbol.Name;

            var compiler = new Compiler();
            var compileResult = compiler.Compile(context.SemanticModel.Compilation);

            if (!compileResult.Success) {
                return;
            }

            var mainAssemblyName = context.SemanticModel.Compilation.AssemblyName;

            var path = Path.Combine(Path.GetTempPath(), "tdd");
            Directory.CreateDirectory(path);
            foreach (var dll in compileResult.References) {
                File.WriteAllBytes(Path.Combine(path, dll.Key + ".dll"), dll.Value);
            }

            var errors = new StringBuilder();
            using (var proc = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = @"nunit3-console.exe",
                    Arguments = mainAssemblyName + ".dll --noresult --noheader --test " + fullMethodName,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    WorkingDirectory = path
                }
            }) {
                var keepMessages = false;
                proc.Start();
                while (!proc.StandardOutput.EndOfStream) {
                    var line = proc.StandardOutput.ReadLine();
                    if (line.Contains("Errors and Failures")) {
                        keepMessages = true;
                        continue;
                    }
                    if (line.Contains("Run Settings")) {
                        break;
                    }
                    if (keepMessages) {
                        errors.AppendLine(line);
                    }
                }
            }
            if (errors.Length > 0) {
                var diag = Diagnostic.Create(Rule, method.GetLocation(), errors.ToString());
                context.ReportDiagnostic(diag);
            }



            //var analyzer = Assembly.GetExecutingAssembly();
            //File.Copy(analyzer.Location, Path.Combine(path, Path.GetFileName(analyzer.Location)), true);

            //try {
            //    //Launcher.Invoke(new byte[0]);
            //    IsolatedInvoker.Invoke(Path.Combine(path, mainAssemblyName + ".dll"), theClass.ToString(), methodSymbol.Name, null, compileResult.References.Select(x => x.Value));


            //    //var app = AppDomain.CreateDomain("temp");
            //    //app.AssemblyResolve += (s, a) => {
            //    //    //var name = a.Name;
            //    //    //var index = name.IndexOf(',');
            //    //    //if (index > 0) {
            //    //    //    name = name.Substring(0, index);
            //    //    //}
            //    //    //if (!compileResult.References.ContainsKey(name)) {
            //    //    //    return null;
            //    //    //}
            //    //    //return Assembly.Load(compileResult.References[name]);
            //    //    return null;
            //    //};


            //    //foreach (var dll in compileResult.References) {
            //    //    app.Load(dll.Value);
            //    //}
            //    //var testObject = app.CreateInstanceAndUnwrap(mainAssemblyName, theClass.ToString());
            //    //testFramework.Run(testObject, method, semanticModel);
            //    //return;
            //}
            //catch (Exception ex) {
            //    if (ex.InnerException != null) {
            //        ex = ex.InnerException;
            //    }
            //    var message = ex.Message;
            //    var diag = Diagnostic.Create(Rule, method.GetLocation(), message);
            //    context.ReportDiagnostic(diag);
            //}
        }
    

    //private static Type CompileTheCode(Compilation compilation, string fullClassName) {

    //    foreach (var reference in compilation.References) {
    //        var path = reference.Display;
    //        Assembly assembly = null;
    //        var compilationReference = reference as CompilationReference;
    //        if (compilationReference != null) {
    //            assembly = Compile(compilationReference.Compilation);
    //            if (assembly == null) {
    //                return null;
    //            }
    //        }
    //        else {
    //            assembly = Assembly.LoadFile(path);
    //        }
    //        var name = Path.GetFileNameWithoutExtension(path);
    //        _references.Add(name, assembly);
    //    }
    //    var mainAssembly = Compile(compilation);
    //    if (mainAssembly == null) {
    //        return null;
    //    }
    //    return mainAssembly.GetType(fullClassName);
    //}



    private static TestFramework ChooseTestFramework(MethodDeclarationSyntax method) {
        foreach (var framework in TestFrameworks) {
            if (framework.CanRun(method)) {
                return framework;
            }
        }
        return null;
    }

    private static bool IsTestMethod(MethodDeclarationSyntax method, IMethodSymbol methodSymbol) {
        var result = false;
        // Test if the method has any known test framework's attribute.
        result = method.AttributeLists.HasAnyAttribute(AllTestFrameworksMethodAttributes.Value);

        if (!result && methodSymbol.Name.IndexOf("Test", System.StringComparison.OrdinalIgnoreCase) >= 0) {
            // Test if the containing class has any NUnit class attribute
            result = methodSymbol.ContainingType.GetAttributes().Any(attribute => attribute.AttributeClass.Name == NUnitTestClassAttribute);

            if (!result) {
                // Test if any other method in the containing class has an NUnit method attribute.
                result = method.Parent.DescendantNodes().Any(descendant => descendant.IsKind(SyntaxKind.MethodDeclaration) && ((MethodDeclarationSyntax)descendant).AttributeLists.HasAnyAttribute(NUnitTestMethodAttributes));
            }
        }
        return result;
    }

    internal const string NUnitTestClassAttribute = "TestFixtureAttribute";
    internal static readonly string[] MicrosoftTestMethodAttributes = new string[] { "TestMethod", "ClassInitialize", "ClassCleanup", "TestInitialize", "TestCleanup", "AssemblyInitialize", "AssemblyCleanup" };
    internal static readonly string[] XUnitTestMethodAttributes = new string[] { "Fact", "Theory" };
    internal static readonly string[] NUnitTestMethodAttributes = new string[] { "Test", "TestCase", "TestCaseSource", "TestFixtureSetup", "TestFixtureTeardown", "SetUp", "TearDown", "OneTimeSetUp", "OneTimeTearDown" };
    internal static readonly System.Lazy<string[]> AllTestFrameworksMethodAttributes = new System.Lazy<string[]>(() => { return XUnitTestMethodAttributes.Concat(MicrosoftTestMethodAttributes).Concat(NUnitTestMethodAttributes).ToArray(); });
}
}

