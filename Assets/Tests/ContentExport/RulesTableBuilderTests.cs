using System;
using System.Collections.Generic;
using BlackHole.Core;
using NUnit.Framework;

namespace BlackHole.ContentExport.Tests
{
    // 전투 규칙 기대값 표(계약 1.7a)의 확인 지점 선택과 글자 형식.
    // 표 전체는 실제 콘텐츠가 있어야 만들 수 있어(에디터 메뉴) 여기서는 콘텐츠 없이 확인할 수 있는 규칙만 본다.
    // 전체의 일치는 서버 테스트(W4-1b)가 확인한다.
    public sealed class RulesTableBuilderTests
    {
        // Level 1, 2, 3에 닿는 누적 EXP가 10, 25, 45. 성장도 1의 이정표는 Level 2.
        private static HqGrowthDefinition Growth() => new(
            new long[] { 10, 25, 45 },
            new[] { new HqMilestone(2, 100, 1f) });

        [TestCase(335, 10, 33)]
        [TestCase(335, 25, 83)]
        [TestCase(335, 50, 167)]
        [TestCase(335, 75, 251)]
        [TestCase(0, 50, 0)]
        [TestCase(7, 100, 7)]
        [TestCase(7, 0, 0)]
        public void PartialCountRoundsDown(int total, int percent, int expected)
        {
            Assert.AreEqual(expected, RulesTableBuilder.PartialCount(total, percent));
        }

        [TestCase(10, -1)]
        [TestCase(10, 101)]
        [TestCase(-1, 50)]
        public void PartialCountRejectsOutOfRangeInput(int total, int percent)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RulesTableBuilder.PartialCount(total, percent));
        }

        [Test]
        public void ProbeExpsAroundEveryThresholdFromTheStartLevel()
        {
            Assert.AreEqual(
                new long[] { 0, 9, 10, 11, 24, 25, 26, 44, 45, 46 },
                RulesTableBuilder.ProbeExps(Growth(), 0));
        }

        [Test]
        public void ProbeExpsStartOneBelowTheStartExpAndSkipLevelsAtOrBelowTheStartLevel()
        {
            // 성장도 1은 Level 2에서 시작한다(시작 EXP 25). Level 3의 임계값 45 근처만 더한다.
            Assert.AreEqual(
                new long[] { 24, 25, 44, 45, 46 },
                RulesTableBuilder.ProbeExps(Growth(), 1));
        }

        [Test]
        public void ProbeGoldsCoverTheTargetBoundariesAndDouble()
        {
            Assert.AreEqual(new long[] { 0, 1, 99, 100, 101, 200 }, RulesTableBuilder.ProbeGolds(100));
        }

        [Test]
        public void ProbeGoldsDropDuplicatesForTinyTargets()
        {
            Assert.AreEqual(new long[] { 0, 1, 2 }, RulesTableBuilder.ProbeGolds(1));
        }

        [Test]
        public void ProbeGoldsDoNotOverflowForHugeTargets()
        {
            IReadOnlyList<long> golds = RulesTableBuilder.ProbeGolds(long.MaxValue - 1);

            // 2 × target은 long을 넘으므로 넣지 않는다.
            Assert.AreEqual(new long[] { 0, 1, long.MaxValue - 2, long.MaxValue - 1, long.MaxValue }, golds);
        }

        [Test]
        public void InlineValueStaysOnOneLineInsideAMultiLineDocument()
        {
            string json = PyJson.Write(new JsonObject
            {
                { "a", new List<object> { new JsonInline(new List<object> { 1, 2L }), new JsonInline(new List<object> { 3, 4L }) } },
                { "b", new JsonInline(new JsonObject { { "x", 0.5f }, { "y", null } }) },
            });

            Assert.AreEqual(
                "{\n  \"a\": [\n    [1, 2],\n    [3, 4]\n  ],\n  \"b\": {\"x\": 0.5, \"y\": null}\n}",
                json);
        }
    }
}
