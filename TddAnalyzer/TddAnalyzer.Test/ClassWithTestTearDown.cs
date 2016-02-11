using System;
using System.Linq;
using System.Collections.Generic;

namespace TddAnalyzer.Test {
    public class ClassWithTestTearDown {

        private int _age = 1;

        [SetUp]
        public void SetUp() {
            _age = 2;
        }

        [Test]
        public void Test() {
            Assert.IsTrue(_age == 2);
        }
        [TearDown]
        public void TearDown() {

        }

    }
}