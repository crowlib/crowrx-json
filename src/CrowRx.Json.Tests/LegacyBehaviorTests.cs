using System.Collections.Generic;

namespace CrowRx.Json.Tests;

public sealed class LegacyBehaviorTests
{
    [Fact]
    public void PrimitiveValues_RoundTrip()
    {
        const int source = 42;

        string json = source.ToJson();
        bool success = json.TryConvertFromJson(out int result);

        Assert.Equal("42", json);
        Assert.True(success);
        Assert.Equal(source, result);
    }

    [Fact]
    public void FieldDto_RoundTripsPublicAndPrivateFields()
    {
        var source = new FieldDto(7, "secret");

        string json = source.ToJson();
        FieldDto result = json.ConvertFromJson<FieldDto>();

        Assert.Equal(7, result.Number);
        Assert.Equal("secret", result.Hidden);
    }

    [Fact]
    public void ListAndDictionary_RoundTrip()
    {
        var source = new Dictionary<string, List<int>>
        {
            ["values"] = new() { 1, 2, 3 },
        };

        Dictionary<string, List<int>> result = source.ToJson().ConvertFromJson<Dictionary<string, List<int>>>();

        Assert.Equal(source, result);
    }

    [Fact]
    public void ListOverwrite_ReplaceAndAppendRemainAvailable()
    {
        object replaceTarget = new List<int> { 9 };
        object appendTarget = new List<int> { 9 };

        bool replaceSuccess = "[1,2]".TryConvertFromJsonOverwrite(typeof(List<int>), ref replaceTarget, isAppend: false, customActivator: null!);
        bool appendSuccess = "[1,2]".TryConvertFromJsonOverwrite(typeof(List<int>), ref appendTarget, isAppend: true, customActivator: null!);

        Assert.True(replaceSuccess);
        Assert.Equal(new[] { 1, 2 }, Assert.IsType<List<int>>(replaceTarget));
        Assert.True(appendSuccess);
        Assert.Equal(new[] { 9, 1, 2 }, Assert.IsType<List<int>>(appendTarget));
    }

    private sealed class FieldDto
    {
        public int Number;
        [JsonInclude]
        private string _hidden = string.Empty;

        public FieldDto()
        {
        }

        public FieldDto(int number, string hidden)
        {
            Number = number;
            _hidden = hidden;
        }

        public string Hidden => _hidden;
    }
}
