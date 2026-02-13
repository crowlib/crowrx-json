using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
#if USING_ZLINQ
using ZLinq;
#else
using System.Linq;
#endif
using System.Reflection; //@@by ps2
using System.Text;
using CrowRx.Pool.Text;


namespace CrowRx.Json
{
    namespace MiniJSON
    {
        internal static class Json
        {
            public static CultureInfo CurrentCultureInfo { get; set; } = CultureInfo.InvariantCulture;


            /// <summary>
            /// Parses the string json into a value
            /// </summary>
            /// <param name="json">A JSON string.</param>
            /// <returns>A List&lt;object&gt;, a Dictionary&lt;string, object&gt;, a double, an integer,a string, null, true, or false</returns>
            public static object Deserialize(string json) => string.IsNullOrEmpty(json) ? null : Parser.Parse(json);

            /// <summary>
            /// Converts a IDictionary / IList object or a simple type (string, int, etc.) into a JSON string
            /// </summary>
            /// <param name="obj">A Dictionary&lt;string, object&gt; / List&lt;object&gt;</param>
            /// <returns>A JSON encoded string, or null if object 'json' is not serializable</returns>
            public static string Serialize(object obj) => Serializer.SerializeInternal(obj);


            private sealed class Parser : IDisposable
            {
                public static object Parse(string jsonString)
                {
                    using Parser instance = new(jsonString);

                    return instance.ParseValue();
                }


                private enum Token
                {
                    None,
                    CurlyOpen,
                    CurlyClose,
                    SquaredOpen,
                    SquaredClose,
                    Colon,
                    Comma,
                    String,
                    Number,
                    True,
                    False,
                    Null
                };


                private const string WHITE_SPACE = " \t\n\r";
                private const string WORD_BREAK = " \t\n\r{}[],:\"";


                private StringReader _stringReader;


                private char PeekChar => Convert.ToChar(_stringReader.Peek());
                private char NextChar => Convert.ToChar(_stringReader.Read());

                private string NextWord
                {
                    get
                    {
                        using IPooledStringBuilder nextWord = StringBuilderPool.Get();

                        while (WORD_BREAK.IndexOf(PeekChar) == -1)
                        {
                            nextWord.StringBuilder.Append(NextChar);

                            if (_stringReader.Peek() == -1)
                            {
                                break;
                            }
                        }

                        return nextWord.StringBuilder.ToString();
                    }
                }

                private Token NextToken
                {
                    get
                    {
                        EatWhitespace();

                        if (_stringReader.Peek() == -1)
                        {
                            return Token.None;
                        }

                        char peekedCharacter = PeekChar;

                        switch (peekedCharacter)
                        {
                            case '{':
                                return Token.CurlyOpen;

                            case '}':
                                _stringReader.Read();

                                return Token.CurlyClose;

                            case '[':
                                return Token.SquaredOpen;

                            case ']':
                                _stringReader.Read();
                                return Token.SquaredClose;

                            case ',':
                                _stringReader.Read();
                                return Token.Comma;

                            case '"':
                                return Token.String;

                            case ':':
                                return Token.Colon;

                            case '0':
                            case '1':
                            case '2':
                            case '3':
                            case '4':
                            case '5':
                            case '6':
                            case '7':
                            case '8':
                            case '9':
                            case '-':
                                return Token.Number;
                        }

                        return NextWord switch
                        {
                            "false" => Token.False,
                            "true" => Token.True,
                            "null" => Token.Null,
                            _ => Token.None
                        };
                    }
                }


                private Parser(string jsonString)
                {
                    _stringReader = new StringReader(jsonString);
                }


                public void Dispose()
                {
                    _stringReader.Dispose();
                    _stringReader = null;
                }

