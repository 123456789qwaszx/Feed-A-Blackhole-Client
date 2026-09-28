using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.Authoring
{
    // 시트 읽기 결과. 오류가 하나라도 있으면 Data는 null이다(부분 통과 금지).
    public sealed class HqGrowthSheetResult
    {
        public HqGrowthData Data { get; }
        public IReadOnlyList<ContentDiagnostic> Diagnostics { get; }
        public bool Succeeded => Data != null;

        internal HqGrowthSheetResult(HqGrowthData data, List<ContentDiagnostic> diagnostics)
        {
            Data = data;
            Diagnostics = diagnostics.AsReadOnly();
        }
    }
}
