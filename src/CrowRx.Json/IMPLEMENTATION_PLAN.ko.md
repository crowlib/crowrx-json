# CrowRx.Json 2.x 구현 계획

상태: 초안
기준 사양: [`SERIALIZATION_SPEC.ko.md`](SERIALIZATION_SPEC.ko.md)
대상 프레임워크: .NET Standard 2.1 / C# 9

## 1. 구현 목표

CrowRx.Json 2.x는 일반 C#과 Unity에서 함께 사용할 수 있는 가벼운 DTO JSON 직렬화기를 목표로 한다.

핵심 성공 조건은 다음과 같다.

1. 지원한다고 선언한 JSON과 C# 타입을 손실 없이 왕복한다.
2. 변환에 실패한 멤버가 있으면 전체 작업이 명확하게 실패한다.
3. 불변 DTO와 record를 생성자 바인딩으로 지원한다.
4. 반복 리플렉션과 불필요한 중간 객체 생성을 피한다.
5. Unity 의존성 없이 동작하고, Unity 환경에서는 Mono와 IL2CPP를 검증한다.
6. 기존 1.x API 사용자가 2.x로 이동할 수 있는 명시적인 경로를 제공한다.

## 2. 구현 원칙

### 2.1 정확성을 성능보다 먼저 고정한다

성능 최적화 전에 오류 모델, 타입 변환, 멤버 선택 및 생성자 선택 규칙을 테스트로 고정한다. 잘못된 결과를 빠르게 반환하는 구현은 허용하지 않는다.

### 2.2 직접 타입 변환을 기본 경로로 사용한다

현재의 `JSON -> Dictionary/List -> C# 객체` 경로는 중간 할당과 두 번째 객체 순회를 만든다. 2.x의 최종 기본 경로는 JSON 토큰을 읽으며 대상 타입으로 직접 변환한다.

내부 DOM이 진단이나 별도 API에 필요해질 수는 있지만, typed API의 필수 중간 단계로 사용하지 않는다.

### 2.3 AOT 호환 경로를 기준 구현으로 삼는다

런타임 코드 생성과 `Expression.Compile()`을 필수로 사용하지 않는다. 타입 메타데이터 캐시와 일반 리플렉션 호출만으로도 동작하는 기준 경로를 먼저 완성한다.

JIT 전용 고속 접근자나 source generator는 측정 결과 필요할 때 별도 계층으로 추가한다.

### 2.4 최적화는 벤치마크로 승인한다

풀링, 커스텀 버퍼 및 특수화된 변환 코드는 실제 할당량이나 처리 시간을 개선한다는 측정 결과가 있을 때만 도입한다.

## 3. 목표 구조

```text
Public API
  |
  +-- JsonSerializer / JsonSerializerOptions
  |     |
  |     +-- JsonReader ------> TypeContract ------> Object construction
  |     |                         |
  |     |                         +-- Members
  |     |                         +-- Constructor binding
  |     |                         +-- Converter
  |     |
  |     +-- JsonWriter <------ TypeContract <------ Object graph
  |
  +-- JsonResult / JsonError

TypeContractCache
  +-- Scalar contract
  +-- Enum contract
  +-- Nullable contract
  +-- Collection contract
  +-- Dictionary contract
  +-- DTO contract
```

### 3.1 예상 소스 구성

```text
Public/
  JsonSerializer.cs
  JsonSerializerOptions.cs
  JsonResult.cs
  JsonError.cs
  JsonException.cs
  Attributes/
  Converters/

Parsing/
  JsonReader.cs
  JsonTokenType.cs
  JsonParseError.cs

Writing/
  JsonWriter.cs

Contracts/
  TypeContract.cs
  TypeContractBuilder.cs
  TypeContractCache.cs
  MemberContract.cs
  ConstructorContract.cs

Activation/
  IJsonActivator.cs
  DefaultJsonActivator.cs

Compatibility/
  StringExtension.cs
```

실제 파일 분리는 각 단계에서 책임이 확인된 후 적용한다. 빈 추상화나 사용되지 않는 인터페이스를 계획만 보고 미리 만들지 않는다.

