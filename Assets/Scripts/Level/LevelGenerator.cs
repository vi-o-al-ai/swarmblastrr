using System;
using Swarmblastrr.Track;

namespace Swarmblastrr.Level
{
    /// <summary>
    /// Procedural level layout. Pure C#: the only Unity call is creating the
    /// <see cref="LevelDefinition"/> result, and every random choice comes from one
    /// <see cref="System.Random"/> seeded with <c>seed * 7919 + levelIndex</c>, so the same inputs
    /// always produce the same level.
    /// </summary>
    /// <remarks>
    /// Difficulty is monotone in the level index by construction: the good side of a gate pair
    /// is <c>Add (5 + 2 * level) .. (7 + 2 * level)</c>, so the smallest value at level N + 1
    /// is never below the largest at level N; multiply gates and enemy crowds sit at fixed pair
    /// indices; and a newly unlocked enemy is always followed by a multiply gate that more than
    /// compensates for it. Randomness only decides exact Add values, the bad side of each pair,
    /// which side is left or right, and the spacing between elements.
    /// </remarks>
    public static class LevelGenerator
    {
        private const int StartingUnits = 10;
        private const int MinGatePairs = 4;
        private const int MaxGatePairs = 10;
        private const float FirstZ = 20f;
        private const double MinSpacing = 14.0;
        private const double MaxSpacing = 22.0;

        /// <summary>An enemy crowd follows every N-th gate pair, except the last pair before the boss.</summary>
        private const int PairsPerEnemy = 3;
        private const double EnemyFraction = 0.4;
        private const int MinEnemyCount = 5;
        private const double BossHpFraction = 0.8;
        private const int RampTiles = 10;
        private const float RampTileLength = 2f;
        private const float TailLength = 30f;

        private const int BaseAddValue = 5;
        private const int AddGrowthPerLevel = 2;

        /// <summary>Random spread above the minimum Add value. Must not exceed <see cref="AddGrowthPerLevel"/>.</summary>
        private const int AddSpread = 2;

        /// <summary>Pair index that becomes a x2 gate once <see cref="FirstMultiplyLevel"/> is reached.</summary>
        private const int FirstMultiplyPair = 4;
        private const int FirstMultiplyLevel = 2;

        /// <summary>Pair indices that are always multiply gates; each directly follows a newly unlocked enemy.</summary>
        private const int SecondMultiplyPair = 7;
        private const int ThirdMultiplyPair = 10;
        private const int SmallMultiplier = 2;
        private const int LargeMultiplier = 3;

        private const double MinSubtractFraction = 0.2;
        private const double MaxSubtractFraction = 0.5;
        private const int MinSubtractValue = 2;
        private const int DivideValue = 2;
        private const int MaxSmallAddBesideMultiply = 3;

