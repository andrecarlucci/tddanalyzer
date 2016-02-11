using System;
using System.Linq;
using System.Collections.Generic;

namespace TddAnalyzer.Test {
    public class ClassWithTestThatFails {
        [Test]
        public void Test() {
            Assert.IsTrue(false);
        }
    }
}