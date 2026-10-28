using System;
using NUnit.Framework;
using Swarmblastrr.Meta;

namespace Swarmblastrr.Tests
{
    /// <summary>
    /// Edit-mode tests for <see cref="UpgradeMath"/> evaluated against <see cref="UpgradeCatalog"/>
    /// and hand-made definitions: the cost curve is monotonic and geometric, the value curve is
    /// linear, and max level is honoured (0 meaning unlimited).
    /// </summary>
    [TestFixture]
    public class UpgradeMathTests
    {
        private const int LevelsToCheck = 40;
        private const float FloatTolerance = 1e-4f;

        [Test]
        public void Catalog_HasOneDefinitionPerUpgradeType()
        {
            Array types = Enum.GetValues(typeof(UpgradeType));
            Assert.AreEqual(types.Length, UpgradeCatalog.All.Length);

            foreach (UpgradeType type in types)
            {
                Assert.AreEqual(type, UpgradeCatalog.All[(int)type].Type, "All[] order must follow the enum");
                Assert.AreEqual(type, UpgradeCatalog.Get(type).Type, "Get must return the matching definition");
                Assert.IsFalse(string.IsNullOrEmpty(UpgradeCatalog.Get(type).DisplayName), type + " needs a display name");
            }
        }

        [Test]
        public void Catalog_MatchesContractTuning()
        {
            UpgradeDefinition starting = UpgradeCatalog.Get(UpgradeType.StartingUnits);
            Assert.AreEqual(30, starting.BaseCost);
            Assert.AreEqual(1.25f, starting.CostGrowth, FloatTolerance);
            Assert.AreEqual(10f, starting.BaseValue, FloatTolerance);
            Assert.AreEqual(1f, starting.ValuePerLevel, FloatTolerance);

            UpgradeDefinition damage = UpgradeCatalog.Get(UpgradeType.UnitDamage);
            Assert.AreEqual(80, damage.BaseCost);
            Assert.AreEqual(1.5f, damage.CostGrowth, FloatTolerance);
            Assert.AreEqual(1f, damage.BaseValue, FloatTolerance);
            Assert.AreEqual(1f, damage.ValuePerLevel, FloatTolerance);

            UpgradeDefinition income = UpgradeCatalog.Get(UpgradeType.CoinIncome);
            Assert.AreEqual(50, income.BaseCost);
            Assert.AreEqual(1.35f, income.CostGrowth, FloatTolerance);
            Assert.AreEqual(1f, income.BaseValue, FloatTolerance);
            Assert.AreEqual(0.1f, income.ValuePerLevel, FloatTolerance);
        }

        [Test]
        public void Catalog_Get_UnknownType_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => UpgradeCatalog.Get((UpgradeType)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => UpgradeCatalog.Get((UpgradeType)(-1)));
        }

        [Test]
        public void CostFor_LevelZero_IsBaseCost()
        {
            foreach (UpgradeDefinition def in UpgradeCatalog.All)
            {
                Assert.AreEqual(def.BaseCost, UpgradeMath.CostFor(def, 0), def.DisplayName);
            }
        }

        [Test]
        public void CostFor_CatalogCurves_AreStrictlyIncreasing()
        {
            foreach (UpgradeDefinition def in UpgradeCatalog.All)
            {
                int previous = UpgradeMath.CostFor(def, 0);
                for (int level = 1; level <= LevelsToCheck; level++)
                {
                    int cost = UpgradeMath.CostFor(def, level);
                    Assert.Greater(cost, previous, def.DisplayName + " level " + level);
                    previous = cost;
                }
            }
        }

        [Test]
        public void CostFor_MatchesGeometricFormula()
        {
            foreach (UpgradeDefinition def in UpgradeCatalog.All)
            {
                for (int level = 0; level <= LevelsToCheck; level++)
                {
                    double expected = Math.Round(def.BaseCost * Math.Pow(def.CostGrowth, level));
                    Assert.AreEqual(expected, UpgradeMath.CostFor(def, level), 1.0, def.DisplayName + " level " + level);
                }
            }
        }

        [Test]
        public void CostFor_KnownValues()
        {
            UpgradeDefinition damage = UpgradeCatalog.Get(UpgradeType.UnitDamage);
            Assert.AreEqual(120, UpgradeMath.CostFor(damage, 1));
            Assert.AreEqual(180, UpgradeMath.CostFor(damage, 2));
            Assert.AreEqual(270, UpgradeMath.CostFor(damage, 3));

            UpgradeDefinition starting = UpgradeCatalog.Get(UpgradeType.StartingUnits);
            Assert.AreEqual(47, UpgradeMath.CostFor(starting, 2));
            Assert.AreEqual(59, UpgradeMath.CostFor(starting, 3));
        }

        [Test]
        public void CostFor_GrowthOfOne_IsFlat()
        {
            UpgradeDefinition flat = MakeDefinition(25, 1f, 0f, 0f, 0);
            for (int level = 0; level < 10; level++)
            {
                Assert.AreEqual(25, UpgradeMath.CostFor(flat, level), "level " + level);
            }
        }

        [Test]
        public void CostFor_NegativeLevel_IsTreatedAsZero()
        {
            foreach (UpgradeDefinition def in UpgradeCatalog.All)
            {
                Assert.AreEqual(UpgradeMath.CostFor(def, 0), UpgradeMath.CostFor(def, -3), def.DisplayName);
            }
        }