                private Dictionary<string, object> ParseObject()
                {
                    Dictionary<string, object> table = null;

                    // ditch opening brace
                    _stringReader.Read();

                    // {
                    while (true)
                    {
                        switch (NextToken)
                        {
                            case Token.None:
                                return null;

                            case Token.Comma:
                                continue;

                            case Token.CurlyClose:
                            {
                                table ??= new Dictionary<string, object>();
                                return table;
                            }

                            case Token.CurlyOpen:
                            case Token.SquaredOpen:
                            case Token.SquaredClose:
                            case Token.Colon:
                            case Token.String:
                            case Token.Number:
                            case Token.True:
                            case Token.False:
                            case Token.Null:
                            default:
                            {
                                // name
                                string name = ParseString();
                                if (string.IsNullOrEmpty(name))
                                {
                                    return null;
                                }

                                // :
                                if (NextToken != Token.Colon)
                                {
                                    return null;
                                }

                                // ditch the colon
                                _stringReader.Read();

                                // value
                                table ??= new Dictionary<string, object>();
                                table[name] = ParseValue();

                                break;
                            }
                        }
                    }
                }

                private List<object> ParseArray()
                {
                    List<object> array = null;

                    // ditch opening bracket
                    _stringReader.Read();

                    // [
                    bool isParsing = true;

                    while (isParsing)
                    {
                        Token nextToken = NextToken;

                        switch (nextToken)
                        {
                            case Token.None:
                                return null;

                            case Token.Comma:
                                continue;

                            case Token.SquaredClose:
                                isParsing = false;
                                break;

                            case Token.CurlyOpen:
                            case Token.CurlyClose:
                            case Token.SquaredOpen:
                            case Token.Colon:
                            case Token.String:
                            case Token.Number:
                            case Token.True:
                            case Token.False:
                            case Token.Null:
                            default:
                                object value = ParseByToken(nextToken);

                                array ??= new List<object>();
                                array.Add(value);

                                break;
                        }
                    }

                    array ??= new List<object>();
                    return array;
                }

                private object ParseValue() => ParseByToken(NextToken);

                private object ParseByToken(Token token) =>
                    token switch
                    {
                        Token.String => ParseString(),
                        Token.Number => ParseNumber(),
                        Token.CurlyOpen => ParseObject(),
                        Token.SquaredOpen => ParseArray(),
                        Token.True => true,
                        Token.False => false,
                        _ => null,
                    };

                private string ParseString()
                {
                    using IPooledStringBuilder parsed = StringBuilderPool.Get();

                    // ditch opening quote
                    _stringReader.Read();

                    bool isParsing = true;

                    while (isParsing)
                    {
                        if (_stringReader.Peek() == -1)
                        {
                            break;
                        }

                        char character = NextChar;

                        switch (character)
                        {
                            case '"':
                                isParsing = false;
                                break;

                            case '\\':
                                if (_stringReader.Peek() == -1)
                                {
                                    isParsing = false;

                                    break;
                                }

                                character = NextChar;

                                switch (character)
                                {
                                    case '"':
                                    case '\\':
                                    case '/':
                                        parsed.StringBuilder.Append(character);
                                        break;

                                    case 'b':
                                        parsed.StringBuilder.Append('\b');
                                        break;

                                    case 'f':
                                        parsed.StringBuilder.Append('\f');
                                        break;

                                    case 'n':
                                        parsed.StringBuilder.Append('\n');
                                        break;

                                    case 'r':
                                        parsed.StringBuilder.Append('\r');
                                        break;

                                    case 't':
                                        parsed.StringBuilder.Append('\t');
                                        break;

                                    case 'u':
                                    {
                                        using IPooledStringBuilder hex = StringBuilderPool.Get();

                                        for (int i = 0; i < 4; i++)
                                        {
                                            hex.StringBuilder.Append(NextChar);
                                        }

                                        parsed.StringBuilder.Append((char)Convert.ToInt32(hex.StringBuilder.ToString(), 16));

                                        break;
                                    }
                                }

                                break;

                            default:
                                parsed.StringBuilder.Append(character);

                                break;
                        }
                    }

                    return parsed.StringBuilder.ToString();
                }

                private object ParseNumber()
                {
                    string number = NextWord;

                    if (ulong.TryParse(number, NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out ulong parsedUlong))
                    {
                        return parsedUlong;
                    }

                    if (long.TryParse(number, NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out long parsedInt))
                    {
                        return parsedInt;
                    }

                    double.TryParse(number, NumberStyles.Number, CultureInfo.InvariantCulture, out double parsedDouble);

                    return parsedDouble;
                }