        /// <summary>
        /// Builds the level for <paramref name="levelIndex"/> (0-based). Elements are sorted by
        /// ascending Z, the first at Z = 20 and each following one 14 to 22 units further on:
        /// <c>min(10, 4 + levelIndex / 2)</c> gate pairs with one clearly better side, an enemy
        /// crowd after every third pair, then exactly one Boss and exactly one RewardRamp last.
        /// </summary>
        public static LevelDefinition Generate(int levelIndex, int seed = 1337)
        {
            if (levelIndex < 0)
            {
                levelIndex = 0;
            }

            System.Random rng = new System.Random(unchecked(seed * 7919 + levelIndex));
            LevelDefinition level = LevelDefinition.CreateEmpty();
            level.StartingUnits = StartingUnits;

            int gatePairs = Math.Min(MaxGatePairs, MinGatePairs + levelIndex / 2);
            int expectedUnits = StartingUnits;
            float z = FirstZ;

            for (int pair = 1; pair <= gatePairs; pair++)
            {
                int expectedAfter;
                LevelElementData gates = MakeGatePair(rng, levelIndex, pair, expectedUnits, out expectedAfter);
                gates.Z = z;
                level.Elements.Add(gates);
                expectedUnits = expectedAfter;
                z = NextZ(rng, z);

                if (pair % PairsPerEnemy == 0 && pair < gatePairs)
                {
                    int count = Math.Max(MinEnemyCount, RoundToInt(expectedUnits * EnemyFraction));
                    LevelElementData enemy = new LevelElementData
                    {
                        Type = LevelElementType.EnemyCrowd,
                        Z = z,
                        Count = count
                    };
                    level.Elements.Add(enemy);
                    expectedUnits = Math.Max(1, expectedUnits - count);
                    z = NextZ(rng, z);
                }
            }

            LevelElementData boss = new LevelElementData
            {
                Type = LevelElementType.Boss,
                Z = z,
                BossHp = Math.Max(1, RoundToInt(expectedUnits * BossHpFraction))
            };
            level.Elements.Add(boss);
            z = NextZ(rng, z);

            LevelElementData ramp = new LevelElementData
            {
                Type = LevelElementType.RewardRamp,
                Z = z,
                RampTiles = RampTiles
            };
            level.Elements.Add(ramp);

            level.Length = z + RampTiles * RampTileLength + TailLength;
            return level;
        }

        private static LevelElementData MakeGatePair(System.Random rng, int levelIndex, int pairIndex, int expectedUnits, out int expectedAfter)
        {
            GateOperation goodOp;
            int goodValue;
            if (IsMultiplyPair(pairIndex, levelIndex))
            {
                goodOp = GateOperation.Multiply;
                goodValue = pairIndex >= ThirdMultiplyPair ? LargeMultiplier : SmallMultiplier;
            }
            else
            {
                goodOp = GateOperation.Add;
                goodValue = BaseAddValue + AddGrowthPerLevel * levelIndex + rng.Next(0, AddSpread + 1);
            }

            GateOperation badOp;
            int badValue;
            switch (rng.Next(3))
            {
                case 0:
                {
                    badOp = GateOperation.Subtract;
                    double fraction = MinSubtractFraction + (MaxSubtractFraction - MinSubtractFraction) * rng.NextDouble();
                    badValue = Math.Max(MinSubtractValue, RoundToInt(expectedUnits * fraction));
                    break;
                }

                case 1:
                {
                    badOp = GateOperation.Divide;
                    badValue = DivideValue;
                    break;
                }

                default:
                {
                    // A smaller Add: strictly less than half of a good Add, or a token amount next to a multiply.
                    badOp = GateOperation.Add;
                    badValue = goodOp == GateOperation.Add
                        ? rng.Next(1, Math.Max(2, goodValue / 2))
                        : rng.Next(1, MaxSmallAddBesideMultiply + 1);
                    break;
                }
            }

            expectedAfter = GateMath.Apply(expectedUnits, goodOp, goodValue);

            bool goodOnLeft = rng.Next(2) == 0;
            LevelElementData data = new LevelElementData { Type = LevelElementType.GatePair };
            if (goodOnLeft)
            {
                data.LeftOp = goodOp;
                data.LeftValue = goodValue;
                data.RightOp = badOp;
                data.RightValue = badValue;
            }
            else
            {
                data.LeftOp = badOp;
                data.LeftValue = badValue;
                data.RightOp = goodOp;
                data.RightValue = goodValue;
            }

            return data;
        }

        private static bool IsMultiplyPair(int pairIndex, int levelIndex)
        {
            if (pairIndex == FirstMultiplyPair)
            {
                return levelIndex >= FirstMultiplyLevel;
            }

            return pairIndex == SecondMultiplyPair || pairIndex == ThirdMultiplyPair;
        }

        private static float NextZ(System.Random rng, float z)
        {
            return z + (float)(MinSpacing + (MaxSpacing - MinSpacing) * rng.NextDouble());
        }

        private static int RoundToInt(double value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }
    }
}