## 4. 공개 API 목표

초기 API 초안은 다음과 같다.

```csharp
public static class JsonSerializer
{
    public static string Serialize<T>(T value, JsonSerializerOptions? options = null);

    public static T? Deserialize<T>(string json, JsonSerializerOptions? options = null);

    public static bool TryDeserialize<T>(
        string json,
        out T? value,
        out JsonError error,
        JsonSerializerOptions? options = null);

    public static bool TryOverwrite<T>(
        string json,
        T target,
        out JsonError error,
        JsonSerializerOptions? options = null);
}
```

`Try` API는 실패 이유를 버리지 않는다. 예외 API는 같은 `JsonError` 정보를 기반으로 `JsonException`을 발생시킨다.

현재의 `ToJson()`, `ConvertFromJson<T>()`, `TryConvertFromJson<T>()` 확장 메서드는 2.x 코어 API를 호출하는 호환 래퍼로 유지할 수 있다. 의미가 달라지는 메서드는 `Obsolete` 처리와 마이그레이션 안내가 필요하다.

## 5. 단계별 구현

## Phase 0. 기준선과 프로젝트 정비

### 작업

- 손상된 저장소 `.git/config`를 별도 작업으로 복구한다.
- 테스트 프로젝트 `CrowRx.Json.Tests`를 추가한다.
- 벤치마크 프로젝트 `CrowRx.Json.Benchmarks`의 최소 골격을 추가한다.
- nullable 경고 29개의 위치와 원인을 분류한다.
- 현재 1.x 동작을 대표하는 회귀 테스트를 작성한다.
- 1.x에서 유지할 API와 중단할 암묵적 동작을 표로 작성한다.
- `CrowRx.Core`, `CrowRx.Pool` 의존성의 실제 용도를 기록하고 제거 여부를 결정한다.

### 완료 조건

- 일반 `dotnet build`와 `dotnet test`가 별도 우회 옵션 없이 실행된다.
- 현재 동작과 알려진 결함을 재현하는 테스트가 존재한다.
- 이후 단계에서 성능을 비교할 기준 벤치마크 결과가 저장된다.

## Phase 1. 오류 모델과 엄격한 JSON reader

### 작업

- 문자열 인덱스 기반 `JsonReader`를 구현한다.
- 객체, 배열, 문자열, 숫자, 불리언 및 `null` 토큰을 구분한다.
- 전체 입력 소비와 뒤쪽 공백 규칙을 구현한다.
- 문자열 이스케이프, 제어 문자, 유니코드 및 서로게이트 쌍을 검증한다.
- JSON 숫자 문법을 검증한 후 invariant culture로 변환한다.
- 중복 키를 감지할 수 있도록 객체 멤버 이름 추적 경로를 제공한다.
- 오류 종류, 위치, 줄/열 및 JSON 경로를 담는 `JsonError`를 정의한다.
- 최대 깊이 제한을 구현한다.

### 테스트

- JSON 표준의 정상/비정상 문자열과 숫자 사례
- 빈 문자열 객체 키
- 루트 scalar 및 `null`
- 뒤쪽 쓰레기 문자
- 닫히지 않은 객체/배열/문자열
- 잘못된 이스케이프와 서로게이트
- 중복 키와 최대 깊이

### 완료 조건

- 잘못된 JSON이 부분 성공이나 기본값으로 변환되지 않는다.
- 모든 파싱 실패가 원본 위치와 오류 범주를 제공한다.

## Phase 2. 타입 계약과 메타데이터 캐시

### 작업

- `TypeContract` 종류를 scalar, enum, nullable, collection, dictionary, DTO로 분리한다.
- `ConcurrentDictionary<Type, TypeContract>` 기반 캐시를 구현한다.
- public 인스턴스 필드와 프로퍼티 선택 규칙을 구현한다.
- static, indexer, const, event, delegate 제외 규칙을 구현한다.
- readonly 및 계산 프로퍼티의 기본 제외 규칙을 구현한다.
- include, ignore, 이름 재정의 및 생성자 지정 특성의 이름과 API를 확정한다.
- 상속 멤버와 JSON 이름 충돌을 계약 생성 시 검출한다.
- 지원하지 않는 타입은 직렬화 시작 전에 계약 생성 오류로 거부한다.

