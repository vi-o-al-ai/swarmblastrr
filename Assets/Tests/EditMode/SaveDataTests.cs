using NUnit.Framework;
using Swarmblastrr.Core;
using Swarmblastrr.Meta;

namespace Swarmblastrr.Tests
{
    /// <summary>
    /// Edit-mode tests for <see cref="SaveData"/>, <see cref="SaveSystem"/> and
    /// <see cref="MemorySaveStore"/>: round-trips through the in-memory store, corrupt or partial
    /// JSON yielding fresh data, <see cref="SaveData.EnsureArrays"/> and <see cref="SaveSystem.Reset"/>.
    /// </summary>
    [TestFixture]
    public class SaveDataTests
    {
        private ISaveStore _previousStore;
        private MemorySaveStore _store;

        [SetUp]
        public void SetUp()
        {
            _previousStore = SaveSystem.Store;
            _store = new MemorySaveStore();
            SaveSystem.Store = _store;
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.Store = _previousStore;
            _previousStore = null;
            _store = null;

            // Nothing under test raises events; clearing keeps a stray subscriber from leaking into other fixtures.
            GameEvents.ClearAll();
        }

        [Test]
        public void CreateNew_HasFreshDefaults()
        {
            SaveData data = SaveData.CreateNew();
            Assert.AreEqual(1, data.Version);
            Assert.AreEqual(0, data.Coins);
            Assert.AreEqual(0, data.LevelIndex);
            Assert.IsNotNull(data.UpgradeLevels);
            Assert.AreEqual(UpgradeCatalog.All.Length, data.UpgradeLevels.Length);
            CollectionAssert.AreEqual(new int[UpgradeCatalog.All.Length], data.UpgradeLevels);
        }

        [Test]
        public void CreateNew_ReturnsDistinctInstances()
        {
            SaveData a = SaveData.CreateNew();
            SaveData b = SaveData.CreateNew();
            Assert.AreNotSame(a, b);
            Assert.AreNotSame(a.UpgradeLevels, b.UpgradeLevels);
        }

        [Test]
        public void MemorySaveStore_StartsEmpty_ThenHoldsWrites()
        {
            Assert.IsFalse(_store.Exists);
            Assert.IsNull(_store.Read());

            _store.Write("{}");
            Assert.IsTrue(_store.Exists);
            Assert.AreEqual("{}", _store.Read());

            _store.Write("{\"Coins\":1}");
            Assert.AreEqual("{\"Coins\":1}", _store.Read(), "a second write replaces the first");

            _store.Delete();
            Assert.IsFalse(_store.Exists);
            Assert.IsNull(_store.Read());
        }

        [Test]
        public void MemorySaveStore_PreloadedJson_Exists()
        {
            MemorySaveStore preloaded = new MemorySaveStore("{\"Coins\":3}");
            Assert.IsTrue(preloaded.Exists);
            Assert.AreEqual("{\"Coins\":3}", preloaded.Read());

            MemorySaveStore empty = new MemorySaveStore(null);
            Assert.IsFalse(empty.Exists);
            Assert.IsNull(empty.Read());
        }

        [Test]
        public void MemorySaveStore_WriteNull_StoresEmptyRecord()
        {
            _store.Write(null);
            Assert.IsTrue(_store.Exists);
            Assert.AreEqual(string.Empty, _store.Read());
        }

        [Test]
        public void Store_Setter_SwapsBackingStore()
        {
            MemorySaveStore other = new MemorySaveStore();
            SaveSystem.Store = other;
            Assert.AreSame(other, SaveSystem.Store);

            SaveData data = SaveData.CreateNew();
            data.Coins = 5;
            SaveSystem.Save(data);

            Assert.IsTrue(other.Exists);
            Assert.IsFalse(_store.Exists, "the replaced store must not be written");
        }

