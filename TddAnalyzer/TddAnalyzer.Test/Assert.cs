using System;
using System.Linq;
using System.Collections.Generic;

namespace TddAnalyzer.Test {
    public static class Assert {
        public static void IsTrue(bool x) {
            if (!x) {
                throw new Exception("Assert Fail");
            }
        }
    }
}