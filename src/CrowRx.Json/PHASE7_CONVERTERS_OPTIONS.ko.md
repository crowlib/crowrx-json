# CrowRx.Json 2.x Phase 7 결과

작성일: 2026-06-21
범위: 컨버터, 액티베이터 및 immutable options

## 구현 결과

- 공개 `IJsonConverter<T>`는 정확히 한 JSON 값을 `Write`하고 완전한 JSON 값 문자열을 `Read`하는 계약으로 확정했다.
- 공개 `IJsonActivator<T>`는 기본 생성 규칙으로 만들 수 없는 정확한 타입의 instance를 생성한다.
- `JsonSerializerOptions`는 생성자와 `WithConverter`/`WithActivator`가 immutable snapshot을 만든다. 기존 options는 후속 등록의 영향을 받지 않는다.
- 동일 타입의 사용자 converter 또는 activator 중복 등록은 구성 시 즉시 실패한다.
- 사용자 converter가 Guid/DateTime/DateTimeOffset 기본 converter보다 우선하며, converter 조회는 기본 `TypeContract` 지원 여부 검사보다 먼저 실행된다.
- converter가 잘못된 JSON을 쓰거나 읽기 중 실패하면 제어된 serializer/converter 오류로 변환한다.
- activator가 null 또는 대상 타입과 호환되지 않는 값을 반환하면 기본 생성자로 조용히 fallback하지 않고 `ObjectCreationFailed`로 실패한다.

## 옵션 정책

- `PropertyNameCaseInsensitive`: DTO 및 생성자 인자 member matching과 overwrite matching에 ordinal case-insensitive 비교를 사용한다.
- `RejectUnknownMembers`: ordinary deserialize와 overwrite에서 매핑되지 않는 JSON member를 `UnknownMember`로 거부한다.
- `AllowNumericEnums`: 정의된 enum 값에 해당하는 범위 내 정수 입력만 허용한다. 기본값은 false다.
- case-insensitive 비교에서 두 CLR member가 충돌하면 임의 선택하지 않고 구성 오류로 실패한다.

## 기본 converter

- `Guid`: D format 문자열
- `DateTime`: invariant round-trip (`O`) 문자열
- `DateTimeOffset`: invariant round-trip (`O`) 문자열

세 기본 converter는 상태가 없고 병렬 호출에 안전하다. 사용자 converter와 activator가 같은 options instance에서 병렬 사용될 경우 그 구현의 thread safety는 등록자 책임이다.

## serializer 경로 통합

- serialize, deserialize 및 overwrite 공개 API가 같은 `JsonSerializerOptions` instance를 받는다.
- nested member와 collection element에도 같은 options가 전달된다.
- overwrite staging/conversion 단계도 converter, member matching, unknown-member 정책을 동일하게 적용한다.
- 기존 `ICustomActivator` overload는 legacy DOM mapper 대신 options adapter를 통해 typed deserialize/overwrite 경로를 사용한다.
- 더 이상 호출되지 않는 legacy `Deserializer`와 lazy static `DefaultActivator`를 제거해 이전 전역 mutable activation 상태도 남기지 않았다.
- Phase 6의 mutation 적용 단계 예외에 대한 부분 변경 가능성은 그대로 유지한다. rollback 보장은 추가하지 않았다.

## 테스트 기준

- 기본 converter와 사용자 converter 우선순위 및 중복 등록 거부
- 기본 지원 불가 root/nested 타입을 converter가 처리하는 경로
- options snapshot 간 완전한 격리
- case-insensitive matching, collision 및 strict unknown-member 정책
- numeric enum opt-in, undefined 값 및 overflow 거부
- typed activator와 legacy `ICustomActivator` 호환 경로
- 잘못된 activator 결과 검증
- overwrite의 converter/options 전달
- 동일 options instance의 병렬 serialize/deserialize

## 경계

- 전역 mutable converter/activator registry는 추가하지 않았다.
- runtime code generation과 `Expression.Compile`은 사용하지 않았다.
- Unity 값 타입 converter와 `UnityEngine` 참조는 추가하지 않았다.
- writer pooling, 전체 성능 비교, 프로파일링, Mono/IL2CPP 및 배포 문서 정리는 Phase 8에 남겼다.
- overwrite의 적용 단계 rollback은 여전히 보장하지 않는다.

## 검증 결과

- 격리된 clean-equivalent Release build 성공
- 전체 199개 테스트 통과
- legacy nullable 경고는 18개에서 9개로 감소했으며 Phase 7 새 코드의 nullable 경고 없음
- Release pack 성공
- 완전히 새로운 NuGet package cache에서 options, custom converter, case-insensitive overwrite와 numeric enum을 포함한 별도 소비 smoke 성공
- 코어 소스/프로젝트의 `UnityEngine` 참조 없음
- 전역 mutable converter/activator registry 없음
- runtime code generation, writer pooling, BenchmarkDotNet/IL2CPP 등 Phase 8 구현 혼입 없음
