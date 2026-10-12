using NUnit.Framework;

namespace BlackHole.Analytics.Tests
{
    // 게임 열거형의 이름이 계약의 ID 규칙(^[a-z][A-Za-z]*$)대로 바뀐다. 서버와 분석 쿼리가 이 ID로 종류를 가린다.
    public sealed class ContractIdsTests
    {
        [TestCase("Asteroid", "asteroid")]
        [TestCase("Comet", "comet")]
        [TestCase("Supernova", "supernova")]
        [TestCase("GoldenAsteroid", "goldenAsteroid")]
        public void LowersOnlyFirstLetter(string typeName, string expected)
        {
            Assert.AreEqual(expected, ContractIds.Of(typeName));
        }
    }
}
