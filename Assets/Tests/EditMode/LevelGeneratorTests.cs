using System;
using System.Collections.Generic;
using NUnit.Framework;
using Swarmblastrr.Level;
using Swarmblastrr.Track;

namespace Swarmblastrr.Tests
{
    /// <summary>
    /// Edit-mode tests for <see cref="LevelGenerator"/>: deterministic for the same inputs, Z strictly
    /// ascending with the contract spacing, exactly one Boss then one RewardRamp last, Length covering
    /// every element, one clearly better side per gate pair and difficulty growing with the level index.
    /// </summary>
    [TestFixture]
    public class LevelGeneratorTests
    {
        private const int LevelsToCheck = 30;
        private const int DefaultSeed = 1337;
        private const int StartingUnits = 10;
        private const int MaxGatePairs = 10;
        private const int BaseGatePairs = 4;
        private const float FirstElementMinZ = 20f;
        private const float MinSpacing = 14f;
        private const float MaxSpacing = 22f;
        private const float RampTileLength = 2f;
        private const float TailLength = 30f;
        private const float ZTolerance = 1e-3f;

        private readonly List<LevelDefinition> _created = new List<LevelDefinition>();

        [TearDown]
        public void TearDown()
        {
            foreach (LevelDefinition level in _created)
            {
                if (level != null)
                {
                    UnityEngine.Object.DestroyImmediate(level);
                }
            }

            _created.Clear();
        }

        [Test]
        public void CreateEmpty_HasNoElementsAndContractDefaults()
        {
            LevelDefinition empty = Register(LevelDefinition.CreateEmpty());
            Assert.IsNotNull(empty);
            Assert.IsNotNull(empty.Elements);
            Assert.AreEqual(0, empty.Elements.Count);
            Assert.AreEqual(10, empty.StartingUnits);
            Assert.AreEqual(200f, empty.Length, ZTolerance);
        }

        [Test]
        public void Generate_ReturnsNonEmptyLevel()
        {
            LevelDefinition level = Generate(0);
            Assert.IsNotNull(level);
            Assert.IsNotNull(level.Elements);
            Assert.GreaterOrEqual(level.Elements.Count, 3, "at least one gate pair, a boss and a ramp");
            Assert.Greater(level.Length, 0f);
            Assert.AreEqual(StartingUnits, level.StartingUnits);
        }

        [Test]
        public void Generate_SameInputs_AreIdentical()
        {
            int[] levels = { 0, 1, 7, 25 };
            foreach (int levelIndex in levels)
            {
                AssertSameLevel(Generate(levelIndex), Generate(levelIndex), "level " + levelIndex);
            }

            AssertSameLevel(Generate(3, 99), Generate(3, 99), "level 3 seed 99");
        }

        [Test]
        public void Generate_DefaultSeed_IsContractSeed()
        {
            AssertSameLevel(Generate(4), Generate(4, DefaultSeed), "default seed");
        }

        [Test]
        public void Generate_DifferentSeeds_ProduceDifferentLayouts()
        {
            LevelDefinition a = Generate(0, DefaultSeed);
            LevelDefinition b = Generate(0, 42);
            Assert.IsFalse(SameZSequence(a, b));
        }

        [Test]
        public void Generate_DifferentLevelIndices_ProduceDifferentLayouts()
        {
            LevelDefinition a = Generate(0);
            LevelDefinition b = Generate(1);
            Assert.IsFalse(SameZSequence(a, b));
        }

        [Test]
        public void Generate_NegativeLevelIndex_IsTreatedAsZero()
        {
            AssertSameLevel(Generate(-3), Generate(0), "negative level index");
        }

