using Microsoft.CodeAnalysis.CodeFixes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Immutable;

namespace TddAnalyzer {
    public class TddAnalyzerCodeFixProvider : CodeFixProvider {
        public override ImmutableArray<string> FixableDiagnosticIds {
            get {
                return new ImmutableArray<string>{ "TddAnalyzer" };
            }
        }

        public async override Task RegisterCodeFixesAsync(CodeFixContext context) {
        }
    }
}
