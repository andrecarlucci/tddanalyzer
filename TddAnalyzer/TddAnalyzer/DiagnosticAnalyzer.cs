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

