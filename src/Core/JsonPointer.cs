using System;
using System.Collections.Generic;

namespace Finite.AspNetCore.JsonPatch
{
    /// <summary>
    /// Defines a class which represents a JSON Pointer, as required by JSON
    /// Patch.
    /// </summary>
    public sealed partial class JsonPointer
    {
        /// <summary>
        /// Declares a delegate which can be used during evaluation of a JSON
        /// Pointer.
        /// </summary>
        /// <param name="span">
        /// The <see cref="ReadOnlySpan{T}"/> representing the current token.
        /// </param>
        /// <param name="depth">
        /// How deep within the tree this token lies.
        /// </param>
        /// <returns>
        /// <c>true</c> to indicate that the next token should be evaluated,
        /// and <c>false</c> to end evaluation.
        /// </returns>
        public delegate bool EvaluateAction(
            ReadOnlySpan<char> span, int depth);

        /// <summary>
        /// Declares a delegate which can be used during evaluation of a JSON
        /// Pointer, with an accompanying state parameter.
        /// </summary>
        /// <typeparam name="TArg">
        /// The type of <paramref name="state"/> to use while evaluating the
        /// JSON Pointer.
        /// </typeparam>
        /// <param name="span">
        /// The <see cref="ReadOnlySpan{T}"/> representing the current token.
        /// </param>
        /// <param name="depth">
        /// How deep within the tree this token lies.
        /// </param>
        /// <param name="state">
        /// The state to use while evaluating the JSON Pointer.
        /// </param>
        /// <returns>
        /// <c>true</c> to indicate that the next token should be evaluated,
        /// and <c>false</c> to end evaluation.
        /// </returns>
        public delegate bool EvaluateAction<TArg>(
            ReadOnlySpan<char> span, int depth, TArg state);

         /// <summary>
        /// Declares a delegate which can be used during evaluation of a JSON
        /// Pointer, with an accompanying state parameter, which is passed by
        /// reference.
        /// </summary>
        /// <typeparam name="TArg">
        /// The type of <paramref name="state"/> to use while evaluating the
        /// JSON Pointer.
        /// </typeparam>
        /// <param name="span">
        /// The <see cref="ReadOnlySpan{T}"/> representing the current token.
        /// </param>
        /// <param name="depth">
        /// How deep within the tree this token lies.
        /// </param>
        /// <param name="state">
        /// The state to use while evaluating the JSON Pointer.
        /// </param>
        /// <returns>
        /// <c>true</c> to indicate that the next token should be evaluated,
        /// and <c>false</c> to end evaluation.
        /// </returns>
        public delegate bool EvaluateRefAction<TArg>(
            ReadOnlySpan<char> span, int depth, ref TArg state);

        private readonly string _original;
        private char[] _temporaryDecodeBuffer;
        private readonly List<Token> _tokens;

        /// <summary>
        /// Gets how many tokens this JSON Pointer contains, or how "deep" into
        /// a document it represents.
        /// </summary>
        public int Depth => _tokens.Count;

        /// <summary>
        /// Gets the raw string used to create this JSON Pointer.
        /// </summary>
        public string Value => _original;

        private JsonPointer(string original)
        {
            _original = original;
            _temporaryDecodeBuffer = Array.Empty<char>();
            _tokens = new List<Token>();
        }

        private JsonPointer(string original, int estimatedTokenCount,
            Token token)
        {
            _original = original;
            _temporaryDecodeBuffer = Array.Empty<char>();
            _tokens = new List<Token>(estimatedTokenCount)
            {
                token
            };
        }

        private void AddToken(Token token)
            => _tokens.Add(token);

        /// <summary>
        /// Evaluates the JSON Pointer.
        /// </summary>
        /// <param name="action">
        /// The <see cref="EvaluateAction"/> to use during evaluation.
        /// </param>
        /// <returns>
        /// <c>true</c> if evaluation completed successfully, or <c>false</c>
        /// if <paramref name="action"/> returned <c>false</c> during
        /// evaluation.
        /// </returns>
        public bool Evaluate(EvaluateAction action)
        {
            return Evaluate(Helper, ref action);

            static bool Helper(ReadOnlySpan<char> span, int depth,
                ref EvaluateAction callback)
            {
                return callback(span, depth);
            }
        }

        /// <summary>
        /// Evaluates the JSON Pointer.
        /// </summary>
        /// <param name="action">
        /// The <see cref="EvaluateAction"/> to use during evaluation.
        /// </param>
        /// <param name="state">
        /// The state to use during evaluation.
        /// </param>
        /// <returns>
        /// <c>true</c> if evaluation completed successfully, or <c>false</c>
        /// if <paramref name="action"/> returned <c>false</c> during
        /// evaluation.
        /// </returns>
        public bool Evaluate<TState>(EvaluateAction<TState> action,
            TState state)
        {
            var stateWrapper = (action, state);
            return Evaluate(Helper, ref stateWrapper);

            static bool Helper(ReadOnlySpan<char> span, int depth,
                ref (EvaluateAction<TState>, TState) stateWrapper)
            {
                var (callback, state) = stateWrapper;
                return callback(span, depth, state);
            }
        }

        /// <summary>
        /// Evaluates the JSON Pointer.
        /// </summary>
        /// <param name="action">
        /// The <see cref="EvaluateAction"/> to use during evaluation.
        /// </param>
        /// <param name="state">
        /// The state to use during evaluation, except passed by reference, to
        /// allow mutability for struct types.
        /// </param>
        /// <returns>
        /// <c>true</c> if evaluation completed successfully, or <c>false</c>
        /// if <paramref name="action"/> returned <c>false</c> during
        /// evaluation.
        /// </returns>
        public bool Evaluate<TState>(EvaluateRefAction<TState> action,
            ref TState state)
        {
            var span = _original.AsSpan();

            int index = 0;
            foreach (var token in _tokens)
            {
                if (token.EscapeSequenceLocation > 0)
                {
                    var text = span[token.Location];

                    if (text.Length > _temporaryDecodeBuffer.Length)
                    {
                        _temporaryDecodeBuffer = new char[text.Length];
                    }

                    text.CopyTo(_temporaryDecodeBuffer);

                    var slice = _temporaryDecodeBuffer.AsSpan()
                        [token.EscapeSequenceLocation..text.Length];

                    var newLength = DecodeEscapeSequences(slice);
                    slice = slice.Slice(0, newLength);

                    if (!action(slice, index, ref state))
                        return false;
                }
                else
                {
                    if (!action(span[token.Location], index, ref state))
                        return false;
                }

                index++;
            }

            return true;
        }

        private static int DecodeEscapeSequences(Span<char> span)
        {
            var length = ReplaceAll(span, "~1", '/');
            return ReplaceAll(span.Slice(0, length), "~0", '~');

            static int ReplaceAll(Span<char> span,
                ReadOnlySpan<char> pattern, char replacement)
            {
                var length = span.Length;
                var index = 0;

                while (index < length)
                {
                    index = span.IndexOf(pattern);

                    if (index < 0)
                        return length;

                    span[(index + 1)..].CopyTo(span[index..]);
                    span[index] = replacement;
                    length -= 1;

                    span = span[(index + 1)..];
                }

                return length;
            }
        }

        private struct Token
        {
            // Location of token in original string
            public Range Location;
            // >0 if the token contains a '~' and needs to be decoded.
            public int EscapeSequenceLocation;
            //// True if the token contains no leading zeros
            //public bool HasNoLeadingZeros;
        }
    }
}
