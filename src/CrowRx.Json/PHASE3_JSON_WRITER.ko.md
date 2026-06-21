# CrowRx.Json 2.x Phase 3 결과

작성일: 2026-06-20
범위: JSON writer와 직렬화

## 구현 결과

- `TypeContractCache.Shared`의 cached `TypeContract`를 사용하는 `JsonWriter`를 추가했다.
- `Serializer.ToJson`과 기존 `object.ToJson()` 호환 경로를 새 writer에 연결했다.
- bool, string, char, 모든 정수 타입, float, double, decimal, enum, nullable, `Guid`, `DateTime`, `DateTimeOffset`을 직렬화한다.
- 숫자와 날짜/시간은 문화권에 영향받지 않는 표현을 사용하고, enum은 정의된 이름으로 기록한다.
- JSON escape, 제어 문자, Unicode 및 surrogate pair를 검증하며 unpaired surrogate를 거부한다.
- 1차원 배열, `IList<T>` shape와 `IDictionary<string, TValue>` shape를 직렬화한다.
- dictionary key는 ordinal 순서로 정렬해 입력 삽입 순서와 무관한 결정적 출력을 만든다.
- DTO 멤버는 `TypeContract.Members` 순서로 순회하며 `CanSerialize`과 `JsonName`을 실제 출력에 사용한다.

## 실패 정책과 깊이

- `NaN`, 양의 무한대와 음의 무한대는 JSON 숫자로 기록하지 않고 명시적으로 실패한다.
- 이름이 정의되지 않은 enum 값은 숫자로 암묵 출력하지 않고 실패한다.
- 문자열이 아닌 dictionary key, 지원하지 않는 collection/type shape는 계약 오류로 실패한다.
- 기본 최대 깊이는 reader와 같은 64다. 내부 writer 생성 시 더 작은 값을 지정해 경계 테스트를 수행할 수 있다.
- 참조 추적은 현재 재귀 경로에만 적용한다. 현재 경로에서 다시 만난 객체는 cycle 오류지만, 경로에서 빠진 동일 객체는 반복 값으로 다시 직렬화한다.
- 참조 동일성이나 `$id`/`$ref` 메타데이터는 보존하지 않는다.

## Legacy writer 정리

- 새 writer 연결과 회귀 테스트 후 `MiniJson`의 reflection 기반 serializer를 제거했다.
- `MiniJson.Json`은 Phase 1 strict reader를 legacy deserialization API에 연결하는 얇은 호환 wrapper만 유지한다.
- legacy writer에서만 사용하던 `CrowRx.Pool` package 의존성을 제거했다.
- 기존 private-field round-trip 회귀 fixture는 Phase 2 계약에 맞게 `[JsonInclude]`로 의도를 명시했다.

## 테스트 기준

`JsonWriterTests`는 다음을 검증한다.

- 모든 scalar 분류와 정수 경계값
- nullable, null root, enum 이름, `Guid`, ISO 8601 날짜/시간
- string escape, Unicode 왕복, surrogate pair와 unpaired surrogate 실패
- 배열, list, string-keyed dictionary와 dictionary 결정적 순서
- nested DTO, 계약 멤버 순서, `JsonName`, `CanSerialize`
- 현재 경로 cycle 실패와 cycle이 아닌 반복 참조 허용
- 최대 깊이, 비유한 부동소수점, unnamed enum과 지원 불가 타입 실패
- 출력 결과의 `System.Text.Json` 표준 parser 소비

Phase 0~2의 기존 90개 테스트와 Phase 3의 31개 테스트를 합쳐 총 121개 테스트가 통과한다.

## 검증 결과

- clean-equivalent Release build 성공
- 기존 legacy nullable 경고 20개 유지, Phase 3 코드의 새 nullable 경고 없음
- Release test 121개 통과
- Release pack 성공
- 완전히 새로운 NuGet package cache를 사용한 별도 package smoke test 성공

## 의도적으로 남긴 경계

- typed deserialization과 legacy `Deserializer` 재작성은 Phase 4에 남겼다.
- 생성자 바인딩과 immutable DTO/record 읽기는 Phase 5에 남겼다.
- overwrite/append 의미 변경은 Phase 6에 남겼다.
- converter/options 전역 구조와 custom activator 통합은 Phase 7에 남겼다.
- `UnityEngine.Object` 지원, runtime code generation과 `Expression.Compile`은 추가하지 않았다.