### 테스트

- public 필드와 프로퍼티
- getter/setter 조합
- readonly, static, indexer 및 계산 프로퍼티
- 상속 및 멤버 이름 충돌
- 명시적 include/ignore/name 특성
- 동일 타입 계약의 캐시 재사용과 동시 접근

### 완료 조건

- 동일 타입의 반복 처리에서 멤버 리플렉션 탐색이 다시 실행되지 않는다.
- 지원 여부와 멤버 선택 결과가 직렬화/역직렬화에서 동일한 계약을 사용한다.

## Phase 3. JSON writer와 직렬화

### 작업

- scalar, enum, nullable 및 문자열 직렬화를 구현한다.
- 배열, `List<T>` 및 `Dictionary<string, TValue>`를 구현한다.
- DTO 멤버를 `TypeContract` 순서로 직렬화한다.
- 결정적인 멤버 출력 순서를 정의한다.
- 순환 참조 검출과 최대 깊이를 구현한다.
- `NaN`과 무한대를 거부한다.
- 문자열이 아닌 딕셔너리 키를 거부한다.
- `Serialize` 및 `ToJson` 호환 경로를 연결한다.

### 테스트

- 모든 지원 scalar와 경계값
- escaping 및 Unicode 왕복
- enum 이름 표현
- 배열, 리스트 및 딕셔너리
- 중첩 DTO
- 순환 참조와 반복 참조
- 결정적인 출력
- 지원하지 않는 타입의 명시적 실패

### 완료 조건

- 지원 타입의 출력이 표준 JSON 파서로 읽힌다.
- 순환 참조가 스택 오버플로 대신 제어된 오류를 반환한다.

## Phase 4. mutable DTO 역직렬화

### 작업

- scalar, enum, nullable 및 문자열 직접 변환을 구현한다.
- 숫자 범위와 토큰 타입을 엄격히 검사한다.
- 배열, 리스트 및 문자열 키 딕셔너리를 직접 생성한다.
- 매개변수 없는 생성자를 사용하는 mutable DTO를 지원한다.
- 생성 후 쓰기 가능한 필드와 프로퍼티를 채운다.
- 누락 멤버, 알 수 없는 멤버 및 엄격 모드 정책을 구현한다.
- 멤버 실패 시 전체 작업 실패와 정확한 JSON 경로를 보장한다.

### 테스트

- scalar 변환 성공과 범위 초과
- nullable과 루트 `null`
- 누락/알 수 없는/잘못된 멤버
- 배열, 리스트, 딕셔너리의 중첩 실패 경로
- 일부 멤버가 성공한 뒤 발생한 실패

### 완료 조건

- 필드 변환 실패를 무시하면서 성공을 반환하는 경로가 없다.
- 새 객체 API는 일부만 채워진 값을 성공 결과로 노출하지 않는다.

## Phase 5. 생성자 바인딩과 불변 DTO

### 작업

- 명시적으로 지정한 JSON 생성자 특성을 구현한다.
- 유일한 public 생성자와 public 매개변수 없는 생성자 우선순위를 구현한다.
- 생성자 매개변수와 JSON 멤버 이름을 연결한다.
- 필수 매개변수, 선택적 매개변수 및 기본값을 처리한다.
- 생성자 인자 변환 후 객체를 한 번만 생성한다.
- 생성자에서 소비하지 않은 쓰기 가능한 멤버를 생성 후 채운다.
- 모호한 생성자와 매개변수/멤버 충돌을 명시적으로 실패시킨다.
- 일반 불변 클래스와 C# record를 검증한다.

### 테스트