        [Test]
        public void Load_EmptyStore_ReturnsNewData()
        {
            SaveData data = SaveSystem.Load();
            Assert.IsNotNull(data);
            Assert.AreEqual(1, data.Version);
            Assert.AreEqual(0, data.Coins);
            Assert.AreEqual(0, data.LevelIndex);
            Assert.IsNotNull(data.UpgradeLevels);
            Assert.AreEqual(UpgradeCatalog.All.Length, data.UpgradeLevels.Length);
            CollectionAssert.AreEqual(new int[UpgradeCatalog.All.Length], data.UpgradeLevels);
            Assert.IsFalse(_store.Exists, "Load must not write");
        }

        [Test]
        public void Load_EmptyString_ReturnsNewData()
        {
            _store.Write(string.Empty);
            Assert.IsTrue(_store.Exists);
            SaveData data = SaveSystem.Load();
            Assert.IsNotNull(data);
            Assert.AreEqual(0, data.Coins);
            Assert.AreEqual(UpgradeCatalog.All.Length, data.UpgradeLevels.Length);
        }

        [Test]
        public void SaveThenLoad_RoundTripsEveryField()
        {
            SaveData data = SaveData.CreateNew();
            data.Coins = 1234;
            data.LevelIndex = 7;
            data.UpgradeLevels[(int)UpgradeType.StartingUnits] = 3;
            data.UpgradeLevels[(int)UpgradeType.UnitDamage] = 1;
            data.UpgradeLevels[(int)UpgradeType.CoinIncome] = 12;

            SaveSystem.Save(data);
            Assert.IsTrue(_store.Exists);

            SaveData loaded = SaveSystem.Load();
            Assert.IsNotNull(loaded);
            Assert.AreNotSame(data, loaded);
            Assert.AreEqual(data.Version, loaded.Version);
            Assert.AreEqual(1234, loaded.Coins);
            Assert.AreEqual(7, loaded.LevelIndex);
            CollectionAssert.AreEqual(data.UpgradeLevels, loaded.UpgradeLevels);
        }

        [Test]
        public void SaveThenLoad_LargeValues_Survive()
        {
            SaveData data = SaveData.CreateNew();
            data.Coins = int.MaxValue;
            data.LevelIndex = 100_000;
            data.UpgradeLevels[0] = 9_999;

            SaveSystem.Save(data);
            SaveData loaded = SaveSystem.Load();

            Assert.AreEqual(int.MaxValue, loaded.Coins);
            Assert.AreEqual(100_000, loaded.LevelIndex);
            Assert.AreEqual(9_999, loaded.UpgradeLevels[0]);
        }

        [Test]
        public void Save_WritesPrettyPrintedJsonWithPublicFields()
        {
            SaveData data = SaveData.CreateNew();
            data.Coins = 42;
            SaveSystem.Save(data);

            string json = _store.Read();
            Assert.IsNotNull(json);
            StringAssert.Contains("\"Version\"", json);
            StringAssert.Contains("\"Coins\"", json);
            StringAssert.Contains("42", json);
            StringAssert.Contains("\"LevelIndex\"", json);
            StringAssert.Contains("\"UpgradeLevels\"", json);
            StringAssert.Contains("\n", json);
        }

        [Test]
        public void Save_OverwritesPreviousRecord()
        {
            SaveData data = SaveData.CreateNew();
            data.Coins = 1;
            SaveSystem.Save(data);
            data.Coins = 2;
            SaveSystem.Save(data);

            Assert.AreEqual(2, SaveSystem.Load().Coins);
        }

        [Test]
        public void Save_Null_IsIgnored()
        {
            Assert.DoesNotThrow(() => SaveSystem.Save(null));
            Assert.IsFalse(_store.Exists);
        }