        [Test]
        public void Generate_ZStrictlyAscending_WithContractSpacing()
        {
            for (int levelIndex = 0; levelIndex <= LevelsToCheck; levelIndex++)
            {
                List<LevelElementData> elements = Generate(levelIndex).Elements;
                Assert.GreaterOrEqual(elements[0].Z, FirstElementMinZ - ZTolerance, "level " + levelIndex + " first element");

                for (int i = 1; i < elements.Count; i++)
                {
                    float gap = elements[i].Z - elements[i - 1].Z;
                    string context = "level " + levelIndex + " element " + i;
                    Assert.Greater(gap, 0f, context + " must be strictly ascending");
                    Assert.GreaterOrEqual(gap, MinSpacing - ZTolerance, context + " spacing too small");
                    Assert.LessOrEqual(gap, MaxSpacing + ZTolerance, context + " spacing too large");
                }
            }
        }

        [Test]
        public void Generate_ExactlyOneBossSecondToLast_AndOneRewardRampLast()
        {
            for (int levelIndex = 0; levelIndex <= LevelsToCheck; levelIndex++)
            {
                List<LevelElementData> elements = Generate(levelIndex).Elements;
                string context = "level " + levelIndex;

                Assert.AreEqual(1, CountOf(elements, LevelElementType.Boss), context + " boss count");
                Assert.AreEqual(1, CountOf(elements, LevelElementType.RewardRamp), context + " ramp count");

                LevelElementData boss = elements[elements.Count - 2];
                LevelElementData ramp = elements[elements.Count - 1];
                Assert.AreEqual(LevelElementType.Boss, boss.Type, context + " second to last");
                Assert.AreEqual(LevelElementType.RewardRamp, ramp.Type, context + " last");
                Assert.GreaterOrEqual(boss.BossHp, 1, context + " boss hp");
                Assert.Greater(ramp.RampTiles, 0, context + " ramp tiles");
            }
        }

        [Test]
        public void Generate_LengthCoversEveryElement()
        {
            for (int levelIndex = 0; levelIndex <= LevelsToCheck; levelIndex++)
            {
                LevelDefinition level = Generate(levelIndex);
                string context = "level " + levelIndex;
                LevelElementData last = level.Elements[level.Elements.Count - 1];

                Assert.GreaterOrEqual(level.Length, last.Z + TailLength - ZTolerance, context + " needs 30 units past the last element");
                Assert.GreaterOrEqual(
                    level.Length,
                    last.Z + last.RampTiles * RampTileLength + TailLength - ZTolerance,
                    context + " needs the ramp tiles plus 30 units");

                foreach (LevelElementData element in level.Elements)
                {
                    Assert.Less(element.Z, level.Length, context + " element beyond the floor");
                }
            }
        }

        [Test]
        public void Generate_GatePairCount_FollowsContractFormula()
        {
            for (int levelIndex = 0; levelIndex <= 40; levelIndex++)
            {
                int expected = Math.Min(MaxGatePairs, BaseGatePairs + levelIndex / 2);
                Assert.AreEqual(expected, CountOf(Generate(levelIndex).Elements, LevelElementType.GatePair), "level " + levelIndex);
            }
        }

        [Test]
        public void Generate_ElementsCarryTheirRelevantValues()
        {
            for (int levelIndex = 0; levelIndex <= LevelsToCheck; levelIndex++)
            {
                List<LevelElementData> elements = Generate(levelIndex).Elements;
                for (int i = 0; i < elements.Count; i++)
                {
                    LevelElementData element = elements[i];
                    string context = "level " + levelIndex + " element " + i + " " + element.Type;
                    switch (element.Type)
                    {
                        case LevelElementType.GatePair:
                            Assert.Greater(element.LeftValue, 0, context + " left value");
                            Assert.Greater(element.RightValue, 0, context + " right value");
                            break;
                        case LevelElementType.EnemyCrowd:
                            Assert.Greater(element.Count, 0, context + " count");
                            break;
                        case LevelElementType.Boss:
                            Assert.Greater(element.BossHp, 0, context + " hp");
                            break;
                        case LevelElementType.RewardRamp:
                            Assert.Greater(element.RampTiles, 0, context + " tiles");
                            break;
                        default:
                            Assert.Fail(context + " unexpected element type");
                            break;
                    }
                }
            }
        }

