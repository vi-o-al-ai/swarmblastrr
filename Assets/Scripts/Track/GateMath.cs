using System.Globalization;
using UnityEngine;

namespace Swarmblastrr.Track
{
    /// <summary>
    /// Pure arithmetic for gates: applying an operation to a unit count with clamping, and the
    /// label / colour that describe it. No Unity dependency apart from <see cref="Color"/>.
    /// </summary>
    public static class GateMath
    {
        /// <summary>Ceiling for the logical unit count. Results are clamped to [0, MaxCount].</summary>
        public const int MaxCount = 1_000_000;

        private const string PlusSign = "+";
        private const string MinusSign = "−";
        private const string TimesSign = "×";
        private const string DivideSign = "÷";

        /// <summary>
        /// Applies <paramref name="op"/> with <paramref name="value"/> to <paramref name="count"/>.
        /// Uses 64-bit arithmetic so large multiplications never overflow, then clamps the result
        /// to [0, <see cref="MaxCount"/>]. Dividing by zero or a negative value is a no-op.
        /// </summary>
        public static int Apply(int count, GateOperation op, int value)
        {
            long result;
            switch (op)
            {
                case GateOperation.Add:
                    result = (long)count + value;
                    break;
                case GateOperation.Subtract:
                    result = (long)count - value;
                    break;
                case GateOperation.Multiply:
                    result = (long)count * value;
                    break;
                case GateOperation.Divide:
                    result = value <= 0 ? count : (long)count / value;
                    break;
                default:
                    result = count;
                    break;
            }

            if (result < 0L)
            {
                return 0;
            }

            if (result > MaxCount)
            {
                return MaxCount;
            }

            return (int)result;
        }

        /// <summary>
        /// Short display string for a gate: "+5", "−5" (U+2212), "×3" (U+00D7) or "÷2" (U+00F7).
        /// </summary>
        public static string Label(GateOperation op, int value)
        {
            string number = value.ToString(CultureInfo.InvariantCulture);
            switch (op)
            {
                case GateOperation.Add:
                    return PlusSign + number;
                case GateOperation.Subtract:
                    return MinusSign + number;
                case GateOperation.Multiply:
                    return TimesSign + number;
                case GateOperation.Divide:
                    return DivideSign + number;
                default:
                    return number;
            }
        }

        /// <summary>
        /// True when the gate helps the player: an addition of more than zero or a multiplication
        /// by more than one. Everything else (including ×1 and +0) counts as negative.
        /// </summary>
        public static bool IsPositive(GateOperation op, int value)
        {
            switch (op)
            {
                case GateOperation.Add:
                    return value > 0;
                case GateOperation.Multiply:
                    return value > 1;
                default:
                    return false;
            }
        }

        /// <summary>Blue (0.25, 0.6, 1) for positive gates, red (1, 0.3, 0.3) for negative ones.</summary>
        public static Color ColorFor(GateOperation op, int value)
        {
            return IsPositive(op, value)
                ? new Color(0.25f, 0.6f, 1f, 1f)
                : new Color(1f, 0.3f, 0.3f, 1f);
        }
    }
}
