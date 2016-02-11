using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TddAnalyzer {
    public static class CSharpAnalyzerExtensions {
        //code from code-cracker
        public static bool HasAnyAttribute(this SyntaxList<AttributeListSyntax> attributeLists, string[] attributeNames) =>
            attributeLists.SelectMany(a => a.Attributes).Select(a => a.Name.ToString()).Any(name => attributeNames.Any(attributeName =>
            name.EndsWith(attributeName, StringComparison.OrdinalIgnoreCase)
            || name.EndsWith($"{attributeName}Attribute", StringComparison.OrdinalIgnoreCase)));
    }
}
