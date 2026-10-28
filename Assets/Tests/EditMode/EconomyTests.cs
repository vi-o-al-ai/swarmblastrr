using NUnit.Framework;
using Swarmblastrr.Core;
using Swarmblastrr.Meta;

namespace Swarmblastrr.Tests
{
    /// <summary>
    /// Edit-mode tests for <see cref="Economy"/>: win and lose rewards, the income multiplier,
    /// config-driven tunables, the null-config fallback and clamping of bad inputs.
    /// </summary>
    [TestFixture]
    public class EconomyTests
    {
        private const int MaxCount = 1_000_000;

        private GameConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = GameConfig.CreateDefault();
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null)
            {
                UnityEngine.Object.DestroyImmediate(_config);
                _config = null;
            }

            // Nothing under test raises events; clearing keeps a stray subscriber from leaking into other fixtures.
            GameEvents.ClearAll();
        }

        [Test]
        public void CreateDefault_HasContractEconomyDefaults()
        {
            Assert.IsNotNull(_config);
            Assert.AreEqual(1, _config.CoinsPerSurvivingUnit);
            Assert.AreEqual(50, _config.LevelClearBonus);
        }

        [Test]
        public void WinReward_BaseCase_IsUnitsPlusClearBonus()
        {
            Assert.AreEqual(150, Economy.WinReward(100, 1, 1f, 0, _config));
            Assert.AreEqual(51, Economy.WinReward(1, 1, 1f, 0, _config));
        }

        [Test]
        public void WinReward_NoSurvivors_IsClearBonusOnly()
        {
            Assert.AreEqual(50, Economy.WinReward(0, 1, 1f, 0, _config));
            Assert.AreEqual(50, Economy.WinReward(0, 100, 1f, 0, _config));
        }

        [Test]
        public void WinReward_RampMultiplier_ScalesUnitCoinsOnly()
        {
            Assert.AreEqual(350, Economy.WinReward(100, 3, 1f, 0, _config));
            Assert.AreEqual(1050, Economy.WinReward(100, 10, 1f, 0, _config));
            Assert.AreEqual(Economy.WinReward(100, 1, 1f, 0, _config) + 100, Economy.WinReward(100, 2, 1f, 0, _config));
        }

        [Test]
        public void WinReward_LevelIndex_AddsTenPerLevel()
        {
            Assert.AreEqual(170, Economy.WinReward(100, 1, 1f, 2, _config));
            for (int level = 0; level < 20; level++)
            {
                Assert.AreEqual(
                    Economy.WinReward(100, 1, 1f, level, _config) + 10,
                    Economy.WinReward(100, 1, 1f, level + 1, _config),
                    "level " + level);
            }
        }

        [Test]
        public void WinReward_IncomeMultiplier_ScalesWholeReward()
        {
            Assert.AreEqual(225, Economy.WinReward(100, 1, 1.5f, 0, _config));
            Assert.AreEqual(300, Economy.WinReward(100, 1, 2f, 0, _config));
            Assert.AreEqual(740, Economy.WinReward(100, 3, 2f, 2, _config));
            Assert.AreEqual(100, Economy.WinReward(0, 1, 2f, 0, _config), "the clear bonus is multiplied too");
        }

        [Test]
        public void WinReward_RoundsToNearestCoin()
        {
            Assert.AreEqual(66, Economy.WinReward(10, 1, 1.1f, 0, _config));
            Assert.AreEqual(66, Economy.WinReward(1, 1, 1.3f, 0, _config));
            Assert.AreEqual(69, Economy.WinReward(3, 1, 1.3f, 0, _config));
        }

        [Test]
        public void WinReward_UsesConfigTunables()
        {
            _config.CoinsPerSurvivingUnit = 2;
            _config.LevelClearBonus = 10;
            Assert.AreEqual(50, Economy.WinReward(10, 2, 1f, 0, _config));

            _config.CoinsPerSurvivingUnit = 0;
            _config.LevelClearBonus = 0;
            Assert.AreEqual(0, Economy.WinReward(10, 2, 1f, 0, _config));
            Assert.AreEqual(30, Economy.WinReward(10, 2, 1f, 3, _config), "the per-level bonus does not come from the config");
        }

        [Test]
        public void WinReward_NullConfig_UsesContractDefaults()
        {
            Assert.AreEqual(150, Economy.WinReward(100, 1, 1f, 0, null));
            Assert.AreEqual(
                Economy.WinReward(100, 2, 1.5f, 3, _config),
                Economy.WinReward(100, 2, 1.5f, 3, null),
                "the default config must match the null fallback");
        }

        [Test]
        public void WinReward_ZeroIncomeMultiplier_YieldsNothing()
        {
            Assert.AreEqual(0, Economy.WinReward(100, 3, 0f, 5, _config));
        }

        [Test]
        public void WinReward_NegativeInputs_AreClamped()
        {
            Assert.AreEqual(50, Economy.WinReward(-10, 1, 1f, 0, _config), "negative survivors count as 0");
            Assert.AreEqual(150, Economy.WinReward(100, 0, 1f, 0, _config), "ramp multiplier below 1 counts as 1");
            Assert.AreEqual(150, Economy.WinReward(100, -5, 1f, 0, _config), "ramp multiplier below 1 counts as 1");
            Assert.AreEqual(150, Economy.WinReward(100, 1, 1f, -3, _config), "negative level index counts as 0");
            Assert.AreEqual(0, Economy.WinReward(100, 1, -1f, 0, _config), "negative income yields nothing");
            Assert.GreaterOrEqual(Economy.WinReward(-1, -1, -1f, -1, _config), 0);
        }

        [Test]
        public void WinReward_LargeInputs_DoNotOverflow()
        {
            Assert.AreEqual(100_000_050, Economy.WinReward(MaxCount, 100, 1f, 0, _config));
            int huge = 0;
            Assert.DoesNotThrow(() => huge = Economy.WinReward(int.MaxValue, int.MaxValue, 1000f, int.MaxValue, _config));
            Assert.GreaterOrEqual(huge, Economy.WinReward(MaxCount, 100, 1f, 0, _config), "saturation must not wrap negative");
            Assert.Greater(Economy.WinReward(MaxCount, 100, 10f, 0, _config), Economy.WinReward(MaxCount, 100, 1f, 0, _config));
        }

        [Test]
        public void LoseReward_BaseCase_IsFivePlusLevel()
        {
            Assert.AreEqual(5, Economy.LoseReward(0, 1f));
            Assert.AreEqual(8, Economy.LoseReward(3, 1f));
            Assert.AreEqual(15, Economy.LoseReward(10, 1f));
        }

        [Test]
        public void LoseReward_IncomeMultiplier_Scales()
        {
            Assert.AreEqual(10, Economy.LoseReward(0, 2f));
            Assert.AreEqual(9, Economy.LoseReward(1, 1.5f));
            Assert.AreEqual(0, Economy.LoseReward(7, 0f));
        }

        [Test]
        public void LoseReward_RoundsToNearestCoin()
        {
            Assert.AreEqual(8, Economy.LoseReward(2, 1.1f));
            Assert.AreEqual(8, Economy.LoseReward(1, 1.3f));
            Assert.AreEqual(9, Economy.LoseReward(3, 1.1f));
        }

        [Test]
        public void LoseReward_NegativeInputs_AreClamped()
        {
            Assert.AreEqual(5, Economy.LoseReward(-5, 1f));
            Assert.AreEqual(0, Economy.LoseReward(3, -2f));
        }

        [Test]
        public void LoseReward_IsAlwaysBelowWinningWithNoSurvivors()
        {
            for (int level = 0; level <= 30; level++)
            {
                Assert.Less(Economy.LoseReward(level, 1f), Economy.WinReward(0, 1, 1f, level, _config), "level " + level);
            }
        }

        [Test]
        public void Rewards_GrowWithLevelIndex()
        {
            for (int level = 0; level < 30; level++)
            {
                Assert.Greater(Economy.WinReward(50, 1, 1f, level + 1, _config), Economy.WinReward(50, 1, 1f, level, _config), "win " + level);
                Assert.Greater(Economy.LoseReward(level + 1, 1f), Economy.LoseReward(level, 1f), "lose " + level);
            }
        }

        [Test]
        public void Rewards_GrowWithIncomeMultiplier()
        {
            Assert.Greater(Economy.WinReward(100, 2, 1.1f, 3, _config), Economy.WinReward(100, 2, 1f, 3, _config));
            Assert.Greater(Economy.LoseReward(3, 2f), Economy.LoseReward(3, 1f));
        }
    }
}
