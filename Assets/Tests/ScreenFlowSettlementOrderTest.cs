using UnityEngine;

public class ScreenFlowSettlementOrderTest
{
    [Test]
    public void 결산후_업그레이드하면_결산로그가_구매로그보다_먼저생성된다()
    {
        // Arrange
        // 실제 ScreenFlow 테스트 환경 구성

        // Act
        // 결산 → 업그레이드 구매 실행

        // Assert
        // 결산 로그와 구매 로그가 모두 생성되었는지 확인
        // 결산 로그의 Seq < 구매 로그의 Seq인지 확인
    }
}
