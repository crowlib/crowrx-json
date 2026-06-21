# CrowRx.Json Serialization Specification

Status: Draft
Target: CrowRx.Json 2.x

## 1. Purpose

CrowRx.Json is a lightweight JSON serializer for general-purpose C# DTOs. The
same core library must work in standard .NET and Unity environments.

The library prioritizes:

1. Correct and deterministic results.
2. A small, convenient public API.
3. Predictable performance and allocation behavior.
4. Explicit failure instead of silent data loss.

It is not intended to serialize arbitrary runtime object graphs or to replace
Unity serialization for `UnityEngine.Object` instances.

## 2. JSON compliance

- Input must conform to the JSON grammar.
- The parser must consume the complete input except for trailing whitespace.
- Malformed input must fail. It must not return a partial value as success.
- Object keys must be JSON strings. Empty string keys are valid.
- `NaN`, positive infinity, and negative infinity are not valid JSON numbers.
- String escapes, Unicode escape sequences, and surrogate pairs must be
  validated.
- Duplicate object keys are rejected by default. A future option may define a
  different policy.

## 3. Supported root values

The following JSON values may appear at the document root:

- Object
- Array
- String
- Number
- Boolean
- `null`

Root `null` succeeds when the requested target type can contain `null` and
fails otherwise.

## 4. Supported C# types

### 4.1 Built-in scalar types

- `bool`
- `string`
- `char`
- Signed and unsigned integer types
- `float`, `double`, and `decimal`
- Enum types
- Nullable forms of supported value types
- `Guid`
- `DateTime` and `DateTimeOffset`

Enum values are written as names by default. Numeric enum input is accepted
only when explicitly enabled.

`DateTime` and `DateTimeOffset` use an invariant ISO 8601 representation.

Numeric conversion must be range checked. Overflow, precision-invalid input,
and incompatible JSON token types fail instead of being coerced silently.

### 4.2 Collections

The initial supported collection types are:

- One-dimensional arrays
- `List<T>`
- Types assignable to `IList<T>` when a concrete implementation is supplied
  by configuration or a custom activator
- `Dictionary<string, TValue>`
- Types assignable to `IDictionary<string, TValue>` under the same activation
  rule

JSON objects are never implicitly converted to lists. JSON arrays are never
implicitly converted to dictionaries.

Dictionary keys must be strings. Non-string C# dictionary keys require an
explicit converter.

Multidimensional arrays, tuples, sets, queues, stacks, and other collection
types are unsupported until separately specified.

### 4.3 DTO types

A DTO is a class or struct whose serialized members are selected according to
section 5. Interfaces, abstract classes, delegates, pointers, and open generic
types are unsupported unless handled by an explicit converter.

Polymorphic type metadata is not emitted or consumed by default.

## 5. Member selection

### 5.1 Default members

By default, CrowRx.Json includes:

- Public instance fields.
- Public instance properties with a public getter when serializing.
- Public instance properties with a public setter or a matching constructor
  parameter when deserializing.

Static members, indexers, constants, events, and delegates are always ignored.

### 5.2 Explicit inclusion and exclusion

Private or protected fields and properties are included only when marked with
a CrowRx.Json inclusion attribute.

Any otherwise eligible member may be excluded with a CrowRx.Json ignore
attribute.

The concrete attribute names and their interaction with platform attributes
must be defined before implementation. Supporting attributes such as
`System.NonSerializedAttribute` and Unity's serialization attributes is a
compatibility decision, not an implicit requirement.

### 5.3 Read-only members

- A readable property without a setter and a readonly field may participate in
  round-trip serialization when the member is bound to a constructor
  parameter.
- A read-only member that is not constructor-bound is excluded by default.
- Computed properties are excluded by default unless explicitly included for
  serialization-only output.
- Reflection must not be used to mutate readonly fields after construction.

### 5.4 Inheritance

Eligible inherited public members are included. Private members declared by a
base type are included only when explicitly marked and when the runtime can
access them safely.

Member name collisions across an inheritance hierarchy are configuration
errors and must not be resolved silently.

### 5.5 Member names

Member matching is case-sensitive by default. Naming policies and per-member
name overrides may be added as explicit options.

Two C# members must not resolve to the same JSON name.

## 6. Object construction

The default deserializer supports DTO classes with either an accessible
parameterless constructor or one unambiguously selected parameterized
constructor. Structs are initialized with their default value or through a
selected constructor when required.

Constructor binding is part of the initial 2.x specification so that immutable
DTOs and records can participate in round-trip serialization.

Constructor parameters are matched to serialized member names. Matching is
case-sensitive by default and follows the configured JSON naming policy. Every
required constructor parameter must have a compatible JSON value or an
explicit default value.

