namespace CrowRx.Json.Tests;

public sealed class OverwriteTests
{
    [Fact]
    public void DtoOverwrite_UpdatesOnlyPresentMembers()
    {
        var target = new MutableDto { A = 1, B = 2 };

        bool success = JsonSerializer.TryOverwrite("""{"A":9,"Unknown":3}""", target, out JsonError error);

        Assert.True(success);
        Assert.Equal(JsonErrorCategory.None, error.Category);
        Assert.Equal(9, target.A);
        Assert.Equal(2, target.B);
    }

    [Fact]
    public void NestedDtoOverwrite_PreservesIdentityAndMissingMembers()
    {
        var child = new ChildDto { Number = 1, Other = 2 };
        var target = new ParentDto { Name = "keep", Child = child };

        bool success = JsonSerializer.TryOverwrite("""{"Child":{"Number":7}}""", target, out JsonError error);

        Assert.True(success);
        Assert.Equal(JsonErrorCategory.None, error.Category);
        Assert.Same(child, target.Child);
        Assert.Equal(7, target.Child.Number);
        Assert.Equal(2, target.Child.Other);
        Assert.Equal("keep", target.Name);
    }

    [Fact]
    public void ListReplace_ClearsAndRepopulatesExistingInstance()
    {
        var target = new List<int> { 9, 8 };

        bool success = JsonSerializer.TryOverwrite("[1,2]", target, out JsonError error);

        Assert.True(success);
        Assert.Equal(JsonErrorCategory.None, error.Category);
        Assert.Equal(new[] { 1, 2 }, target);
    }

    [Fact]
    public void ListAppend_AddsToExistingInstance()
    {
        var target = new List<int> { 9 };

        bool success = JsonSerializer.TryOverwrite("[1,2]", target, out JsonError error, JsonOverwriteMode.Append);

        Assert.True(success);
        Assert.Equal(JsonErrorCategory.None, error.Category);
        Assert.Equal(new[] { 9, 1, 2 }, target);
    }

    [Fact]
    public void DictionaryReplace_ClearsAndRepopulatesExistingInstance()
    {
        var target = new Dictionary<string, int> { ["old"] = 9 };

        bool success = JsonSerializer.TryOverwrite("""{"new":1}""", target, out JsonError error);

        Assert.True(success);
        Assert.Equal(JsonErrorCategory.None, error.Category);
        Assert.Equal(new Dictionary<string, int> { ["new"] = 1 }, target);
    }

    [Fact]
    public void DictionaryAppend_AddsNewKeys()
    {
        var target = new Dictionary<string, int> { ["old"] = 9 };

        bool success = JsonSerializer.TryOverwrite(
            """{"new":1}""",
            target,
            out JsonError error,
            JsonOverwriteMode.Append);

        Assert.True(success);
        Assert.Equal(JsonErrorCategory.None, error.Category);
        Assert.Equal(9, target["old"]);
        Assert.Equal(1, target["new"]);
    }

    [Fact]
    public void DictionaryAppend_DuplicateKeyFailsWithoutMutation()
    {
        var target = new Dictionary<string, int> { ["existing"] = 9 };

        bool success = JsonSerializer.TryOverwrite(
            """{"existing":1,"new":2}""",
            target,
            out JsonError error,
            JsonOverwriteMode.Append);

        Assert.False(success);
        Assert.Equal(JsonErrorCategory.DuplicateDictionaryKey, error.Category);
        Assert.Equal("$.existing", error.Path);
        Assert.Equal(new Dictionary<string, int> { ["existing"] = 9 }, target);
    }

    [Fact]
    public void ConversionFailure_HappensBeforeAnyMutation()
    {
        var target = new MutableDto { A = 1, B = 2 };

        bool success = JsonSerializer.TryOverwrite("""{"A":5,"B":"bad"}""", target, out JsonError error);

        Assert.False(success);
        Assert.Equal(JsonErrorCategory.TypeMismatch, error.Category);
        Assert.Equal("$.B", error.Path);
        Assert.Equal(1, target.A);
        Assert.Equal(2, target.B);
    }

