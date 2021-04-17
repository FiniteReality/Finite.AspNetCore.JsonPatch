using System;
using System.Diagnostics.CodeAnalysis;

namespace Finite.AspNetCore.JsonPatch
{
    public partial class JsonPointer
    {
        // Valid array indexes have no leading zeros, unless it is zero.
        private static ReadOnlySpan<char> Digits
            => new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
        private static ReadOnlySpan<char> Zero
            => Digits.Slice(0, 1);
        private static ReadOnlySpan<char> OneThroughNine
            => Digits[1..];

        /// <summary>
        /// Attempts to parse the given value as a JSON Pointer.
        /// </summary>
        /// <param name="jsonPointer">
        /// The pointer to parse.
        /// </param>
        /// <param name="value">
        /// The parsed value, if parsing was successful, or <c>null</c>
        /// otherwise.
        /// </param>
        /// <returns>
        /// <c>true</c> if parsing was successful, or <c>false</c> otherwise.
        /// </returns>
        public static bool TryParse(string jsonPointer,
            [NotNullWhen(true)] out JsonPointer? value)
        {
            value = null;
            var pathSpan = jsonPointer.AsSpan();
            var startPosition = 1;

            if (pathSpan.Length == 0)
            {
                // special case: empty path refers to the whole document
                value = new JsonPointer(jsonPointer);
                return true;
            }

            var estimatedTokenCount = 0;
            for (var x = 0; x < pathSpan.Length; x++)
            {
                var position = pathSpan[x..].IndexOf('/');

                if (position < 0)
                    break;

                estimatedTokenCount++;
                x += position;
            }

            do
            {
                var nextSeparator = pathSpan[startPosition..].IndexOf('/');

                if (nextSeparator < 0)
                    nextSeparator = pathSpan.Length - startPosition;

                var range = startPosition..(startPosition + nextSeparator);
                var token = CreateToken(pathSpan[range], range);

                if (token.EscapeSequenceLocation > 0)
                {
                    if (token.EscapeSequenceLocation == pathSpan.Length - 1)
                        return false;

                    if (pathSpan[token.EscapeSequenceLocation + 1] != '0' &&
                        pathSpan[token.EscapeSequenceLocation + 1] != '1')
                        return false;
                }

                if (value == null)
                    value = new JsonPointer(jsonPointer, estimatedTokenCount, token);
                else
                    value.AddToken(token);

                startPosition += nextSeparator + 1;
            }
            while (startPosition < pathSpan.Length);

            return true;

            static Token CreateToken(ReadOnlySpan<char> token,
                Range range)
                => new()
                {
                    Location = range,
                    EscapeSequenceLocation = token.IndexOf('~'),
                    //HasNoLeadingZeros =
                    //    token.SequenceEqual(Zero) ||
                    //    token.IndexOfAny(OneThroughNine) == 0
                };
        }
    }
}
