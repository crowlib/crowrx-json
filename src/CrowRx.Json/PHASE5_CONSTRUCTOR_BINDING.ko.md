# CrowRx.Json 2.x Phase 5 결과

작성일: 2026-06-20
범위: 생성자 바인딩과 불변 DTO

## 구현 결과

- `ConstructorContract`와 `ConstructorParameterContract`를 cached `TypeContract`에 추가했다.
- 생성자 선택은 `[JsonConstructor]`, 유일한 public 생성자, 여러 public 생성자 중 public parameterless 생성자 순서로 결정한다.
- `[JsonConstructor]`는 non-public 생성자에도 적용되며 같은 타입에 둘 이상이면 모호성 오류다.
- public 생성자가 여러 개이고 public parameterless 생성자도 없으면 reflection 반환 순서에 의존하지 않고 모호성 오류로 실패한다.
- 생성자 매개변수는 대소문자를 구분해 `MemberContract.JsonName`과 연결한다.
- 매개변수와 멤버 타입이 다르거나 매개변수에 대응하는 직렬화 멤버가 없으면 계약 생성 단계에서 실패한다.
- 생성자에 연결된 get-only property와 public readonly field를 왕복 계약에 포함한다.
- 생성자에 연결되지 않은 read-only/computed 멤버의 기존 제외 정책은 유지한다.

## 객체 생성과 원자성

- 모든 필수 생성자 인자를 먼저 변환한 뒤 생성자를 정확히 한 번 호출한다.
- 누락된 선택적 매개변수는 C# metadata의 선언된 기본값을 사용한다.
- 누락된 필수 매개변수는 해당 JSON member path의 `MissingRequiredMember` 오류다.
- 생성자가 소비한 JSON 멤버는 객체 생성 후 다시 setter/field assignment 하지 않는다.
- 생성자가 소비하지 않은 writable 멤버만 기존 mutable DTO 경로로 채운다.
- 생성자 인자 변환 실패나 생성자 예외가 발생하면 객체를 성공 결과로 노출하지 않는다.
- 생성자 내부 예외는 `ConstructorInvocationFailed`와 루트 path를 가진 `JsonError`로 변환한다.
- readonly field는 생성 후 reflection으로 변경하지 않는다.

## 지원 형태

- 일반 immutable class의 get-only property
- 생성자에 연결된 public readonly field
- C# positional record class
- public parameterless 생성자와 init property를 사용하는 일반 record class
- 선택적 생성자 매개변수
- 생성자 인자 외에 추가 writable 멤버가 있는 DTO

## 테스트 기준

`ConstructorBindingTests`와 갱신한 기존 계약/typed deserializer 테스트는 다음을 검증한다.

- immutable class와 readonly field 왕복
- positional record와 일반 record class 왕복
- 선택적 기본값과 누락된 필수 인자 path
- 명시적 private JSON 생성자 우선순위
- 여러 public 생성자 중 parameterless 생성자 우선순위
- 생성자가 소비한 writable 멤버의 중복 assignment 방지
- 생성 후 남은 writable 멤버 assignment
- 다중 생성자 모호성
- 매개변수/멤버 이름 및 타입 충돌
- 생성자 예외의 `JsonError` 변환

Phase 0~4 테스트와 Phase 5 테스트를 합쳐 총 174개 테스트가 통과한다.

## 검증 결과

- clean-equivalent Release build 성공
- 기존 legacy nullable 경고 20개 유지, Phase 5 코드의 새 nullable 경고 없음
- Release test 174개 통과
- Release pack 성공
- 완전히 새로운 NuGet package cache에서 immutable DTO 생성자 바인딩을 포함한 별도 package smoke test 성공

## 의도적으로 남긴 경계

- ordinary deserialize와 overwrite/append의 typed 통합은 Phase 6에 남겼다.
- converter, custom activator, public strict mode와 options 전역 구조는 Phase 7에 남겼다.
- polymorphic metadata, `UnityEngine.Object`, runtime code generation과 `Expression.Compile`은 추가하지 않았다.
