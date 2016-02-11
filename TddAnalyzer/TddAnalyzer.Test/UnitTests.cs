using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using TestHelper;
using Xunit;
using System.IO;
using TddAnalyzer.Util;

namespace TddAnalyzer.Test {
    public class UnitTest : CodeFixVerifier {


        public UnitTest() {
            ReflectionHelper.AssemblyNameForTesting = "TddAnalyzer.Test";
        }

        //No diagnostics expected to show up
        [Fact]
        public void TestMethod1() {
            var test = @"";
            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void This_test_pass() {
            var test = File.ReadAllText("ClassWithTest.cs");
            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void This_test_fails() {
            var test = File.ReadAllText("ClassWithTestThatFails.cs");

            var expected = new DiagnosticResult {
                Id = "TddAnalyzer",
                Message = "Assert Fail",
                Severity = DiagnosticSeverity.Warning,
                Locations = new[] { new DiagnosticResultLocation("Test0.cs", 7, 9) }
            };
            VerifyCSharpDiagnostic(test, expected);
        }

        [Fact]
        public void This_test_has_setup() {
            var test = File.ReadAllText("ClassWithTestSetup.cs");
            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void This_test_has_teardown() {
            var test = File.ReadAllText("ClassWithTestTearDown.cs");
            VerifyCSharpDiagnostic(test);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider() {
            return new TddAnalyzerCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() {
            return new TddAnalyzerAnalyzer();
        }
    }
}