        [Test]
        public void Save_NormalisesDataBeforeWriting()
        {
            SaveData data = SaveData.CreateNew();
            data.Coins = -20;
            data.UpgradeLevels = null;

            SaveSystem.Save(data);

            Assert.AreEqual(0, data.Coins, "Save runs EnsureArrays on the record it is given");
            Assert.IsNotNull(data.UpgradeLevels);

            SaveData loaded = SaveSystem.Load();
            Assert.AreEqual(0, loaded.Coins);
            Assert.AreEqual(UpgradeCatalog.All.Length, loaded.UpgradeLevels.Length);
        }

        [Test]
        public void Load_CorruptJson_ReturnsNewData()
        {
            string[] corrupt =
            {
                "not json at all",
                "{",
                "}{",
                "{\"Coins\": }",
                "[1, 2, 3",
                "null",
                "\"just a string\"",
            };

            foreach (string json in corrupt)
            {
                SaveSystem.Store = new MemorySaveStore(json);
                SaveData data = null;
                Assert.DoesNotThrow(() => data = SaveSystem.Load(), "input: " + json);
                Assert.IsNotNull(data, "input: " + json);
                Assert.AreEqual(0, data.Coins, "input: " + json);
                Assert.AreEqual(0, data.LevelIndex, "input: " + json);
                Assert.IsNotNull(data.UpgradeLevels, "input: " + json);
                Assert.AreEqual(UpgradeCatalog.All.Length, data.UpgradeLevels.Length, "input: " + json);
                Assert.GreaterOrEqual(data.Version, 1, "input: " + json);
            }
        }

        [Test]
        public void Load_PartialJsonFromOlderBuild_FillsMissingFields()
        {
            SaveSystem.Store = new MemorySaveStore("{\"Coins\":77,\"LevelIndex\":4}");

            SaveData data = SaveSystem.Load();

            Assert.AreEqual(77, data.Coins);
            Assert.AreEqual(4, data.LevelIndex);
            Assert.IsNotNull(data.UpgradeLevels);
            Assert.AreEqual(UpgradeCatalog.All.Length, data.UpgradeLevels.Length);
            CollectionAssert.AreEqual(new int[UpgradeCatalog.All.Length], data.UpgradeLevels);
            Assert.GreaterOrEqual(data.Version, 1);
        }

        [Test]
        public void Load_ShortUpgradeArray_IsGrownPreservingValues()
        {
            SaveSystem.Store = new MemorySaveStore("{\"Version\":1,\"Coins\":5,\"LevelIndex\":0,\"UpgradeLevels\":[9]}");

            SaveData data = SaveSystem.Load();

            Assert.AreEqual(5, data.Coins);
            Assert.AreEqual(UpgradeCatalog.All.Length, data.UpgradeLevels.Length);
            Assert.AreEqual(9, data.UpgradeLevels[0]);
            for (int i = 1; i < data.UpgradeLevels.Length; i++)
            {
                Assert.AreEqual(0, data.UpgradeLevels[i], "slot " + i);
            }
        }

        [Test]
        public void Load_NegativeValues_AreClampedToZero()
        {
            SaveSystem.Store = new MemorySaveStore("{\"Version\":1,\"Coins\":-50,\"LevelIndex\":-2,\"UpgradeLevels\":[-1,2,-3]}");

            SaveData data = SaveSystem.Load();

            Assert.AreEqual(0, data.Coins);
            Assert.AreEqual(0, data.LevelIndex);
            Assert.AreEqual(UpgradeCatalog.All.Length, data.UpgradeLevels.Length);
            Assert.AreEqual(0, data.UpgradeLevels[0]);
            Assert.AreEqual(2, data.UpgradeLevels[1]);
            Assert.AreEqual(0, data.UpgradeLevels[2]);
        }

        [Test]
        public void EnsureArrays_NullArray_CreatesIt()
        {
            SaveData data = new SaveData { UpgradeLevels = null };
            data.EnsureArrays();
            Assert.IsNotNull(data.UpgradeLevels);
            Assert.AreEqual(UpgradeCatalog.All.Length, data.UpgradeLevels.Length);
            CollectionAssert.AreEqual(new int[UpgradeCatalog.All.Length], data.UpgradeLevels);
        }

