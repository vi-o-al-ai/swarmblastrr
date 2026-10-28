using System;
using NUnit.Framework;
using UnityEngine;
using Swarmblastrr.Track;

namespace Swarmblastrr.Tests
{
    /// <summary>
    /// Edit-mode tests for <see cref="GateMath"/>: every operation, clamping at 0 and
    /// <see cref="GateMath.MaxCount"/>, divide-by-zero, labels, polarity and colours.
    /// </summary>
    [TestFixture]
    public class GateMathTests
    {
        private const float ColorTolerance = 1e-4f;

        private static readonly Color PositiveColor = new Color(0.25f, 0.6f, 1f);
        private static readonly Color NegativeColor = new Color(1f, 0.3f, 0.3f);

        [Test]
        public void MaxCount_IsOneMillion()
        {
            Assert.AreEqual(1_000_000, GateMath.MaxCount);
        }

        [Test]
        public void Apply_Add_AddsValue()
        {
            Assert.AreEqual(15, GateMath.Apply(10, GateOperation.Add, 5));
            Assert.AreEqual(10, GateMath.Apply(10, GateOperation.Add, 0));
            Assert.AreEqual(1, GateMath.Apply(0, GateOperation.Add, 1));
        }

        [Test]
        public void Apply_Subtract_SubtractsValue()
        {
            Assert.AreEqual(7, GateMath.Apply(10, GateOperation.Subtract, 3));
            Assert.AreEqual(0, GateMath.Apply(10, GateOperation.Subtract, 10));
            Assert.AreEqual(10, GateMath.Apply(10, GateOperation.Subtract, 0));
        }

        [Test]
        public void Apply_Subtract_NegativeValue_Adds()
        {
            Assert.AreEqual(10, GateMath.Apply(5, GateOperation.Subtract, -5));
        }

        [Test]
        public void Apply_Multiply_MultipliesValue()
        {
            Assert.AreEqual(30, GateMath.Apply(10, GateOperation.Multiply, 3));
            Assert.AreEqual(10, GateMath.Apply(10, GateOperation.Multiply, 1));
            Assert.AreEqual(0, GateMath.Apply(10, GateOperation.Multiply, 0));
            Assert.AreEqual(0, GateMath.Apply(0, GateOperation.Multiply, 1000));
        }

        [Test]
        public void Apply_Divide_UsesIntegerDivision()
        {
            Assert.AreEqual(5, GateMath.Apply(10, GateOperation.Divide, 2));
            Assert.AreEqual(3, GateMath.Apply(7, GateOperation.Divide, 2));
            Assert.AreEqual(0, GateMath.Apply(1, GateOperation.Divide, 2));
            Assert.AreEqual(10, GateMath.Apply(10, GateOperation.Divide, 1));
        }

        [Test]
        public void Apply_DivideByZeroOrNegative_LeavesCountUnchanged()
        {
            Assert.AreEqual(10, GateMath.Apply(10, GateOperation.Divide, 0));
            Assert.AreEqual(10, GateMath.Apply(10, GateOperation.Divide, -3));
            Assert.AreEqual(0, GateMath.Apply(0, GateOperation.Divide, 0));
            Assert.AreEqual(GateMath.MaxCount, GateMath.Apply(GateMath.MaxCount, GateOperation.Divide, 0));
        }

        [Test]
        public void Apply_ClampsAtZero()
        {
            Assert.AreEqual(0, GateMath.Apply(3, GateOperation.Subtract, 10));
            Assert.AreEqual(0, GateMath.Apply(5, GateOperation.Add, -10));
            Assert.AreEqual(0, GateMath.Apply(5, GateOperation.Multiply, -2));
            Assert.AreEqual(0, GateMath.Apply(-5, GateOperation.Add, 0));
            Assert.AreEqual(0, GateMath.Apply(-5, GateOperation.Divide, 2));
        }

        [Test]
        public void Apply_ClampsAtMaxCount()
        {
            Assert.AreEqual(GateMath.MaxCount, GateMath.Apply(GateMath.MaxCount - 1, GateOperation.Add, 10));
            Assert.AreEqual(GateMath.MaxCount, GateMath.Apply(GateMath.MaxCount, GateOperation.Add, 1));
            Assert.AreEqual(GateMath.MaxCount, GateMath.Apply(GateMath.MaxCount, GateOperation.Multiply, 2));
            Assert.AreEqual(GateMath.MaxCount, GateMath.Apply(GateMath.MaxCount / 2 + 1, GateOperation.Multiply, 2));
            Assert.AreEqual(GateMath.MaxCount, GateMath.Apply(GateMath.MaxCount, GateOperation.Subtract, 0));
        }

        [Test]
        public void Apply_ExactlyMaxCount_IsNotClamped()
        {
            Assert.AreEqual(GateMath.MaxCount, GateMath.Apply(GateMath.MaxCount - 1, GateOperation.Add, 1));
            Assert.AreEqual(GateMath.MaxCount, GateMath.Apply(GateMath.MaxCount / 2, GateOperation.Multiply, 2));
            Assert.AreEqual(GateMath.MaxCount - 1, GateMath.Apply(GateMath.MaxCount, GateOperation.Subtract, 1));
        }