- get-only 프로퍼티와 readonly 필드
- 위치 기반 record와 일반 record class
- 선택적 생성자 매개변수
- 누락된 필수 매개변수
- 명시적 생성자 선택
- 다중 생성자 모호성
- 생성자 예외의 오류 변환

### 완료 조건

- 지원하는 불변 DTO가 `value -> JSON -> value` 왕복 테스트를 통과한다.
- readonly 필드를 객체 생성 후 리플렉션으로 변경하지 않는다.

## Phase 6. overwrite와 append

### 작업

- ordinary deserialize와 overwrite 내부 경로를 분리한다.
- JSON에 존재하는 DTO 멤버만 갱신한다.
- 컬렉션 replace와 append를 별도 옵션 또는 API로 표현한다.
- 딕셔너리 append의 중복 키 실패 정책을 구현한다.
- 트랜잭션 방식 overwrite의 비용을 측정한다.
- 트랜잭션을 제공하지 못하면 부분 변경 가능성을 API와 문서에 명시한다.
- 1.x `TryConvertFromJsonOverwrite` 및 `isAppend` 마이그레이션 경로를 구현한다.

### 테스트

- 누락 멤버 보존
- 컬렉션 교체와 추가
- 중복 키
- 중첩 객체 overwrite
- 작업 도중 실패했을 때 대상 객체 상태

### 완료 조건

- overwrite 실패 시 객체 상태에 대한 계약이 테스트와 문서에서 일치한다.
- JSON 객체를 리스트 값으로 바꾸는 기존 암묵적 변환은 존재하지 않는다.

## Phase 7. 컨버터, 액티베이터 및 옵션

### 작업

- `IJsonConverter<T>` 공개 계약을 확정한다.
- 컨버터 우선순위와 중복 등록 정책을 구현한다.
- 커스텀 액티베이터를 새 계약에 맞게 정리한다.
- 전역 변경 가능 레지스트리 대신 immutable options 구성을 사용한다.
- 대소문자, 알 수 없는 멤버 및 숫자 enum 정책을 옵션화한다.
- `Guid`, `DateTime`, `DateTimeOffset` 기본 컨버터를 구현한다.
- Unity 값 타입 컨버터는 별도 패키지 후보로 유지하고 코어에는 넣지 않는다.

### 테스트

- 기본 컨버터와 사용자 컨버터 우선순위
- 옵션 인스턴스 간 격리
- 병렬 직렬화/역직렬화
- 커스텀 생성이 필요한 타입

### 완료 조건

- 커스텀 기능이 전역 상태를 변경하지 않는다.
- 코어 어셈블리는 UnityEngine 참조 없이 빌드된다.

## Phase 8. 성능, Unity 검증 및 배포

### 작업

- cold/warm, 처리량 및 할당량 벤치마크를 실행한다.
- 현재 1.x, CrowRx.Json 2.x 및 비교 기준 직렬화기의 결과를 기록한다.
- 메타데이터 캐시, reader, writer 및 컬렉션 경로를 프로파일링한다.
- 측정으로 필요성이 확인된 경우에만 writer 버퍼 풀링을 적용한다.
- Unity 지원 버전에서 Mono 및 IL2CPP smoke test 프로젝트를 실행한다.
- managed code stripping 단계에서 계약과 생성자가 보존되는지 확인한다.
- NuGet 패키지와 Unity 설치 방법을 검증한다.
- README, API 문서 및 1.x 마이그레이션 문서를 갱신한다.

### 완료 조건

- 공개한 모든 기능이 적합성 테스트를 통과한다.
- 성능 수치는 재현 가능한 벤치마크와 함께 기록된다.
- Unity 호환성 주장은 실제 Mono 및 IL2CPP 실행 결과로 뒷받침된다.
- nullable 경고 없이 패키지가 빌드된다.

## 6. 테스트 전략

### 6.1 테스트 계층

