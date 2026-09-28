using System.Collections.Generic;

namespace BlackHole.Core
{
    // 로드 결과. 오류가 하나라도 있으면 Content는 null이다(부분 통과 금지).
    public sealed class ContentLoadResult
    {
        public GameContent Content { get; }
        public IReadOnlyList<ContentDiagnostic> Diagnostics { get; }
        public bool Succeeded => Content != null;

        internal ContentLoadResult(GameContent content, List<ContentDiagnostic> diagnostics)
        {
            Content = content;
            Diagnostics = diagnostics.AsReadOnly();
        }
    }
}