        [Test]
        public void Generate_EveryGatePair_HasOneClearlyBetterSide()
        {
            for (int levelIndex = 0; levelIndex <= LevelsToCheck; levelIndex++)
            {
                int units = StartingUnits;
                List<LevelElementData> elements = Generate(levelIndex).Elements;
                for (int i = 0; i < elements.Count; i++)
                {
                    LevelElementData element = elements[i];
                    string context = "level " + levelIndex + " element " + i;
                    if (element.Type == LevelElementType.GatePair)
                    {
                        int left = GateMath.Apply(units, element.LeftOp, element.LeftValue);
                        int right = GateMath.Apply(units, element.RightOp, element.RightValue);
                        Assert.AreNotEqual(left, right, context + " both sides give the same result");
                        int better = Math.Max(left, right);
                        Assert.Greater(better, units, context + " the better side must grow the crowd");
                        units = better;
                    }
                    else if (element.Type == LevelElementType.EnemyCrowd)
                    {
                        units = Math.Max(1, units - element.Count);
                    }
                }
            }
        }

        [Test]
        public void Generate_BossHp_IsBeatableAlongTheBetterPath()
        {
            for (int levelIndex = 0; levelIndex <= LevelsToCheck; levelIndex++)
            {
                List<LevelElementData> elements = Generate(levelIndex).Elements;
                int units = ExpectedUnitsBeforeBoss(elements);
                LevelElementData boss = elements[elements.Count - 2];
                string context = "level " + levelIndex;
                Assert.GreaterOrEqual(boss.BossHp, 1, context);
                Assert.LessOrEqual(boss.BossHp, units, context + " boss must be beatable with one damage per unit");
            }
        }

        [Test]
        public void Generate_EnemyCrowds_ArePresentAndGrowWithLevel()
        {
            int previousTotal = -1;
            for (int levelIndex = 0; levelIndex <= LevelsToCheck; levelIndex++)
            {
                List<LevelElementData> elements = Generate(levelIndex).Elements;
                string context = "level " + levelIndex;
                int enemies = 0;
                int total = 0;
                foreach (LevelElementData element in elements)
                {
                    if (element.Type == LevelElementType.EnemyCrowd)
                    {
                        enemies++;
                        total += element.Count;
                    }
                }

                Assert.GreaterOrEqual(enemies, 1, context + " needs at least one enemy crowd");
                Assert.GreaterOrEqual(total, previousTotal, context + " enemy units must not shrink");
                previousTotal = total;
            }

            Assert.Greater(TotalEnemyUnits(Generate(20).Elements), TotalEnemyUnits(Generate(0).Elements));
        }

        [Test]
        public void Generate_DifficultyGrowsWithLevel()
        {
            int previousHp = -1;
            int previousPairs = -1;
            int previousElements = -1;
            for (int levelIndex = 0; levelIndex <= LevelsToCheck; levelIndex++)
            {
                LevelDefinition level = Generate(levelIndex);
                string context = "level " + levelIndex;
                int hp = BossHp(level.Elements);
                int pairs = CountOf(level.Elements, LevelElementType.GatePair);

                Assert.GreaterOrEqual(hp, previousHp, context + " boss hp must not drop");
                Assert.GreaterOrEqual(pairs, previousPairs, context + " gate pairs must not drop");
                Assert.GreaterOrEqual(level.Elements.Count, previousElements, context + " element count must not drop");

                previousHp = hp;
                previousPairs = pairs;
                previousElements = level.Elements.Count;
            }

            Assert.Greater(BossHp(Generate(10).Elements), BossHp(Generate(0).Elements));
            Assert.Greater(BossHp(Generate(20).Elements), BossHp(Generate(10).Elements));
            Assert.Greater(CountOf(Generate(12).Elements, LevelElementType.GatePair), CountOf(Generate(0).Elements, LevelElementType.GatePair));
        }

