using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using TddAnalyzer.Util;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;

namespace TddAnalyzer.Frameworks {
    public abstract class TestFramework {
        public bool CanRun(MethodDeclarationSyntax method) {
            var result = method.AttributeLists.HasAnyAttribute(GetTestAttributes());
            if (!result) {
                return false;
            }
            return !method.AttributeLists.HasAnyAttribute(GetExcludingTestAttributes());
        }

        protected abstract string[] GetTestAttributes();
        protected abstract string[] GetExcludingTestAttributes();
        protected abstract string[] GetSetUpAttributes();
        protected abstract string[] GetTearDownAttributes();

        private SemanticModel _semanticModel;

        public void Run(Type type, MethodDeclarationSyntax method, SemanticModel semanticModel) {
            _semanticModel = semanticModel;
            var testObject = CreateObject(type);
            ExecuteSetup(testObject, method);
            ExecuteTest(testObject, method);
            ExecuteTearDown(testObject, method);
        }
        
        protected virtual object CreateObject(Type type) {
            return Activator.CreateInstance(type);
        }

        protected virtual void ExecuteSetup(object testObject, MethodDeclarationSyntax method) {
            var setupMethod = method.Parent.DescendantNodes()
                .FirstOrDefault(descendant => descendant.IsKind(SyntaxKind.MethodDeclaration) && ((MethodDeclarationSyntax)descendant).AttributeLists.HasAnyAttribute(GetSetUpAttributes()));
            var setupMethodSyntax = setupMethod as MethodDeclarationSyntax;
            if (setupMethodSyntax == null) {
                return;
            }
            var methodSymbol = GetMethodSymbol(setupMethodSyntax);
            testObject.RunMethod(methodSymbol.Name);
        }


        protected virtual void ExecuteTest(object testObject, MethodDeclarationSyntax method) {
            var methodSymbol = GetMethodSymbol(method);
            testObject.RunMethod(methodSymbol.Name);
        }

        private void ExecuteTearDown(object testObject, MethodDeclarationSyntax method) {
            var tearDown = method.Parent.DescendantNodes()
                .FirstOrDefault(descendant => descendant.IsKind(SyntaxKind.MethodDeclaration) && ((MethodDeclarationSyntax)descendant).AttributeLists.HasAnyAttribute(GetTearDownAttributes()));
            var tearDownMethodSyntax = tearDown as MethodDeclarationSyntax;
            if (tearDownMethodSyntax == null) {
                return;
            }
            var methodSymbol = GetMethodSymbol(tearDownMethodSyntax);
            testObject.RunMethod(methodSymbol.Name);
        }

        protected IMethodSymbol GetMethodSymbol(MethodDeclarationSyntax method) {
            return _semanticModel.GetDeclaredSymbol(method);
        }
    }
}