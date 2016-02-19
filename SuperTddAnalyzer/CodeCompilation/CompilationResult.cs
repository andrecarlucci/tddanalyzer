using System.Collections.Generic;

namespace TddAnalyzer {
    public class CompilationResult {
        public bool Success { get; set; }
        public Dictionary<string, byte[]> References = new Dictionary<string, byte[]>();
    }
}