- 단위 테스트: reader, writer, 타입 계약, 변환기
- 적합성 테스트: 사양의 각 항목과 공개 API
- 회귀 테스트: 발견된 모든 결함
- 왕복 테스트: `value -> JSON -> value`
- 상호 운용 테스트: 표준 JSON 구현이 CrowRx.Json 출력을 읽고 반대 방향도 처리
- fuzz/property 테스트: 임의 문자열, 숫자, 중첩 배열 및 객체
- 통합 테스트: NuGet 소비 프로젝트와 Unity 프로젝트

### 6.2 성공 판정 원칙

- 단순히 예외가 없다는 이유로 성공으로 보지 않는다.
- 결과 값, 오류 범주, JSON 경로 및 대상 객체 상태를 함께 검증한다.
- 성능 테스트는 기능 테스트와 분리하며, 기능 실패를 성능 회귀로 숨기지 않는다.

## 7. 성능 측정 항목

대표 데이터 세트는 다음을 포함한다.

- 작은 flat DTO
- 문자열과 숫자가 많은 DTO
- 중첩 DTO
- 큰 배열과 리스트
- 문자열 키 딕셔너리
- mutable DTO와 constructor-bound immutable DTO

각 데이터 세트에서 다음을 측정한다.

- 초당 처리량
- 작업당 평균/중앙값 지연 시간
- 작업당 할당 바이트
- 최초 타입 계약 생성 비용
- 캐시 준비 이후 비용
- 직렬화와 역직렬화 각각의 비용

목표 수치는 기준선 측정 후 별도 성능 예산 문서에서 확정한다. 측정 전에 임의의 배수 목표를 약속하지 않는다.

## 8. 1.x에서 제거하거나 변경할 동작

- private 필드의 무조건적인 포함을 제거한다.
- 필드 변환 실패를 무시한 전체 성공을 제거한다.
- JSON 객체를 값 목록으로 암묵 변환하는 동작을 제거한다.
- 비문자열 딕셔너리 키의 `ToString()` 자동 변환을 제거한다.
- 임의 타입에 대한 무조건적인 `Activator.CreateInstance` 사용을 제거한다.
- 유효하지 않은 숫자를 `0`으로 바꾸는 동작을 제거한다.
- 순환 참조로 인한 무한 재귀 가능성을 제거한다.
- 파싱 실패와 JSON `null`을 같은 값으로 표현하지 않는다.

## 9. 주요 위험과 대응

### 생성자 바인딩의 복잡성

이름 정책, 기본값, nullable 및 다중 생성자 때문에 경우의 수가 많다. Phase 5를 독립 단계로 유지하고 mutable DTO 경로가 안정된 뒤 구현한다.

### 리플렉션 성능과 AOT의 충돌

JIT 코드 생성을 기본 최적화로 사용하지 않는다. 메타데이터 캐시를 먼저 적용하고, 추가 최적화는 JIT와 AOT 경로를 분리할 가치가 벤치마크로 확인될 때만 도입한다.

### overwrite 원자성 비용

복사본 생성은 큰 객체에서 비쌀 수 있다. 정확한 원자성 보장 범위를 Phase 6에서 측정하고, 보장하지 못하는 경우 API 이름과 문서로 위험을 드러낸다.

### 범용 기능의 무제한 확장

지원 타입은 사양과 적합성 테스트가 있는 경우에만 추가한다. 다형성, 참조 보존, 임의 컬렉션 및 source generator는 2.x 핵심 완료 조건에 포함하지 않는다.

## 10. 권장 구현 순서

첫 번째 실제 구현 단위는 **Phase 0의 테스트 기반과 Phase 1의 엄격한 reader**다. 기존 객체 매퍼를 먼저 고치면 잘못된 JSON과 타입 변환 실패를 구분하기 어려워진다.

각 Phase는 다음 순서를 따른다.

1. 사양 사례를 실패 테스트로 작성한다.
2. 가장 작은 구현으로 테스트를 통과시킨다.
3. 회귀 테스트와 오류 경로를 추가한다.
4. 빌드와 전체 테스트를 실행한다.
5. 성능에 영향을 주는 단계라면 기준 벤치마크를 비교한다.
6. 완료 조건을 만족한 뒤 다음 Phase로 이동한다.
