using System.Collections.Generic;

namespace BlackHole.Core
{
    // 로드 결과. 오류가 하나라도 있으면 Tree는 null이다(부분 통과 금지).
    public sealed class NodeTreeLoadResult
    {
        public NodeTree Tree { get; }
        public IReadOnlyList<ContentDiagnostic> Diagnostics { get; }
        public bool Succeeded => Tree != null;

        internal NodeTreeLoadResult(NodeTree tree, List<ContentDiagnostic> diagnostics)
        {
            Tree = tree;
            Diagnostics = diagnostics.AsReadOnly();
        }
    }
}