                private void EatWhitespace()
                {
                    if (_stringReader.Peek() == -1)
                    {
                        return;
                    }

                    while (WHITE_SPACE.IndexOf(PeekChar) != -1)
                    {
                        _stringReader.Read();

                        if (_stringReader.Peek() == -1)
                        {
                            break;
                        }
                    }
                }
            }

            private sealed class Serializer
            {
                public static string SerializeInternal(object obj)
                {
                    using IPooledStringBuilder pooledStringBuilder = StringBuilderPool.Get();

                    var instance = new Serializer(pooledStringBuilder.StringBuilder);

                    instance.SerializeValue(obj);

                    return instance._stringBuilder.ToString();
                }


                private readonly StringBuilder _stringBuilder;


                private Serializer(StringBuilder stringBuilder)
                {
                    _stringBuilder = stringBuilder;
                }


                private void SerializeValue(object value)
                {
                    switch (value)
                    {
                        case null:
                            _stringBuilder.Append("null");
                            break;

                        case string asStr:
                            SerializeString(asStr);
                            break;

                        case bool:
                            _stringBuilder.Append(value.ToString().ToLower());
                            break;

                        case IList asList:
                            SerializeArray(asList);
                            break;

                        case IDictionary asDict:
                            SerializeObject(asDict);
                            break;

                        case char:
                            SerializeString(value.ToString());
                            break;

                        default:
                            SerializeOther(value);
                            break;
                    }
                }

                private void SerializeObject(IDictionary obj)
                {
                    bool isFirst = true;

                    _stringBuilder.Append('{');

                    foreach (object key in obj.Keys)
                    {
                        if (!isFirst)
                        {
                            _stringBuilder.Append(',');
                        }

                        SerializeString(key.ToString());

                        _stringBuilder.Append(':');

                        SerializeValue(obj[key]);

                        isFirst = false;
                    }

                    _stringBuilder.Append('}');
                }

                private void SerializeArray(IList anArray)
                {
                    _stringBuilder.Append('[');

                    bool isFirst = true;

                    foreach (object obj in anArray)
                    {
                        if (isFirst == false)
                        {
                            _stringBuilder.Append(',');
                        }

                        SerializeValue(obj);

                        isFirst = false;
                    }

                    _stringBuilder.Append(']');
                }

                private void SerializeString(string str)
                {
                    _stringBuilder.Append('\"');

                    char[] charArray = str.ToCharArray();

                    foreach (char character in charArray)
                    {
                        switch (character)
                        {
                            case '"':
                                _stringBuilder.Append("\\\"");

                                break;

                            case '\\':
                                _stringBuilder.Append("\\\\");

                                break;

                            case '\b':
                                _stringBuilder.Append("\\b");

                                break;

                            case '\f':
                                _stringBuilder.Append("\\f");

                                break;

                            case '\n':
                                _stringBuilder.Append("\\n");

                                break;

                            case '\r':
                                _stringBuilder.Append("\\r");

                                break;

                            case '\t':
                                _stringBuilder.Append("\\t");

                                break;

                            default:
                            {
                                int codepoint = Convert.ToInt32(character);

                                if (codepoint is >= 32 and <= 126)
                                {
                                    _stringBuilder.Append(character);
                                }
                                else
                                {
                                    _stringBuilder.Append("\\u" + Convert.ToString(codepoint, 16).PadLeft(4, '0'));
                                }

                                break;
                            }
                        }
                    }

                    _stringBuilder.Append('\"');
                }

                private void SerializeOther(object value)
                {
                    if (value is float or int or uint or long or double or sbyte or byte or short or ushort or ulong or decimal)
                    {
                        _stringBuilder.Append(string.Format(CultureInfo.InvariantCulture, "{0}", value));
                    }
                    else
                    {
                        SerializeNonPrimitiveTypeObject(value); //@@by ps2
                    }
                }

                //@@by ps2 
                private void SerializeNonPrimitiveTypeObject(object obj)
                {
                    FieldInfo[] fieldInfos = obj.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                    if (fieldInfos.Length > 0)
                    {
                        Dictionary<string, object> dic =
                            fieldInfos
#if USING_ZLINQ
                                .AsValueEnumerable()
#endif
                                .ToDictionary(fieldInfo => fieldInfo.Name, fieldInfo => fieldInfo.GetValue(obj));

                        SerializeObject(dic);
                    }
                }
            }
        }
    }
}