        private LevelDefinition Generate(int levelIndex)
        {
            return Register(LevelGenerator.Generate(levelIndex));
        }

        private LevelDefinition Generate(int levelIndex, int seed)
        {
            return Register(LevelGenerator.Generate(levelIndex, seed));
        }

        /// <summary>Remembers a generated ScriptableObject so TearDown can destroy it.</summary>
        private LevelDefinition Register(LevelDefinition level)
        {
            _created.Add(level);
            return level;
        }

        private static int CountOf(List<LevelElementData> elements, LevelElementType type)
        {
            int count = 0;
            foreach (LevelElementData element in elements)
            {
                if (element.Type == type)
                {
                    count++;
                }
            }

            return count;
        }

        private static int BossHp(List<LevelElementData> elements)
        {
            foreach (LevelElementData element in elements)
            {
                if (element.Type == LevelElementType.Boss)
                {
                    return element.BossHp;
                }
            }

            Assert.Fail("level has no boss");
            return 0;
        }

        private static int TotalEnemyUnits(List<LevelElementData> elements)
        {
            int total = 0;
            foreach (LevelElementData element in elements)
            {
                if (element.Type == LevelElementType.EnemyCrowd)
                {
                    total += element.Count;
                }
            }

            return total;
        }

        /// <summary>Walks the level taking the better gate each time, the way the generator sizes the boss.</summary>
        private static int ExpectedUnitsBeforeBoss(List<LevelElementData> elements)
        {
            int units = StartingUnits;
            foreach (LevelElementData element in elements)
            {
                if (element.Type == LevelElementType.GatePair)
                {
                    int left = GateMath.Apply(units, element.LeftOp, element.LeftValue);
                    int right = GateMath.Apply(units, element.RightOp, element.RightValue);
                    units = Math.Max(left, right);
                }
                else if (element.Type == LevelElementType.EnemyCrowd)
                {
                    units = Math.Max(1, units - element.Count);
                }
                else if (element.Type == LevelElementType.Boss)
                {
                    break;
                }
            }

            return units;
        }

        private static bool SameZSequence(LevelDefinition a, LevelDefinition b)
        {
            if (a.Elements.Count != b.Elements.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Elements.Count; i++)
            {
                if (a.Elements[i].Z != b.Elements[i].Z)
                {
                    return false;
                }
            }

            return true;
        }

        private static void AssertSameLevel(LevelDefinition a, LevelDefinition b, string context)
        {
            Assert.AreEqual(a.Length, b.Length, context + " Length");
            Assert.AreEqual(a.StartingUnits, b.StartingUnits, context + " StartingUnits");
            Assert.AreEqual(a.Elements.Count, b.Elements.Count, context + " element count");
            for (int i = 0; i < a.Elements.Count; i++)
            {
                AssertSameElement(a.Elements[i], b.Elements[i], context + " element " + i);
            }
        }

        private static void AssertSameElement(LevelElementData x, LevelElementData y, string context)
        {
            Assert.AreEqual(x.Type, y.Type, context + " Type");
            Assert.AreEqual(x.Z, y.Z, context + " Z");
            Assert.AreEqual(x.LeftOp, y.LeftOp, context + " LeftOp");
            Assert.AreEqual(x.LeftValue, y.LeftValue, context + " LeftValue");
            Assert.AreEqual(x.RightOp, y.RightOp, context + " RightOp");
            Assert.AreEqual(x.RightValue, y.RightValue, context + " RightValue");
            Assert.AreEqual(x.Count, y.Count, context + " Count");
            Assert.AreEqual(x.BossHp, y.BossHp, context + " BossHp");
            Assert.AreEqual(x.RampTiles, y.RampTiles, context + " RampTiles");
        }
    }
}
