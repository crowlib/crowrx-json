using System.Collections.Generic;

namespace CrowRx.Json.Tests;

public sealed class KnownDefectBaselineTests
{
    [Fact]
    public void Parser_RejectsTrailingGarbage()
    {
        bool success = "1 trailing".TryConvertFromJson(out int result);

        Assert.False(success);
        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData("[1 2]")]
    [InlineData("[1,,2]")]
    public void Parser_RejectsMalformedArraySeparators(string json)
    {
        bool success = json.TryConvertFromJson(out List<int> result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void Parser_RejectsInvalidNumber()
    {
        bool success = "1e".TryConvertFromJson(out int result);

        Assert.False(success);
        Assert.Equal(0, result);
    }

    [Fact]
    public void Parser_AcceptsEmptyObjectKey()
    {
        bool success = "{\"\":1}".TryConvertFromJson(out Dictionary<string, int> result);

        Assert.True(success);
        Assert.Equal(1, result[string.Empty]);
    }

    [Fact]
    public void Parser_RejectsDuplicateObjectKey()
    {
        bool success = "{\"value\":1,\"value\":2}".TryConvertFromJson(out Dictionary<string, int> result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void RootNull_SucceedsForNullableReference()
    {
        bool success = "null".TryConvertFromJson(out string result);

        Assert.True(success);
        Assert.Null(result);
    }

    [Fact]
    public void DtoMemberConversionFailure_FailsTheWholeOperation()
    {
        bool success = "{\"Number\":\"not-an-int\"}".TryConvertFromJson(out NumberDto result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void ListElementConversionFailure_FailsTheWholeOperation()
    {
        bool success = "[\"not-an-int\",2]".TryConvertFromJson(out List<int> result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonObject_IsNotImplicitlyConvertedToListValues()
    {
        bool success = "{\"first\":1,\"second\":2}".TryConvertFromJson(out List<int> result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void GenericOverwrite_UsesExistingTarget()
    {
        var target = new NumberDto { Number = 99, Other = 100 };

        bool success = "{\"Number\":1}".TryConvertFromJsonOverwrite(ref target);

        Assert.True(success);
        Assert.Equal(1, target.Number);
        Assert.Equal(100, target.Other);
    }

    public sealed class NumberDto
    {
        public int Number = 42;
        public int Other = 7;
    }
}
