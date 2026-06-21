using System.Collections.Concurrent;
using System.Reflection;

namespace CrowRx.Json.Tests;

public sealed class TypeContractTests
{
    public static IEnumerable<object[]> ContractKinds()
    {
        yield return new object[] { typeof(int), (int)TypeContractKind.Scalar };
        yield return new object[] { typeof(string), (int)TypeContractKind.Scalar };
        yield return new object[] { typeof(Guid), (int)TypeContractKind.Scalar };
        yield return new object[] { typeof(SampleEnum), (int)TypeContractKind.Enum };
        yield return new object[] { typeof(int?), (int)TypeContractKind.Nullable };
        yield return new object[] { typeof(int[]), (int)TypeContractKind.Collection };
        yield return new object[] { typeof(List<int>), (int)TypeContractKind.Collection };
        yield return new object[] { typeof(IList<int>), (int)TypeContractKind.Collection };
        yield return new object[] { typeof(Dictionary<string, int>), (int)TypeContractKind.Dictionary };
        yield return new object[] { typeof(IDictionary<string, int>), (int)TypeContractKind.Dictionary };
        yield return new object[] { typeof(SimpleDto), (int)TypeContractKind.Dto };
    }

    [Theory]
    [MemberData(nameof(ContractKinds))]
    public void Build_ClassifiesSupportedTypes(Type type, int expectedKind)
    {
        TypeContract contract = TypeContractBuilder.Build(type);

        Assert.Equal((TypeContractKind)expectedKind, contract.Kind);
        Assert.Equal(type, contract.Type);
    }

    [Fact]
    public void Build_RecordsUnderlyingAndElementTypes()
    {
        Assert.Equal(typeof(int), TypeContractBuilder.Build(typeof(int?)).UnderlyingType);
        Assert.Equal(typeof(int), TypeContractBuilder.Build(typeof(int[])).ElementType);
        Assert.Equal(typeof(int), TypeContractBuilder.Build(typeof(List<int>)).ElementType);

        TypeContract dictionary = TypeContractBuilder.Build(typeof(Dictionary<string, SimpleDto>));
        Assert.Equal(typeof(string), dictionary.KeyType);
        Assert.Equal(typeof(SimpleDto), dictionary.ValueType);
    }

    [Fact]
    public void Build_SelectsMembersOnceForBothDirections()
    {
        TypeContract contract = TypeContractBuilder.Build(typeof(MemberSelectionDto));

        AssertMember(contract, nameof(MemberSelectionDto.PublicField), canSerialize: true, canDeserialize: true);
        AssertMember(contract, nameof(MemberSelectionDto.PublicProperty), canSerialize: true, canDeserialize: true);
        AssertMember(contract, nameof(MemberSelectionDto.PrivateSetter), canSerialize: true, canDeserialize: false);
        AssertMember(contract, nameof(MemberSelectionDto.WriteOnly), canSerialize: false, canDeserialize: true);
        AssertMember(contract, "privateValue", canSerialize: true, canDeserialize: true);
        AssertMember(contract, "includedReadOnly", canSerialize: true, canDeserialize: false);
        AssertMember(contract, "", canSerialize: true, canDeserialize: true);

        Assert.DoesNotContain(contract.Members, member => member.MemberInfo.Name == nameof(MemberSelectionDto.ReadOnlyField));
        Assert.DoesNotContain(contract.Members, member => member.MemberInfo.Name == nameof(MemberSelectionDto.Computed));
        Assert.DoesNotContain(contract.Members, member => member.MemberInfo.Name == nameof(MemberSelectionDto.Ignored));
        Assert.DoesNotContain(contract.Members, member => member.MemberInfo.Name == nameof(MemberSelectionDto.StaticField));
        Assert.DoesNotContain(contract.Members, member => member.MemberInfo.Name == nameof(MemberSelectionDto.Constant));
        Assert.DoesNotContain(contract.Members, member => member.MemberInfo.Name == nameof(MemberSelectionDto.Callback));
        Assert.DoesNotContain(contract.Members, member => member.MemberInfo.Name == "Item");
    }

    [Fact]
    public void Build_IncludesEligibleInheritedAndExplicitPrivateBaseMembers()
    {
        TypeContract contract = TypeContractBuilder.Build(typeof(DerivedDto));

        AssertMember(contract, nameof(BaseDto.BaseValue), canSerialize: true, canDeserialize: true);
        AssertMember(contract, "baseSecret", canSerialize: true, canDeserialize: true);
        AssertMember(contract, nameof(DerivedDto.DerivedValue), canSerialize: true, canDeserialize: true);
    }

    [Fact]
    public void Build_TreatsOverriddenPropertyAsOneMember()
    {
        TypeContract contract = TypeContractBuilder.Build(typeof(OverrideDerivedDto));

        MemberContract member = Assert.Single(contract.Members, item => item.JsonName == nameof(OverrideBaseDto.Value));
        Assert.Equal(typeof(OverrideDerivedDto), member.MemberInfo.DeclaringType);
    }

