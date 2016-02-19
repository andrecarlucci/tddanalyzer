using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TddAnalyzer.CodeCompilation {
    public class Launcher : MarshalByRefObject {
        public static void Invoke(byte[] dll) {
            AppDomain appDomain = AppDomain.CreateDomain("Loading Domain");
            try {
                appDomain.AssemblyResolve += (s, a) => {
                    var path = Path.Combine(Path.GetTempPath(), "tdd");
                    return Assembly.LoadFile(Path.Combine(path, a.Name + ".dll"));
                };
                
                Launcher program = (Launcher)appDomain.CreateInstanceAndUnwrap(
                    typeof(Launcher).Assembly.FullName,
                    typeof(Launcher).FullName);

                program.Execute(dll);
            }
            finally {
                AppDomain.Unload(appDomain);
            }
        }

        public void Execute(byte[] dll) {
            //// load the bytes and run Main() using reflection
            //// working with bytes is useful if the assembly doesn't come from disk
            //Assembly assembly = Assembly.Load(dll);


            //var type = instance.GetType();

            //// invoke the method
            //var result = type.InvokeMember(methodName, BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance, null, instance, parameters);

            //var main = assembly.EntryPoint;
            //main.Invoke(null, new object[] { null });
        }
    }
}
