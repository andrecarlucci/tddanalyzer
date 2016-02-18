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

namespace TddAnalyzer {
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class TddAnalyzerAnalyzer : DiagnosticAnalyzer {
        public const string DiagnosticId = "TddAnalyzer";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Testing";

        public static Dictionary<string, string> _references = new Dictionary<string, string>();

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

            var type = CompileTheCode(context, theClass.ToString());

            if (type == null) {
                return;
            }

            try {
                testFramework.Run(type, method, semanticModel);
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

            //var workspace = MSBuildWorkspace.Create();
            //var solution = workspace.OpenSolutionAsync(solutionFile).Result;
            //Project project = null;
            //foreach (var projectId in solution.ProjectIds) {
            //    project = solution.GetProject(projectId);
            //    if (project.FilePath == projectFile) {
            //        break;
            //    }
            //}
            //if (project == null) {
            //    return;
            //}


            //var tree = context.SemanticModel.SyntaxTree;
            //var projectFile = Find(tree.FilePath, "*proj");
            //var solutionFile = Find(tree.FilePath, "sln");

            //if (solutionFile == null || projectFile == null) {
            //    return;
            //}


            //var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
            //options = options.WithAllowUnsafe(true);                                //Allow unsafe code;
            //options = options.WithOptimizationLevel(OptimizationLevel.Release);     //Set optimization level
            //options = options.WithPlatform(Platform.AnyCpu);                           //Set platform

            //var Mscorlib = PortableExecutableReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location);
            //var compilation = CSharpCompilation.Create("MyCompilation",
            //                    syntaxTrees: new[] { tree },
            //                    references: new[] { Mscorlib },
            //                    options: options);





 

            //var mscorlib = MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly);



        }

        private static Type CompileTheCode(SyntaxNodeAnalysisContext context, string fullClassName) {
            var compilation = context.SemanticModel.Compilation;
            using (var ms = new MemoryStream()) {
                var result = compilation.Emit(ms);

                if (!result.Success) {
                    var failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    foreach (Diagnostic diagnostic in failures) {
                        Debug.WriteLine("{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
                    }
                    return null;
                }
                ms.Seek(0, SeekOrigin.Begin);
                var assembly = Assembly.Load(ms.ToArray());

                _references.Clear();
                foreach (var reference in compilation.References) {
                    var path = reference.Display;
                    var name = Path.GetFileNameWithoutExtension(path);
                    _references.Add(name, path);
                }

                AppDomain.CurrentDomain.AssemblyResolve += (s, a) => {
                    var name = a.Name;
                    var index = name.IndexOf(',');
                    if (index > 0) {
                        name = name.Substring(0, index);
                    }
                    var bytes = File.ReadAllBytes(_references[name]);
                    return Assembly.Load(bytes);
                };


                foreach (var reference in compilation.References) {
                    var bytes = File.ReadAllBytes(reference.Display);
                    Assembly.Load(bytes);
                }
                return assembly.GetType(fullClassName);
            }
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {
            throw new NotImplementedException();
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

