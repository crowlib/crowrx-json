using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CrowRx.Json
{
    internal sealed class JsonReader
    {
        internal const int DefaultMaximumDepth = 64;

        private readonly string _json;
        private readonly int _maximumDepth;
        private readonly bool _preserveNumbers;
        private int _position;
        private JsonError _error = JsonError.NoError;

        private JsonReader(string json, int maximumDepth, bool preserveNumbers)
        {
            _json = json;
            _maximumDepth = maximumDepth;
            _preserveNumbers = preserveNumbers;
        }

        internal static bool TryRead(
            string json,
            out object? value,
            out JsonError error,
            int maximumDepth = DefaultMaximumDepth,
            bool preserveNumbers = false)
        {
            if (json is null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            if (maximumDepth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumDepth));
            }

            var reader = new JsonReader(json, maximumDepth, preserveNumbers);
            bool success = reader.TryReadDocument(out value);
            error = success ? JsonError.NoError : reader._error;
            return success;
        }

        private bool TryReadDocument(out object? value)
        {
            SkipWhitespace();

            if (IsAtEnd)
            {
                value = null;
                return Fail(JsonErrorCategory.UnexpectedEnd, "Expected a JSON value.", "$", _position);
            }

            if (!TryReadValue("$", 0, out value))
            {
                return false;
            }

            SkipWhitespace();
            if (!IsAtEnd)
            {
                return Fail(JsonErrorCategory.TrailingContent, "Unexpected content after the root JSON value.", "$", _position);
            }

            return true;
        }

        private bool TryReadValue(string path, int depth, out object? value)
        {
            SkipWhitespace();
            if (IsAtEnd)
            {
                value = null;
                return Fail(JsonErrorCategory.UnexpectedEnd, "Expected a JSON value.", path, _position);
            }

            switch (Current)
            {
                case '{':
                    return TryReadObject(path, depth, out value);
                case '[':
                    return TryReadArray(path, depth, out value);
                case '"':
                    return TryReadString(path, out value);
                case 't':
                    return TryReadLiteral("true", true, path, out value);
                case 'f':
                    return TryReadLiteral("false", false, path, out value);
                case 'n':
                    return TryReadLiteral("null", null, path, out value);
                default:
                    if (Current == '-' || IsDigit(Current))
                    {
                        return TryReadNumber(path, out value);
                    }

                    value = null;
                    return Fail(JsonErrorCategory.UnexpectedToken, $"Unexpected character '{Current}'.", path, _position);
            }
        }

        private bool TryReadObject(string path, int depth, out object? value)
        {
            value = null;
            if (depth >= _maximumDepth)
            {
                return Fail(JsonErrorCategory.MaximumDepthExceeded, $"Maximum depth {_maximumDepth} was exceeded.", path, _position);
            }

            _position++;
            SkipWhitespace();

            var result = new Dictionary<string, object?>();
            if (TryConsume('}'))
            {
                value = result;
                return true;
            }

            while (true)
            {
                SkipWhitespace();
                if (IsAtEnd)
                {
                    return Fail(JsonErrorCategory.UnexpectedEnd, "Expected an object property name.", path, _position);
                }

                if (Current != '"')
                {
                    return Fail(JsonErrorCategory.ExpectedPropertyName, "Object property names must be JSON strings.", path, _position);
                }

                if (!TryReadString(path, out object? propertyValue))
                {
                    return false;
                }

                string propertyName = (string)propertyValue!;
                string propertyPath = AppendPropertyPath(path, propertyName);
                if (result.ContainsKey(propertyName))
                {
                    return Fail(JsonErrorCategory.DuplicatePropertyName, $"Duplicate property name '{propertyName}'.", propertyPath, _position);
                }

                SkipWhitespace();
                if (!TryConsume(':'))
                {
                    return Fail(
                        IsAtEnd ? JsonErrorCategory.UnexpectedEnd : JsonErrorCategory.ExpectedColon,
                        "Expected ':' after the object property name.",
                        propertyPath,
                        _position);
                }

                if (!TryReadValue(propertyPath, depth + 1, out object? memberValue))
                {
                    return false;
                }

                result.Add(propertyName, memberValue);
                SkipWhitespace();

                if (TryConsume('}'))
                {
                    value = result;
                    return true;
                }

                if (!TryConsume(','))
                {
                    return Fail(
                        IsAtEnd ? JsonErrorCategory.UnexpectedEnd : JsonErrorCategory.ExpectedCommaOrEnd,
                        "Expected ',' or '}' after the object property value.",
                        path,
                        _position);
                }
            }
        }

        private bool TryReadArray(string path, int depth, out object? value)
        {
            value = null;
            if (depth >= _maximumDepth)
            {
                return Fail(JsonErrorCategory.MaximumDepthExceeded, $"Maximum depth {_maximumDepth} was exceeded.", path, _position);
            }

            _position++;
            SkipWhitespace();

            var result = new List<object?>();
            if (TryConsume(']'))
            {
                value = result;
                return true;
            }

            int index = 0;
            while (true)
            {
                string elementPath = $"{path}[{index}]";
                if (!TryReadValue(elementPath, depth + 1, out object? element))
                {
                    return false;
                }

                result.Add(element);
                index++;
                SkipWhitespace();

                if (TryConsume(']'))
                {
                    value = result;
                    return true;
                }

                if (!TryConsume(','))
                {
                    return Fail(
                        IsAtEnd ? JsonErrorCategory.UnexpectedEnd : JsonErrorCategory.ExpectedCommaOrEnd,
                        "Expected ',' or ']' after the array element.",
                        path,
                        _position);
                }
            }
        }

        private bool TryReadString(string path, out object? value)
        {
            int openingQuote = _position;
            _position++;
            var builder = new StringBuilder();

            while (!IsAtEnd)
            {
                char character = Current;
                _position++;

                if (character == '"')
                {
                    value = builder.ToString();
                    return true;
                }

                if (character == '\\')
                {
                    if (!TryReadEscape(path, builder))
                    {
                        value = null;
                        return false;
                    }

                    continue;
                }

                if (character < 0x20)
                {
                    value = null;
                    return Fail(JsonErrorCategory.UnescapedControlCharacter, "JSON strings cannot contain unescaped control characters.", path, _position - 1);
                }

                if (char.IsHighSurrogate(character))
                {
                    if (IsAtEnd || !char.IsLowSurrogate(Current))
                    {
                        value = null;
                        return Fail(JsonErrorCategory.InvalidSurrogatePair, "A high surrogate must be followed by a low surrogate.", path, _position - 1);
                    }

                    builder.Append(character);
                    builder.Append(Current);
                    _position++;
                    continue;
                }

                if (char.IsLowSurrogate(character))
                {
                    value = null;
                    return Fail(JsonErrorCategory.InvalidSurrogatePair, "A low surrogate must follow a high surrogate.", path, _position - 1);
                }

                builder.Append(character);
            }

            value = null;
            return Fail(JsonErrorCategory.UnexpectedEnd, "Unterminated JSON string.", path, openingQuote);
        }

        private bool TryReadEscape(string path, StringBuilder builder)
        {
            if (IsAtEnd)
            {
                return Fail(JsonErrorCategory.UnexpectedEnd, "Unterminated JSON escape sequence.", path, _position);
            }

            char escape = Current;
            _position++;
            switch (escape)
            {
                case '"': builder.Append('"'); return true;
                case '\\': builder.Append('\\'); return true;
                case '/': builder.Append('/'); return true;
                case 'b': builder.Append('\b'); return true;
                case 'f': builder.Append('\f'); return true;
                case 'n': builder.Append('\n'); return true;
                case 'r': builder.Append('\r'); return true;
                case 't': builder.Append('\t'); return true;
                case 'u': return TryReadUnicodeEscape(path, builder);
                default:
                    return Fail(JsonErrorCategory.InvalidEscape, $"Invalid JSON escape sequence '\\{escape}'.", path, _position - 1);
            }
        }

        private bool TryReadUnicodeEscape(string path, StringBuilder builder)
        {
            int escapePosition = _position - 2;
            if (!TryReadHexCodeUnit(path, out char codeUnit))
            {
                return false;
            }

            if (char.IsHighSurrogate(codeUnit))
            {
                if (_position + 2 > _json.Length || _json[_position] != '\\' || _json[_position + 1] != 'u')
                {
                    return Fail(JsonErrorCategory.InvalidSurrogatePair, "An escaped high surrogate must be followed by an escaped low surrogate.", path, escapePosition);
                }

                _position += 2;
                if (!TryReadHexCodeUnit(path, out char lowSurrogate))
                {
                    return false;
                }

                if (!char.IsLowSurrogate(lowSurrogate))
                {
                    return Fail(JsonErrorCategory.InvalidSurrogatePair, "An escaped high surrogate must be followed by an escaped low surrogate.", path, escapePosition);
                }

                builder.Append(codeUnit);
                builder.Append(lowSurrogate);
                return true;
            }

            if (char.IsLowSurrogate(codeUnit))
            {
                return Fail(JsonErrorCategory.InvalidSurrogatePair, "An escaped low surrogate must follow an escaped high surrogate.", path, escapePosition);
            }

            builder.Append(codeUnit);
            return true;
        }

        private bool TryReadHexCodeUnit(string path, out char codeUnit)
        {
            int start = _position;
            if (_position + 4 > _json.Length)
            {
                codeUnit = default;
                return Fail(JsonErrorCategory.UnexpectedEnd, "A Unicode escape must contain four hexadecimal digits.", path, start);
            }

            int value = 0;
            for (int index = 0; index < 4; index++)
            {
                int digit = HexValue(_json[_position + index]);
                if (digit < 0)
                {
                    codeUnit = default;
                    return Fail(JsonErrorCategory.InvalidUnicodeEscape, "A Unicode escape contains a non-hexadecimal character.", path, _position + index);
                }

                value = (value << 4) | digit;
            }

            _position += 4;
            codeUnit = (char)value;
            return true;
        }

        private bool TryReadNumber(string path, out object? value)
        {
            int start = _position;
            if (TryConsume('-') && IsAtEnd)
            {
                value = null;
                return Fail(JsonErrorCategory.InvalidNumber, "A minus sign must be followed by a digit.", path, start);
            }

            if (TryConsume('0'))
            {
                if (!IsAtEnd && IsDigit(Current))
                {
                    value = null;
                    return Fail(JsonErrorCategory.InvalidNumber, "Leading zeroes are not allowed in JSON numbers.", path, _position);
                }
            }
            else
            {
                if (IsAtEnd || Current < '1' || Current > '9')
                {
                    value = null;
                    return Fail(JsonErrorCategory.InvalidNumber, "Expected a digit in the JSON number.", path, _position);
                }

                ReadDigits();
            }

            bool hasFractionOrExponent = false;
            if (TryConsume('.'))
            {
                hasFractionOrExponent = true;
                if (IsAtEnd || !IsDigit(Current))
                {
                    value = null;
                    return Fail(JsonErrorCategory.InvalidNumber, "A decimal point must be followed by a digit.", path, _position);
                }

                ReadDigits();
            }

            if (!IsAtEnd && (Current == 'e' || Current == 'E'))
            {
                hasFractionOrExponent = true;
                _position++;
                if (!IsAtEnd && (Current == '+' || Current == '-'))
                {
                    _position++;
                }

                if (IsAtEnd || !IsDigit(Current))
                {
                    value = null;
                    return Fail(JsonErrorCategory.InvalidNumber, "An exponent must contain at least one digit.", path, _position);
                }

                ReadDigits();
            }

            string number = _json.Substring(start, _position - start);
            if (_preserveNumbers)
            {
                if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double preservedValue) &&
                    !double.IsNaN(preservedValue) &&
                    !double.IsInfinity(preservedValue))
                {
                    value = new JsonNumber(number, !hasFractionOrExponent);
                    return true;
                }

                value = null;
                return Fail(JsonErrorCategory.InvalidNumber, "The JSON number is outside the supported numeric range.", path, start);
            }

            if (!hasFractionOrExponent)
            {
                if (number[0] != '-' && ulong.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out ulong unsignedValue))
                {
                    value = unsignedValue;
                    return true;
                }

                if (long.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long signedValue))
                {
                    value = signedValue;
                    return true;
                }
            }

            if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue) &&
                !double.IsNaN(doubleValue) &&
                !double.IsInfinity(doubleValue))
            {
                value = doubleValue;
                return true;
            }

            value = null;
            return Fail(JsonErrorCategory.InvalidNumber, "The JSON number is outside the supported numeric range.", path, start);
        }

        private bool TryReadLiteral(string literal, object? literalValue, string path, out object? value)
        {
            if (_position + literal.Length > _json.Length ||
                string.CompareOrdinal(_json, _position, literal, 0, literal.Length) != 0)
            {
                value = null;
                return Fail(JsonErrorCategory.UnexpectedToken, $"Expected '{literal}'.", path, _position);
            }

            _position += literal.Length;
            value = literalValue;
            return true;
        }

        private void ReadDigits()
        {
            while (!IsAtEnd && IsDigit(Current))
            {
                _position++;
            }
        }

        private void SkipWhitespace()
        {
            while (!IsAtEnd && (Current == ' ' || Current == '\t' || Current == '\r' || Current == '\n'))
            {
                _position++;
            }
        }

        private bool TryConsume(char character)
        {
            if (IsAtEnd || Current != character)
            {
                return false;
            }

            _position++;
            return true;
        }

        private bool Fail(JsonErrorCategory category, string message, string path, int position)
        {
            (int line, int column) = GetLineAndColumn(position);
            _error = new JsonError(category, message, path, position, line, column);
            return false;
        }

        private (int line, int column) GetLineAndColumn(int position)
        {
            int line = 1;
            int column = 1;
            for (int index = 0; index < position && index < _json.Length; index++)
            {
                if (_json[index] == '\r')
                {
                    line++;
                    column = 1;
                    if (index + 1 < position && _json[index + 1] == '\n')
                    {
                        index++;
                    }
                }
                else if (_json[index] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }

            return (line, column);
        }

        private static string AppendPropertyPath(string path, string propertyName)
        {
            var builder = new StringBuilder(path.Length + propertyName.Length + 4);
            builder.Append(path);
            builder.Append("[\"");

            foreach (char character in propertyName)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 0x20)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }
                        break;
                }
            }

            builder.Append("\"]");
            return builder.ToString();
        }

        private static bool IsDigit(char character) => character >= '0' && character <= '9';

        private static int HexValue(char character)
        {
            if (character >= '0' && character <= '9') return character - '0';
            if (character >= 'a' && character <= 'f') return character - 'a' + 10;
            if (character >= 'A' && character <= 'F') return character - 'A' + 10;
            return -1;
        }

        private bool IsAtEnd => _position >= _json.Length;
        private char Current => _json[_position];
    }
}