    [Theory]
    [InlineData(typeof(HiddenMemberDerivedDto))]
    [InlineData(typeof(RenamedCollisionDto))]
    public void Build_RejectsJsonNameCollisions(Type type)
    {
        TypeContractException exception = Assert.Throws<TypeContractException>(() => TypeContractBuilder.Build(type));

        Assert.Equal(type, exception.TargetType);
        Assert.Contains("resolve to JSON name", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RecordsExplicitJsonConstructor()
    {
        TypeContract contract = TypeContractBuilder.Build(typeof(ExplicitConstructorDto));

        Assert.NotNull(contract.ExplicitConstructor);
        Assert.True(contract.ExplicitConstructor!.IsDefined(typeof(JsonConstructorAttribute), inherit: false));
    }

    [Fact]
    public void Build_RejectsMultipleExplicitJsonConstructors()
    {
        Assert.Throws<TypeContractException>(() => TypeContractBuilder.Build(typeof(MultipleExplicitConstructorsDto)));
    }

    public static IEnumerable<object[]> UnsupportedTypes()
    {
        yield return new object[] { typeof(object), typeof(object) };
        yield return new object[] { typeof(IDisposable), typeof(IDisposable) };
        yield return new object[] { typeof(AbstractDto), typeof(AbstractDto) };
        yield return new object[] { typeof(Action), typeof(Action) };
        yield return new object[] { typeof(int[,]), typeof(int[,]) };
        yield return new object[] { typeof(Dictionary<int, string>), typeof(Dictionary<int, string>) };
        yield return new object[] { typeof(HashSet<int>), typeof(HashSet<int>) };
        yield return new object[] { typeof(List<Action>), typeof(Action) };
        yield return new object[] { typeof((int, int)), typeof((int, int)) };
        yield return new object[] { typeof(List<>), typeof(List<>) };
        yield return new object[] { typeof(int).MakeByRefType(), typeof(int).MakeByRefType() };
    }

    [Theory]
    [MemberData(nameof(UnsupportedTypes))]
    public void Build_RejectsUnsupportedTypes(Type type, Type expectedTargetType)
    {
        TypeContractException exception = Assert.Throws<TypeContractException>(() => TypeContractBuilder.Build(type));

        Assert.Equal(expectedTargetType, exception.TargetType);
    }

    [Fact]
    public void Build_RejectsDtoWithUnsupportedSelectedMember()
    {
        TypeContractException exception = Assert.Throws<TypeContractException>(() => TypeContractBuilder.Build(typeof(UnsupportedMemberDto)));

        Assert.Equal(typeof(HashSet<int>), exception.TargetType);
    }

    [Fact]
    public void Cache_ReusesTheSameContractInstance()
    {
        var cache = new TypeContractCache();

        TypeContract first = cache.GetContract(typeof(MemberSelectionDto));
        TypeContract second = cache.GetContract(typeof(MemberSelectionDto));

        Assert.Same(first, second);
    }

    [Fact]
    public void Cache_ReusesOneContractDuringConcurrentAccess()
    {
        var cache = new TypeContractCache();
        var contracts = new ConcurrentBag<TypeContract>();

        Parallel.For(0, 64, _ => contracts.Add(cache.GetContract(typeof(MemberSelectionDto))));

        TypeContract first = contracts.First();
        Assert.All(contracts, contract => Assert.Same(first, contract));
    }

    private static void AssertMember(TypeContract contract, string jsonName, bool canSerialize, bool canDeserialize)
    {
        MemberContract member = Assert.Single(contract.Members, item => item.JsonName == jsonName);
        Assert.Equal(canSerialize, member.CanSerialize);
        Assert.Equal(canDeserialize, member.CanDeserialize);
    }

    public enum SampleEnum
    {
        Value,
    }

    public sealed class SimpleDto
    {
        public int Value;
    }

    public sealed class MemberSelectionDto
    {
        public int PublicField;
        public readonly int ReadOnlyField;
        public static int StaticField;
        public const int Constant = 1;
        public Action? Callback;

        [JsonInclude]
        [JsonPropertyName("privateValue")]
        private int _privateValue;

        [JsonInclude]
        [JsonPropertyName("includedReadOnly")]
        private int IncludedReadOnly => _privateValue;

        [JsonInclude]
        private Action? _includedCallback = static () => { };

        private Action? IncludedCallback => _includedCallback;

        public int PublicProperty { get; set; }
        public int PrivateSetter { get; private set; }
        public int WriteOnly { set => _privateValue = value; }
        public int Computed => PublicField + PublicProperty;

        [JsonIgnore]
        public int Ignored { get; set; }

        [JsonPropertyName("")]
        public int EmptyName { get; set; }

        public int this[int index]
        {
            get => index;
            set => _privateValue = value;
        }
    }

    public class BaseDto
    {
        public int BaseValue;

        [JsonInclude]
        [JsonPropertyName("baseSecret")]
        private int _baseSecret = 1;

        private int BaseSecret => _baseSecret;
    }

    public sealed class DerivedDto : BaseDto
    {
        public int DerivedValue;
    }

    public class OverrideBaseDto
    {
        public virtual int Value { get; set; }
    }

    public sealed class OverrideDerivedDto : OverrideBaseDto
    {
        public override int Value { get; set; }
    }

    public class HiddenMemberBaseDto
    {
        public int Value;
    }

    public sealed class HiddenMemberDerivedDto : HiddenMemberBaseDto
    {
        public new int Value;
    }

    public sealed class RenamedCollisionDto
    {
        [JsonPropertyName("same")]
        public int First;

        [JsonPropertyName("same")]
        public int Second;
    }

    public sealed class ExplicitConstructorDto
    {
        public int Value;

        public ExplicitConstructorDto()
        {
        }

        [JsonConstructor]
        private ExplicitConstructorDto(int Value)
        {
            this.Value = Value;
        }
    }

    public sealed class MultipleExplicitConstructorsDto
    {
        [JsonConstructor]
        public MultipleExplicitConstructorsDto()
        {
        }

        [JsonConstructor]
        public MultipleExplicitConstructorsDto(int value)
        {
        }
    }

    public abstract class AbstractDto
    {
        public int Value;
    }

    public sealed class UnsupportedMemberDto
    {
        public HashSet<int> Values = new();
    }
}