    [Fact]
    public void MutationFailure_CanLeaveEarlierMutationsApplied()
    {
        var target = new ThrowingSetterDto();

        bool success = JsonSerializer.TryOverwrite("""{"A":5,"B":2}""", target, out JsonError error);

        Assert.False(success);
        Assert.Equal(JsonErrorCategory.MutationFailed, error.Category);
        Assert.Equal("$.B", error.Path);
        Assert.Equal(5, target.A);
        Assert.Equal(0, target.B);
    }

    [Fact]
    public void RootArrayReplace_RequiresSameLength()
    {
        int[] target = { 9, 8 };

        Assert.True(JsonSerializer.TryOverwrite("[1,2]", target, out JsonError successError));
        Assert.Equal(JsonErrorCategory.None, successError.Category);
        Assert.Equal(new[] { 1, 2 }, target);

        Assert.False(JsonSerializer.TryOverwrite("[3]", target, out JsonError failureError));
        Assert.Equal(JsonErrorCategory.UnsupportedOverwriteTarget, failureError.Category);
        Assert.Equal(new[] { 1, 2 }, target);
    }

    [Fact]
    public void AppendRejectsArraysAndDtos()
    {
        int[] array = { 1 };
        var dto = new MutableDto { A = 1 };

        Assert.False(JsonSerializer.TryOverwrite("[2]", array, out JsonError arrayError, JsonOverwriteMode.Append));
        Assert.Equal(JsonErrorCategory.UnsupportedOverwriteTarget, arrayError.Category);
        Assert.False(JsonSerializer.TryOverwrite("""{"A":2}""", dto, out JsonError dtoError, JsonOverwriteMode.Append));
        Assert.Equal(JsonErrorCategory.UnsupportedOverwriteTarget, dtoError.Category);
        Assert.Equal(1, dto.A);
    }

    [Fact]
    public void ConstructorBoundMember_CannotBeOverwritten()
    {
        var target = new ImmutableDto(1);

        bool success = JsonSerializer.TryOverwrite("""{"Value":2}""", target, out JsonError error);

        Assert.False(success);
        Assert.Equal(JsonErrorCategory.UnsupportedOverwriteTarget, error.Category);
        Assert.Equal("$.Value", error.Path);
        Assert.Equal(1, target.Value);
    }

    [Fact]
    public void JsonObject_IsNotConvertedToListValuesDuringOverwrite()
    {
        var target = new List<int> { 9 };

        bool success = JsonSerializer.TryOverwrite("""{"first":1}""", target, out JsonError error);

        Assert.False(success);
        Assert.Equal(JsonErrorCategory.TypeMismatch, error.Category);
        Assert.Equal(new[] { 9 }, target);
    }

    [Fact]
    public void LegacyGenericOverwrite_UsesExistingTargetAndPreservesMissingMembers()
    {
        var target = new MutableDto { A = 9, B = 10 };

        bool success = """{"A":1}""".TryConvertFromJsonOverwrite(ref target);

        Assert.True(success);
        Assert.Equal(1, target.A);
        Assert.Equal(10, target.B);
    }

    private sealed class MutableDto
    {
        public int A { get; set; }
        public int B { get; set; }
    }

    private sealed class ChildDto
    {
        public int Number { get; set; }
        public int Other { get; set; }
    }

    private sealed class ParentDto
    {
        public string Name { get; set; } = string.Empty;
        public ChildDto Child { get; set; } = new();
    }

    private sealed class ThrowingSetterDto
    {
        private int _b;

        public int A { get; set; }
        public int B
        {
            get => _b;
            set
            {
                if (value == 2)
                {
                    throw new InvalidOperationException("B rejects 2.");
                }

                _b = value;
            }
        }
    }

    private sealed class ImmutableDto
    {
        public int Value { get; }

        public ImmutableDto(int Value)
        {
            this.Value = Value;
        }
    }
}
