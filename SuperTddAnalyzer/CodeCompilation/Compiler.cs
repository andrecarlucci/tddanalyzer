using AppDomainToolkit;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace TddAnalyzer {
    public class Compiler {

        private Dictionary<string, byte[]> _references = new Dictionary<string, byte[]>();

        public CompilationResult Compile(Compilation compilation) {
            var result = new CompilationResult();
            result.Success = CompileInternal(compilation);
            result.References = _references;
            return result;
        }

        public bool CompileInternal(Compilation compilation) {
            foreach (var reference in compilation.References) {
                var path = reference.Display;
                var name = Path.GetFileNameWithoutExtension(path);
                var compilationReference = reference as CompilationReference;
                if (compilationReference != null) {
                    var result = CompileInternal(compilationReference.Compilation);
                    if (!result) {
                        return false;
                    }
                }
                else if(!_references.ContainsKey(name)) {
                    _references[name] = File.ReadAllBytes(path);
                }
            }
            var dll = MakeDll(compilation);
            if (dll == null) {
                return false;
            }
            var assemblyName = compilation.AssemblyName;
            _references[assemblyName] = dll;
            return true;
        }

        private string GetDllPath(string name) {
            return Path.Combine(Path.GetTempPath(), name + ".dll");
        }

        private static byte[] MakeDll(Compilation compilation) {
            using (var ms = new MemoryStream()) {
                var result = compilation.Emit(ms);
                if (!result.Success) {
                    return null;
                }
                ms.Seek(0, SeekOrigin.Begin);
                return ms.ToArray();
            }
        }

        private Assembly Temporary_AssemblyResolve(object sender, ResolveEventArgs a) {
            var name = a.Name;
            var index = name.IndexOf(',');
            if (index > 0) {
                name = name.Substring(0, index);
            }
            if (!_references.ContainsKey(name)) {
                return null;
            }
            return null;
        }
    }
}