        [Test]
        public void CostFor_HugeLevel_DoesNotOverflow()
        {
            UpgradeDefinition damage = UpgradeCatalog.Get(UpgradeType.UnitDamage);
            int cost = 0;
            Assert.DoesNotThrow(() => cost = UpgradeMath.CostFor(damage, 10_000));
            Assert.Greater(cost, 0, "a huge level must saturate to a positive cost, not wrap negative");
            Assert.GreaterOrEqual(cost, UpgradeMath.CostFor(damage, LevelsToCheck));
        }

        [Test]
        public void ValueFor_LevelZero_IsBaseValue()
        {
            foreach (UpgradeDefinition def in UpgradeCatalog.All)
            {
                Assert.AreEqual(def.BaseValue, UpgradeMath.ValueFor(def, 0), FloatTolerance, def.DisplayName);
            }
        }

        [Test]
        public void ValueFor_IsLinearInLevel()
        {
            foreach (UpgradeDefinition def in UpgradeCatalog.All)
            {
                float previous = UpgradeMath.ValueFor(def, 0);
                for (int level = 1; level <= LevelsToCheck; level++)
                {
                    float value = UpgradeMath.ValueFor(def, level);
                    float expected = def.BaseValue + def.ValuePerLevel * level;
                    Assert.AreEqual(expected, value, 1e-3f, def.DisplayName + " level " + level);
                    Assert.AreEqual(def.ValuePerLevel, value - previous, 1e-3f, def.DisplayName + " step " + level);
                    Assert.Greater(value, previous, def.DisplayName + " must grow each level");
                    previous = value;
                }
            }
        }

        [Test]
        public void ValueFor_KnownValues()
        {
            Assert.AreEqual(15f, UpgradeMath.ValueFor(UpgradeCatalog.Get(UpgradeType.StartingUnits), 5), FloatTolerance);
            Assert.AreEqual(4f, UpgradeMath.ValueFor(UpgradeCatalog.Get(UpgradeType.UnitDamage), 3), FloatTolerance);
            Assert.AreEqual(1.5f, UpgradeMath.ValueFor(UpgradeCatalog.Get(UpgradeType.CoinIncome), 5), FloatTolerance);
        }

        [Test]
        public void ValueFor_NegativeLevel_IsTreatedAsZero()
        {
            foreach (UpgradeDefinition def in UpgradeCatalog.All)
            {
                Assert.AreEqual(UpgradeMath.ValueFor(def, 0), UpgradeMath.ValueFor(def, -7), FloatTolerance, def.DisplayName);
            }
        }

        [Test]
        public void IsMaxed_UnlimitedUpgrade_NeverMaxes()
        {
            UpgradeDefinition unlimited = MakeDefinition(10, 1.5f, 1f, 1f, 0);
            Assert.IsFalse(UpgradeMath.IsMaxed(unlimited, 0));
            Assert.IsFalse(UpgradeMath.IsMaxed(unlimited, 1));
            Assert.IsFalse(UpgradeMath.IsMaxed(unlimited, 1000));
            Assert.IsFalse(UpgradeMath.IsMaxed(unlimited, int.MaxValue));
        }

        [Test]
        public void IsMaxed_LimitedUpgrade_MaxesAtMaxLevel()
        {
            UpgradeDefinition limited = MakeDefinition(10, 1.5f, 1f, 1f, 3);
            Assert.IsFalse(UpgradeMath.IsMaxed(limited, 0));
            Assert.IsFalse(UpgradeMath.IsMaxed(limited, 1));
            Assert.IsFalse(UpgradeMath.IsMaxed(limited, 2));
            Assert.IsTrue(UpgradeMath.IsMaxed(limited, 3));
            Assert.IsTrue(UpgradeMath.IsMaxed(limited, 4));
            Assert.IsTrue(UpgradeMath.IsMaxed(limited, 100));
        }

        [Test]
        public void IsMaxed_MaxLevelOne_MaxesAfterFirstPurchase()
        {
            UpgradeDefinition single = MakeDefinition(10, 1.5f, 1f, 1f, 1);
            Assert.IsFalse(UpgradeMath.IsMaxed(single, 0));
            Assert.IsTrue(UpgradeMath.IsMaxed(single, 1));
        }

        [Test]
        public void IsMaxed_CatalogUpgrades_FollowTheirMaxLevel()
        {
            foreach (UpgradeDefinition def in UpgradeCatalog.All)
            {
                Assert.IsFalse(UpgradeMath.IsMaxed(def, 0), def.DisplayName + " must be purchasable at level 0");
                if (def.MaxLevel <= 0)
                {
                    Assert.IsFalse(UpgradeMath.IsMaxed(def, 1000), def.DisplayName + " is unlimited");
                }
                else
                {
                    Assert.IsFalse(UpgradeMath.IsMaxed(def, def.MaxLevel - 1), def.DisplayName);
                    Assert.IsTrue(UpgradeMath.IsMaxed(def, def.MaxLevel), def.DisplayName);
                }
            }
        }

        private static UpgradeDefinition MakeDefinition(int baseCost, float costGrowth, float baseValue, float valuePerLevel, int maxLevel)
        {
            return new UpgradeDefinition
            {
                Type = UpgradeType.StartingUnits,
                DisplayName = "Test",
                BaseCost = baseCost,
                CostGrowth = costGrowth,
                BaseValue = baseValue,
                ValuePerLevel = valuePerLevel,
                MaxLevel = maxLevel,
            };
        }
    }
}