Constructor selection must be deterministic. The initial implementation uses
the following precedence:

1. A constructor explicitly marked for JSON deserialization.
2. The only accessible public constructor.
3. An accessible public parameterless constructor.

If more than one constructor remains eligible at the same precedence, the
operation fails with an ambiguous-constructor error. The serializer must not
guess based on whichever constructor reflection returns first.

After construction, remaining writable members are populated normally.

Types requiring other construction rules need either:

- A registered custom activator.
- A registered converter.

## 7. Null handling

- JSON `null` maps to reference types and nullable value types.
- JSON `null` targeting a non-nullable value type fails.
- A C# `null` value is serialized as JSON `null`.
- Missing members and members explicitly set to `null` are distinct cases.
- Nullable reference annotations may be used for diagnostics but are not a
  complete runtime validation system.

## 8. Missing and unknown members

Default behavior:

- A missing JSON member leaves the C# member at its initialized value.
- An unknown JSON member is ignored.
- A present member whose value cannot be converted causes the complete
  operation to fail.

Strict unknown-member handling may be provided as an option.

## 9. Failure and atomicity

A successful operation guarantees that every present, selected member was
converted successfully. Field-level failures must never be discarded while
returning overall success.

Failure information should contain:

- Error category.
- JSON path.
- Expected C# type.
- Actual JSON token type.
- Source position when available.
- Human-readable explanation.

Exception-based and `Try` APIs may coexist, but they must report the same
underlying failure semantics.

Deserializing into a new object is atomic from the caller's perspective: no
partially populated result is returned as success.

## 10. Overwrite and append behavior

Overwrite is a separate operation from ordinary deserialization.

- DTO overwrite updates only members present in the JSON input.
- Collection replacement clears and repopulates the existing collection.
- Append mode is supported only for explicitly supported list and dictionary
  types.
- Dictionary append fails on a duplicate key by default.
- If overwrite fails, the API must clearly document whether the original
  object can have been partially modified.

The preferred target is transactional overwrite, but it is not promised until
an implementation strategy and performance budget are approved.

## 11. Reference graphs and recursion

CrowRx.Json serializes JSON trees, not arbitrary reference graphs.

- Reference identity is not preserved.
- Cyclic references cause a controlled serialization failure.
- A configurable maximum depth protects against excessive recursion.
- Repeated references are serialized as repeated values unless a cycle is
  encountered.

## 12. Customization

Customization is explicit and type based:

- Custom converter for reading and writing a type.
- Custom activator for constructing a type.
- Serializer options for naming and validation policies.

Converters take precedence over reflection-based DTO handling. Global mutable
registries are avoided; configuration belongs to a serializer options or
serializer instance.

## 13. Unity boundary

The core package contains no dependency on `UnityEngine`.

- Plain C# DTOs must behave the same in .NET and Unity.
- `UnityEngine.Object` and derived types are unsupported by the core reflection
  serializer.
- Unity value types such as vectors may be supported through an optional Unity
  converter package.
- IL2CPP and managed code stripping compatibility must be verified by tests and
  documented for every reflection-dependent feature.

## 14. Performance contract

The initial implementation does not promise to outperform every platform
serializer. It does promise to avoid unnecessary repeated work:

- Reflection metadata is cached per type.
- Serialization does not repeatedly discover members for each object.
- Hot paths avoid avoidable boxing and temporary allocations where practical.
- Parser and writer state must not rely on global mutable state.
- Caches and serializer instances must be safe for documented concurrent use.

Performance claims require benchmarks covering throughput, allocations, cold
start, warm cache, and representative nested DTOs.

## 15. Compatibility and versioning

Changes to default member selection, naming, enum representation, null
handling, or failure policy are serialization-contract changes and require a
major version or an explicit compatibility mode.

Serialized output ordering is deterministic for a given type and options, but
consumers must not rely on object member order because JSON object order has no
semantic meaning.

## 16. Required conformance tests

Before a feature is considered supported, tests must cover:

- Valid and malformed JSON grammar.
- Every built-in scalar type and its boundary values.
- Numeric overflow and invalid numeric syntax.
- Null, missing, and unknown members.
- Public fields and properties.
- Parameterized constructors, immutable DTOs, and records.
- Constructor selection, default parameters, and ambiguity failures.
- Explicitly included non-public members.
- Inheritance and member-name collisions.
- Arrays, lists, and string-keyed dictionaries.
- Nested failure paths.
- Cycles and maximum depth.
- New-object and overwrite atomicity.
- Concurrent serializer use.
- Unity Mono and IL2CPP builds for features advertised as Unity compatible.
