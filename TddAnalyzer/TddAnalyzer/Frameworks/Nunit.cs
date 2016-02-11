using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TddAnalyzer.Frameworks {
    public class Nunit : TestFramework {
        private string[] _attributes = new[] { "Test" };
        private string[] _attributesICantHandle = new[] { "TestCase" };

        private string[] _attributesForSetUp = new[] { "Setup" };
        private string[] _attributesForTearDown = new[] { "TearDown" };

        protected override string[] GetExcludingTestAttributes() {
            return _attributesICantHandle;
        }

        protected override string[] GetTestAttributes() {
            return _attributes;
        }

        protected override string[] GetSetUpAttributes() {
            return _attributesForSetUp;
        }

        protected override string[] GetTearDownAttributes() {
            return _attributesForTearDown;
        }
    }
}