        [Test]
        public void Apply_LargeOperands_DoNotOverflow()
        {
            Assert.AreEqual(GateMath.MaxCount, GateMath.Apply(int.MaxValue, GateOperation.Multiply, int.MaxValue));
            Assert.AreEqual(GateMath.MaxCount, GateMath.Apply(int.MaxValue, GateOperation.Add, int.MaxValue));
            Assert.AreEqual(0, GateMath.Apply(int.MinValue, GateOperation.Multiply, int.MaxValue));
            Assert.AreEqual(0, GateMath.Apply(int.MinValue, GateOperation.Subtract, int.MaxValue));
            Assert.AreEqual(GateMath.MaxCount, GateMath.Apply(int.MaxValue, GateOperation.Subtract, int.MinValue));
        }

        [Test]
        public void Apply_ResultIsAlwaysWithinRange()
        {
            int[] counts = { int.MinValue, -1, 0, 1, 999, GateMath.MaxCount, int.MaxValue };
            int[] values = { int.MinValue, -7, 0, 1, 2, 1000, GateMath.MaxCount, int.MaxValue };

            foreach (GateOperation op in Enum.GetValues(typeof(GateOperation)))
            {
                foreach (int count in counts)
                {
                    foreach (int value in values)
                    {
                        int result = GateMath.Apply(count, op, value);
                        string context = op + " count=" + count + " value=" + value;
                        Assert.GreaterOrEqual(result, 0, context);
                        Assert.LessOrEqual(result, GateMath.MaxCount, context);
                    }
                }
            }
        }

        [Test]
        public void Label_UsesTypographicSigns()
        {
            Assert.AreEqual("+5", GateMath.Label(GateOperation.Add, 5));
            Assert.AreEqual("−5", GateMath.Label(GateOperation.Subtract, 5));
            Assert.AreEqual("×3", GateMath.Label(GateOperation.Multiply, 3));
            Assert.AreEqual("÷2", GateMath.Label(GateOperation.Divide, 2));
        }

        [Test]
        public void Label_Subtract_UsesMinusSignNotHyphen()
        {
            string label = GateMath.Label(GateOperation.Subtract, 12);
            Assert.AreEqual(3, label.Length);
            Assert.AreEqual('−', label[0]);
            Assert.AreNotEqual('-', label[0]);
            Assert.AreEqual("12", label.Substring(1));
        }

        [Test]
        public void Label_LargeValue_HasNoDigitGrouping()
        {
            Assert.AreEqual("+1000000", GateMath.Label(GateOperation.Add, 1_000_000));
            Assert.AreEqual("×250", GateMath.Label(GateOperation.Multiply, 250));
        }

        [Test]
        public void Label_Zero_StillCarriesSign()
        {
            Assert.AreEqual("+0", GateMath.Label(GateOperation.Add, 0));
            Assert.AreEqual("−0", GateMath.Label(GateOperation.Subtract, 0));
        }

        [Test]
        public void IsPositive_Add_OnlyAboveZero()
        {
            Assert.IsTrue(GateMath.IsPositive(GateOperation.Add, 5));
            Assert.IsTrue(GateMath.IsPositive(GateOperation.Add, 1));
            Assert.IsFalse(GateMath.IsPositive(GateOperation.Add, 0));
            Assert.IsFalse(GateMath.IsPositive(GateOperation.Add, -3));
        }

        [Test]
        public void IsPositive_Multiply_OnlyAboveOne()
        {
            Assert.IsTrue(GateMath.IsPositive(GateOperation.Multiply, 2));
            Assert.IsTrue(GateMath.IsPositive(GateOperation.Multiply, 10));
            Assert.IsFalse(GateMath.IsPositive(GateOperation.Multiply, 1));
            Assert.IsFalse(GateMath.IsPositive(GateOperation.Multiply, 0));
            Assert.IsFalse(GateMath.IsPositive(GateOperation.Multiply, -1));
        }

        [Test]
        public void IsPositive_SubtractAndDivide_AreNeverPositive()
        {
            int[] values = { -5, 0, 1, 2, 100 };
            foreach (int value in values)
            {
                Assert.IsFalse(GateMath.IsPositive(GateOperation.Subtract, value), "Subtract " + value);
                Assert.IsFalse(GateMath.IsPositive(GateOperation.Divide, value), "Divide " + value);
            }
        }

        [Test]
        public void ColorFor_PositiveGate_IsBlue()
        {
            AssertColor(PositiveColor, GateMath.ColorFor(GateOperation.Add, 5));
            AssertColor(PositiveColor, GateMath.ColorFor(GateOperation.Multiply, 2));
        }

        [Test]
        public void ColorFor_NegativeGate_IsRed()
        {
            AssertColor(NegativeColor, GateMath.ColorFor(GateOperation.Subtract, 5));
            AssertColor(NegativeColor, GateMath.ColorFor(GateOperation.Divide, 2));
            AssertColor(NegativeColor, GateMath.ColorFor(GateOperation.Add, 0));
            AssertColor(NegativeColor, GateMath.ColorFor(GateOperation.Multiply, 1));
        }

        [Test]
        public void ColorFor_MatchesIsPositive()
        {
            int[] values = { -2, 0, 1, 2, 7 };
            foreach (GateOperation op in Enum.GetValues(typeof(GateOperation)))
            {
                foreach (int value in values)
                {
                    Color expected = GateMath.IsPositive(op, value) ? PositiveColor : NegativeColor;
                    AssertColor(expected, GateMath.ColorFor(op, value));
                }
            }
        }

        private static void AssertColor(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, ColorTolerance, "r");
            Assert.AreEqual(expected.g, actual.g, ColorTolerance, "g");
            Assert.AreEqual(expected.b, actual.b, ColorTolerance, "b");
        }
    }
}
