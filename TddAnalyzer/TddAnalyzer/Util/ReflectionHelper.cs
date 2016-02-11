using System;
using System.Reflection;

namespace TddAnalyzer.Util {
    public static class ReflectionHelper {

        public static string AssemblyNameForTesting = "";

        public static Type GetType(string fullName, string assemblyName) {
            if (!String.IsNullOrEmpty(AssemblyNameForTesting)) {
                assemblyName = AssemblyNameForTesting;
            }
            return Type.GetType(fullName + "," + assemblyName);
        }

        public static void RunMethod(this object obj, string methodName) {
            obj.GetType().GetRuntimeMethod(methodName, new Type[] { }).Invoke(obj, null);
        }
    }
}
