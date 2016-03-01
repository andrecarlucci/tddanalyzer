using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TddAnalyzer.Util;

namespace TddAnalyzer.CodeCompilation {
    // Provides a means of invoking an assembly in an isolated appdomain
    public static class IsolatedInvoker {
        // main Invoke method
        public static void Invoke(string assemblyFile, string typeName, string methodName, object[] parameters, IEnumerable<byte[]> references) {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            var domainSetup = new AppDomainSetup();
            domainSetup.ApplicationName = Guid.NewGuid().ToString();
            domainSetup.ApplicationBase = Environment.CurrentDirectory;
            var domain = AppDomain.CreateDomain(Guid.NewGuid().ToString(), null, domainSetup);
            try {
                var invoker = (InvokerHelper)domain.CreateInstanceFromAndUnwrap(
                    Assembly.GetExecutingAssembly().Location, 
                    typeof(InvokerHelper).FullName);
                invoker.InvokeHelper(assemblyFile, typeName, methodName, parameters);
            }
            finally {
                AppDomain.Unload(domain);
            }
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {
            //try {
            //    var assembly = Assembly.Load(args.Name);
            //    if (assembly != null) {
            //        Debug.WriteLine("Loaded: " + args.Name);
            //        return assembly;
            //    }
            //}
            //catch { }
            Debug.WriteLine("Loaded: " + args.Name);
            var filename = args.Name.Split(',')[0];
            //var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            //var file = Path.Combine(path, parts[0].Trim() + ".dll"));
            var path = Path.Combine(Path.GetTempPath(), "tdd", filename + ".dll");
            if (!File.Exists(path)) {
                return null;
            }
            return Assembly.Load(path);
        }

        private class InvokerHelper : MarshalByRefObject {
            // This helper function is executed in an isolated app domain
            public void InvokeHelper(string assemblyFile, string typeName, string methodName, object[] parameters) {
                var handle = Activator.CreateInstanceFrom(assemblyFile, typeName);
                var instance = handle.Unwrap();
                instance.RunMethod(methodName);

                //var type = instance.GetType();
                
                //var result = type.InvokeMember(methodName, BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance, null, instance, parameters);
            }
        }
    }
}
