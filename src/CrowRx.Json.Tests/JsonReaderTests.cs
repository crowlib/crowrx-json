using System.Collections.Generic;

namespace CrowRx.Json.Tests;

public sealed class JsonReaderTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("\"value\"")]
    [InlineData("-12.5e+2")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("null")]
    [InlineData("0")]
    [InlineData("-0")]
    [InlineData("1.25")]
    [InlineData("1e10")]
    [InlineData("1E-2")]
    public void TryRead_AcceptsEveryJsonRootKind(string json)
    {
        bool success = JsonReader.TryRead(json, out _, out JsonError error);

        Assert.True(success);
        Assert.Equal(JsonErrorCategory.None, error.Category);
    }

    [Theory]
    [InlineData("", JsonErrorCategory.UnexpectedEnd)]
    [InlineData("{", JsonErrorCategory.UnexpectedEnd)]
    [InlineData("[", JsonErrorCategory.UnexpectedEnd)]
    [InlineData("\"unterminated", JsonErrorCategory.UnexpectedEnd)]
    [InlineData("1 trailing", JsonErrorCategory.TrailingContent)]
    [InlineData("[1 2]", JsonErrorCategory.ExpectedCommaOrEnd)]
    [InlineData("[1,,2]", JsonErrorCategory.UnexpectedToken)]
    [InlineData("[1,]", JsonErrorCategory.UnexpectedToken)]
    [InlineData("{\"value\":1,}", JsonErrorCategory.ExpectedPropertyName)]
    [InlineData("{value:1}", JsonErrorCategory.ExpectedPropertyName)]
    [InlineData("{\"value\" 1}", JsonErrorCategory.ExpectedColon)]
    [InlineData("01", JsonErrorCategory.InvalidNumber)]
    [InlineData("1.", JsonErrorCategory.InvalidNumber)]
    [InlineData("1e", JsonErrorCategory.InvalidNumber)]
    [InlineData("--1", JsonErrorCategory.InvalidNumber)]
    [InlineData("+1", JsonErrorCategory.UnexpectedToken)]
    [InlineData(".1", JsonErrorCategory.UnexpectedToken)]
    [InlineData("NaN", JsonErrorCategory.UnexpectedToken)]
    [InlineData("Infinity", JsonErrorCategory.UnexpectedToken)]
    public void TryRead_RejectsMalformedJson(string json, JsonErrorCategory expectedCategory)
    {
        bool success = JsonReader.TryRead(json, out _, out JsonError error);

        Assert.False(success);
        Assert.Equal(expectedCategory, error.Category);
    }

    [Fact]
    public void TryRead_AllowsTrailingWhitespace()
    {
        bool success = JsonReader.TryRead("[1]\r\n\t ", out object? value, out JsonError error);

        Assert.True(success);
        Assert.IsType<List<object?>>(value);
        Assert.Equal(JsonErrorCategory.None, error.Category);
    }

    [Fact]
    public void TryRead_AcceptsEmptyPropertyName()
    {
        bool success = JsonReader.TryRead("{\"\":1}", out object? value, out _);

        Assert.True(success);
        Assert.True(Assert.IsType<Dictionary<string, object?>>(value).ContainsKey(string.Empty));
    }

    [Fact]
    public void TryRead_RejectsDuplicatePropertyNameWithPath()
    {
        bool success = JsonReader.TryRead("{\"value\":1,\"value\":2}", out _, out JsonError error);

        Assert.False(success);
        Assert.Equal(JsonErrorCategory.DuplicatePropertyName, error.Category);
        Assert.Equal("$[\"value\"]", error.Path);
    }

    [Theory]
    [InlineData("\"\\x\"", JsonErrorCategory.InvalidEscape)]
    [InlineData("\"\\u12X4\"", JsonErrorCategory.InvalidUnicodeEscape)]
    [InlineData("\"\\uD800\"", JsonErrorCategory.InvalidSurrogatePair)]
    [InlineData("\"\\uDC00\"", JsonErrorCategory.InvalidSurrogatePair)]
    [InlineData("\"line\nbreak\"", JsonErrorCategory.UnescapedControlCharacter)]
    public void TryRead_RejectsInvalidStrings(string json, JsonErrorCategory expectedCategory)
    {
        bool success = JsonReader.TryRead(json, out _, out JsonError error);

        Assert.False(success);
        Assert.Equal(expectedCategory, error.Category);
    }

    [Fact]
    public void TryRead_DecodesEscapedSurrogatePair()
    {
        bool success = JsonReader.TryRead("\"\\uD83D\\uDE00\"", out object? value, out _);

        Assert.True(success);
        Assert.Equal("😀", value);
    }

    [Fact]
    public void TryRead_ReportsNestedPathAndSourceLocation()
    {
        const string json = "{\n  \"items\": [1, nope]\n}";

        bool success = JsonReader.TryRead(json, out _, out JsonError error);

        Assert.False(success);
        Assert.Equal(JsonErrorCategory.UnexpectedToken, error.Category);
        Assert.Equal("$[\"items\"][1]", error.Path);
        Assert.Equal(2, error.Line);
        Assert.Equal(16, error.Column);
        Assert.Equal(json.IndexOf("nope", StringComparison.Ordinal), error.Position);
    }

    [Fact]
    public void TryRead_EnforcesMaximumDepth()
    {
        bool success = JsonReader.TryRead("[[0]]", out _, out JsonError error, maximumDepth: 1);

        Assert.False(success);
        Assert.Equal(JsonErrorCategory.MaximumDepthExceeded, error.Category);
        Assert.Equal("$[0]", error.Path);
    }
}
