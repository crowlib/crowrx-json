using System;

namespace CrowRx.Json
{
    public enum JsonErrorCategory
    {
        None,
        UnexpectedEnd,
        UnexpectedToken,
        TrailingContent,
        ExpectedPropertyName,
        ExpectedColon,
        ExpectedCommaOrEnd,
        InvalidEscape,
        InvalidUnicodeEscape,
        InvalidSurrogatePair,
        UnescapedControlCharacter,
        InvalidNumber,
        DuplicatePropertyName,
        MaximumDepthExceeded,
        TypeMismatch,
        NumericOverflow,
        InvalidValue,
        UnsupportedType,
        ObjectCreationFailed,
        MemberAssignmentFailed,
        UnknownMember,
        MissingRequiredMember,
        AmbiguousConstructor,
        ConstructorBindingFailed,
        ConstructorInvocationFailed,
        UnsupportedOverwriteTarget,
        DuplicateDictionaryKey,
        MutationFailed,
        ConverterFailed,
    }

    public sealed class JsonError
    {
        internal static readonly JsonError NoError = new(
            JsonErrorCategory.None,
            string.Empty,
            "$",
            0,
            1,
            1);

        public JsonErrorCategory Category { get; }
        public string Message { get; }
        public string Path { get; }
        public int Position { get; }
        public int Line { get; }
        public int Column { get; }
        public Type? ExpectedType { get; }
        public string? ActualJsonType { get; }

        internal JsonError(
            JsonErrorCategory category,
            string message,
            string path,
            int position,
            int line,
            int column,
            Type? expectedType = null,
            string? actualJsonType = null)
        {
            Category = category;
            Message = message;
            Path = path;
            Position = position;
            Line = line;
            Column = column;
            ExpectedType = expectedType;
            ActualJsonType = actualJsonType;
        }

        public override string ToString() =>
            $"{Category} at {Path} (line {Line}, column {Column}): {Message}";
    }
}
