using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TddAnalyzer {
    public static class ReferenceFinder {
        public static void Find(string solutionPath, string methodName) {
            var msWorkspace = MSBuildWorkspace.Create();

            List<ReferencedSymbol> referencesToMethod = new List<ReferencedSymbol>();
           // Console.WriteLine("Searching for method \"{0}\" reference in solution {1} ", methodName, Path.GetFileName(solutionPath));
            ISymbol methodSymbol = null;
            bool found = false;

            //You must install the MSBuild Tools or this line will throw an exception.

            var solution = msWorkspace.OpenSolutionAsync(solutionPath).Result;
            foreach (var project in solution.Projects) {
                foreach (var document in project.Documents) {
                    var model = document.GetSemanticModelAsync().Result;

                    var methodInvocation = document.GetSyntaxRootAsync().Result;
                    InvocationExpressionSyntax node = null;
                    try {
                        node = methodInvocation.DescendantNodes().OfType<InvocationExpressionSyntax>()
                         .Where(x => ((MemberAccessExpressionSyntax)x.Expression).Name.ToString() == methodName).FirstOrDefault();

                        if (node == null)
                            continue;
                    }
                    catch (Exception exception) {
                        // Swallow the exception of type cast. 
                        // Could be avoided by a better filtering on above linq.
                        continue;
                    }

                    methodSymbol = model.GetSymbolInfo(node).Symbol;
                    found = true;
                    break;
                }

                if (found) break;
            }

            foreach (var item in SymbolFinder.FindReferencesAsync(methodSymbol, solution).Result) {
                foreach (var location in item.Locations) {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Project Assembly -> {0}", location.Document.Project.AssemblyName);
                    Console.ResetColor();
                }

            }

            Console.WriteLine("Finished searching. Press any key to continue....");
        }
    }
}