        [Test]
        public void EnsureArrays_ShortArray_GrowsAndKeepsValues()
        {
            SaveData data = new SaveData { UpgradeLevels = new[] { 5 } };
            data.EnsureArrays();
            Assert.AreEqual(UpgradeCatalog.All.Length, data.UpgradeLevels.Length);
            Assert.AreEqual(5, data.UpgradeLevels[0]);
            for (int i = 1; i < data.UpgradeLevels.Length; i++)
            {
                Assert.AreEqual(0, data.UpgradeLevels[i], "slot " + i);
            }
        }

        [Test]
        public void EnsureArrays_EmptyArray_Grows()
        {
            SaveData data = new SaveData { UpgradeLevels = new int[0] };
            data.EnsureArrays();
            Assert.AreEqual(UpgradeCatalog.All.Length, data.UpgradeLevels.Length);
        }

        [Test]
        public void EnsureArrays_LongerArray_IsKept()
        {
            int[] longer = { 1, 2, 3, 4, 5 };
            SaveData data = new SaveData { UpgradeLevels = longer };
            data.EnsureArrays();
            Assert.AreSame(longer, data.UpgradeLevels);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, data.UpgradeLevels);
        }

        [Test]
        public void EnsureArrays_ExactLength_IsUntouched()
        {
            int[] exact = new int[UpgradeCatalog.All.Length];
            for (int i = 0; i < exact.Length; i++)
            {
                exact[i] = i + 1;
            }

            SaveData data = new SaveData { UpgradeLevels = exact };
            data.EnsureArrays();
            Assert.AreSame(exact, data.UpgradeLevels);
            for (int i = 0; i < exact.Length; i++)
            {
                Assert.AreEqual(i + 1, data.UpgradeLevels[i], "slot " + i);
            }
        }

        [Test]
        public void EnsureArrays_ClampsNegativeValues()
        {
            SaveData data = new SaveData
            {
                Version = 0,
                Coins = -1,
                LevelIndex = -9,
                UpgradeLevels = new[] { 0, -4, 2 },
            };

            data.EnsureArrays();

            Assert.AreEqual(1, data.Version);
            Assert.AreEqual(0, data.Coins);
            Assert.AreEqual(0, data.LevelIndex);
            Assert.AreEqual(0, data.UpgradeLevels[1]);
            Assert.AreEqual(2, data.UpgradeLevels[2]);
        }

        [Test]
        public void EnsureArrays_IsIdempotent()
        {
            SaveData data = SaveData.CreateNew();
            data.Coins = 10;
            data.LevelIndex = 3;
            data.UpgradeLevels[1] = 4;

            data.EnsureArrays();
            int[] afterFirst = data.UpgradeLevels;
            data.EnsureArrays();

            Assert.AreSame(afterFirst, data.UpgradeLevels);
            Assert.AreEqual(10, data.Coins);
            Assert.AreEqual(3, data.LevelIndex);
            Assert.AreEqual(4, data.UpgradeLevels[1]);
        }

        [Test]
        public void Reset_DeletesRecord_SoLoadStartsFresh()
        {
            SaveData data = SaveData.CreateNew();
            data.Coins = 99;
            data.LevelIndex = 5;
            SaveSystem.Save(data);
            Assert.IsTrue(_store.Exists);

            SaveSystem.Reset();

            Assert.IsFalse(_store.Exists);
            SaveData loaded = SaveSystem.Load();
            Assert.AreEqual(0, loaded.Coins);
            Assert.AreEqual(0, loaded.LevelIndex);
        }

        [Test]
        public void Reset_OnEmptyStore_DoesNotThrow()
        {
            Assert.IsFalse(_store.Exists);
            Assert.DoesNotThrow(() => SaveSystem.Reset());
            Assert.IsFalse(_store.Exists);
        }
    }
}
