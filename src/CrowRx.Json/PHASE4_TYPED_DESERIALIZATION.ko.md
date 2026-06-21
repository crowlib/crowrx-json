# CrowRx.Json 2.x Phase 4 결과

작성일: 2026-06-20
범위: mutable DTO typed deserialization

## 구현 결과

- strict `JsonReader`의 JSON tree를 cached `TypeContract`에 따라 직접 변환하는 `JsonDeserializer`를 추가했다.
- typed 경로에서는 JSON 숫자의 원문을 `JsonNumber`로 보존해 대상 숫자 타입별 범위와 token shape를 검사한다.
- bool, string, char, 모든 정수 타입, float, double, decimal, enum, nullable, `Guid`, `DateTime`, `DateTimeOffset` 변환을 구현했다.
- 정수 대상은 소수부나 지수가 있는 JSON number를 거부하며, overflow와 부동소수점/decimal underflow를 제어된 오류로 처리한다.
- enum은 대소문자가 정확히 일치하는 정의된 이름만 허용하고 숫자 enum 입력은 거부한다.
- 1차원 배열, `List<T>`/`IList<T>`와 `Dictionary<string, TValue>`/`IDictionary<string, TValue>`를 재귀적으로 생성한다.
- 접근 가능한 매개변수 없는 생성자를 가진 class와 기본 생성 가능한 struct DTO를 만든 뒤 `CanDeserialize` 멤버를 `JsonName`으로 연결해 채운다.
- 누락된 멤버는 생성 직후 초기값을 유지하고, 알 수 없는 멤버는 기본적으로 무시한다.
- strict unknown-member 정책은 내부 typed deserializer 플래그와 테스트로 고정했으며 공개 options 연결은 Phase 7에 남겼다.

## 공개 API와 호환 경로

- `JsonSerializer.Serialize<T>`를 새 writer의 공개 진입점으로 추가했다.
- `JsonSerializer.Deserialize<T>`와 Type 기반 overload를 추가했다.
- `JsonSerializer.TryDeserialize<T>`와 Type 기반 overload는 실패 시 `JsonError`를 반환한다.
- 예외 API는 동일한 `JsonError`를 보관하는 `JsonException`을 발생시킨다.
- custom activator가 없는 기존 `ConvertFromJson`/`TryConvertFromJson` 신규 객체 경로를 typed deserializer에 연결했다.
- custom activator 경로와 모든 overwrite/append API는 기존 `Deserializer`에 남겨 Phase 6~7 경계를 보존했다.

## 오류와 원자성

- 변환 오류 범주로 type mismatch, numeric overflow, invalid value, unsupported type, object creation, member assignment와 unknown member를 추가했다.
- 변환 오류는 JSON path, 예상 C# 타입과 실제 JSON token 종류를 제공한다.
- parser 오류는 기존 position, line, column을 그대로 유지한다.
- 배열/list/dictionary/DTO의 중첩 변환 실패는 정확한 element/member path에서 전체 작업을 실패시킨다.
- DTO와 collection은 완성되기 전까지 지역 값으로만 유지되므로 실패 시 일부 채워진 결과를 호출자에게 노출하지 않는다.

## 테스트 기준

`JsonDeserializerTests`와 갱신한 defect 회귀 테스트는 다음을 검증한다.

- 모든 scalar 타입과 정수/decimal 경계값
- overflow, underflow, 정수 token shape와 token type 불일치
- enum, nullable, root null, GUID와 ISO 8601 날짜/시간
- 배열/list/dictionary와 interface collection 생성
- 중첩 collection 실패 path
- mutable DTO, private include, `JsonName`, 누락 초기값과 unknown member 기본 정책
- strict unknown-member 정책
- 멤버 실패 후 부분 객체 비노출
- 매개변수 없는 생성자가 없는 DTO의 Phase 4 실패
- `JsonException`과 parser 위치 정보 보존

Phase 0~3 테스트와 Phase 4 테스트를 합쳐 총 160개 테스트가 통과한다.

## 검증 결과

- clean-equivalent Release build 성공
- 기존 legacy nullable 경고 20개 유지, Phase 4 코드의 새 nullable 경고 없음
- Release test 160개 통과
- Release pack 성공
- 완전히 새로운 NuGet package cache를 사용한 별도 package smoke test 성공

## 의도적으로 남긴 경계

- `[JsonConstructor]`, 매개변수 생성자, immutable DTO와 record 바인딩은 Phase 5에 남겼다.
- overwrite/append의 typed 재작성과 원자성 의미 변경은 Phase 6에 남겼다.
- public strict option, converter, custom activator와 options 전역 구조는 Phase 7에 남겼다.
- polymorphic metadata, `UnityEngine.Object`, runtime code generation과 `Expression.Compile`은 추가하지 않았다.
