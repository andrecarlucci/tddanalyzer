using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Text.RegularExpressions;
using TddAnalyzer.Frameworks;
using System.Reflection;
using System.IO;
using Microsoft.CodeAnalysis.Emit;
using System.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;

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

        private static string Find(string sourceFilePath, string extension) {
            try {
                var directoryPath = Path.GetDirectoryName(sourceFilePath);
                while (!string.IsNullOrWhiteSpace(directoryPath)) {
                    var projectFilePath = Directory.GetFiles(directoryPath, "*."+ extension, SearchOption.TopDirectoryOnly).FirstOrDefault();
                    if (projectFilePath != null) {
                        return projectFilePath;
                    }
                    var parentDirectory = Directory.GetParent(directoryPath);
                    directoryPath = parentDirectory == null ? null : parentDirectory.FullName;
                }
                return null;
            }
            catch (Exception e) {
                Trace.WriteLine(string.Format("Exception in FindProjectFile({0}, {1}): {2}", sourceFilePath, extension, e));
                return null;
            }
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

            var tree = context.SemanticModel.SyntaxTree;
            var projectFile = Find(tree.FilePath, "*proj");
            var solutionFile = Find(tree.FilePath, "sln");

            if (solutionFile == null || projectFile == null) {
                return;
            }

            var workspace = MSBuildWorkspace.Create();
            var solution = workspace.OpenSolutionAsync(solutionFile).Result;
            Project project = null;
            foreach (var projectId in solution.ProjectIds) {
                project = solution.GetProject(projectId);
                if (project.FilePath == projectFile) {
                    break;
                }
            }
            if (project == null) {
                return;
            }
            




            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
            options = options.WithAllowUnsafe(true);                                //Allow unsafe code;
            options = options.WithOptimizationLevel(OptimizationLevel.Release);     //Set optimization level
            options = options.WithPlatform(Platform.AnyCpu);                           //Set platform

            var Mscorlib = PortableExecutableReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location);
            var compilation = CSharpCompilation.Create("MyCompilation",
                                syntaxTrees: new[] { tree },
                                references: new[] { Mscorlib },
                                options: options);

            using (var ms = new MemoryStream()) {
                var result = compilation.Emit(ms);

                if (!result.Success) {
                    var failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    foreach (Diagnostic diagnostic in failures) {
                        Debug.WriteLine("{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
                    }
                    return;
                }
                ms.Seek(0, SeekOrigin.Begin);
                var assembly = Assembly.Load(ms.ToArray());

                Type type = assembly.GetType("RoslynCompileSample.Writer");
                object obj = Activator.CreateInstance(type);
                type.InvokeMember("Write",
                    BindingFlags.Default | BindingFlags.InvokeMethod,
                    null,
                    obj,
                    new object[] { "Hello World" });
            }



            

            //var mscorlib = MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly);
            
            
            try {
                testFramework.Run(method, semanticModel);
                return;
            }
            catch (Exception ex) {
                if (ex.InnerException != null) {
                    ex = ex.InnerException;
                }
                var message = ex.Message;
                var diag = Diagnostic.Create(Rule, method.GetLocation(), message);
                context.ReportDiagnostic(diag);
            }
        }

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

