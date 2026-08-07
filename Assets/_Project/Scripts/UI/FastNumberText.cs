using TMPro;
using UnityEngine;

namespace Overdraft.UI
{
    /// <summary>
    /// Writes a long into a TMP_Text with zero allocation (docs/01 §11).
    /// <para>
    /// The main UI element of this game is a number that changes several times a second. A
    /// plain value.ToString("N0") there allocates a string every change, which is the sneakiest
    /// way to fail NFR-02. Digits are extracted by hand into a reused char[] instead.
    /// </para>
    /// </summary>
    public sealed class FastNumberText : MonoBehaviour
    {
        [SerializeField] TMP_Text text;
        [SerializeField] bool useThousandsSeparators = true;

        // long.MinValue formatted with separators is 26 characters. 32 leaves headroom and
        // keeps the buffer in one cache line.
        readonly char[] buffer = new char[32];
        long lastValue = long.MinValue;

        void Reset()
        {
            text = GetComponent<TMP_Text>();
        }

        void Awake()
        {
            if (text == null) { text = GetComponent<TMP_Text>(); }
        }

        public void SetValue(long value)
        {
            if (value == lastValue) { return; }   // most frames change nothing
            lastValue = value;

            int length = Format(value, buffer, useThousandsSeparators);
            text.SetCharArray(buffer, 0, length);
        }

        /// <summary>Forces the next SetValue to write even if the value is unchanged.</summary>
        public void Invalidate()
        {
            lastValue = long.MinValue;
        }

        /// <summary>
        /// Writes value into destination and returns the number of characters written.
        /// Internal so the EditMode suite can assert the formatting without a TMP_Text.
        /// </summary>
        internal static int Format(long value, char[] destination, bool separators)
        {
            bool negative = value < 0L;

            // long.MinValue has no positive counterpart, so it is special-cased rather than
            // negated. It cannot occur in gameplay, but a formatter that overflows on one
            // specific input is a landmine.
            if (value == long.MinValue)
            {
                const string minValue = "-9223372036854775808";
                for (int i = 0; i < minValue.Length; i++) { destination[i] = minValue[i]; }
                return minValue.Length;
            }

            if (negative) { value = -value; }

            int cursor = destination.Length;
            int digitsSinceComma = 0;

            do
            {
                if (separators && digitsSinceComma == 3)
                {
                    cursor--;
                    destination[cursor] = ',';
                    digitsSinceComma = 0;
                }

                cursor--;
                destination[cursor] = (char)('0' + (int)(value % 10L));
                value /= 10L;
                digitsSinceComma++;
            }
            while (value > 0L);

            if (negative)
            {
                cursor--;
                destination[cursor] = '-';
            }

            int length = destination.Length - cursor;

            // Digits were written from the right; shift them to the front so callers can pass
            // (buffer, 0, length) straight into SetCharArray.
            for (int i = 0; i < length; i++)
            {
                destination[i] = destination[cursor + i];
            }

            return length;
        }
    }
}
