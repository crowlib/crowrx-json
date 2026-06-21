# CrowRx.Json 2.x Phase 6 결과

작성일: 2026-06-21
범위: overwrite와 append

## 구현 결과

- ordinary deserialize와 분리된 JsonOverwriter 경로를 추가했다.
- 공개 JsonSerializer.TryOverwrite의 generic/Type 기반 overload를 추가했다.
- JsonOverwriteMode.Replace와 JsonOverwriteMode.Append를 명시적인 공개 모드로 추가했다.
- DTO overwrite는 JSON에 존재하는 writable 멤버만 갱신하고 누락 멤버와 알 수 없는 멤버는 유지/무시한다.
- 기존 값이 있는 mutable nested DTO, list 및 dictionary는 참조 동일성을 유지한 채 재귀적으로 overwrite한다.
- constructor-bound, get-only 및 readonly 멤버를 overwrite하려 하면 명시적으로 실패한다.
- root scalar와 nullable 값은 overwrite 대상이 아니며 새 객체 deserialize를 사용해야 한다.

## 컬렉션 정책

- list replace는 기존 instance를 Clear한 뒤 변환된 요소를 다시 채운다.
- list append는 기존 요소를 유지하고 변환된 요소를 뒤에 추가한다.
- dictionary replace는 기존 entry를 모두 지운 뒤 JSON entry로 교체한다.
- dictionary append는 새 key만 추가하며 기존 key와 충돌하면 DuplicateDictionaryKey로 실패한다.
- array append는 지원하지 않는다.
- root array replace는 기존 array instance를 유지하기 위해 길이가 같은 입력만 허용한다.
- JSON object를 list 값 열거로 암묵 변환하는 legacy 동작은 없다.

## Legacy API 마이그레이션

- custom activator가 없는 TryConvertFromJsonOverwrite overload를 typed overwrite 경로에 연결했다.
- generic overwrite가 기존 target 대신 null 임시 객체를 사용하던 결함을 수정했다.
- legacy isAppend는 각각 JsonOverwriteMode.Replace와 JsonOverwriteMode.Append로 연결된다.
- custom activator가 전달된 경로는 Phase 7까지 기존 Deserializer를 유지한다.

## 실패와 객체 상태 계약

overwrite는 두 단계로 실행한다.

1. 전체 JSON tree의 타입 변환, 중복 key 및 overwrite 가능 여부를 검사하고 mutation을 staging한다.
2. staging이 모두 성공한 뒤 대상 객체에 mutation을 순서대로 적용한다.

따라서 parsing, numeric/type conversion, duplicate key와 지원 여부 실패는 mutation 전에 발생하며 대상은 변경되지 않는다.

다만 사용자 property setter나 custom mutable collection의 Add/Clear가 적용 단계에서 예외를 던질 수 있다. 이 경우 앞서 적용된 mutation은 되돌리지 않으며 MutationFailed를 반환한다. Phase 6은 완전한 transactional rollback을 보장하지 않는다.

## 비용 측정

BenchmarkDotNet ShortRun으로 동일한 flat DTO 입력을 새 객체 deserialize와 staged overwrite로 비교했다.

| Method | Mean | Allocated | 상대 시간 | 상대 할당 |
| --- | ---: | ---: | ---: | ---: |
| DeserializeReplacement | 996.1 ns | 1.89 KB | 1.00 | 1.00 |
| StagedOverwrite | 1,285.2 ns | 2.51 KB | 1.29 | 1.33 |

현재 staging은 새 객체 deserialize 대비 약 29% 느리고 33% 더 할당한다. 이 비용과 rollback을 위한 추가 snapshot 비용을 고려해 완전 transactional overwrite는 이번 단계에서 도입하지 않았다.

## 테스트 기준

OverwriteTests와 갱신한 legacy defect 회귀 테스트는 다음을 검증한다.

- DTO 누락 멤버 보존과 unknown member 무시
- nested DTO identity와 누락 nested member 보존
- list replace/append
- dictionary replace/append와 duplicate key
- array 동일 길이 replace와 append 거부
- conversion 실패 전 mutation 없음
- setter 예외 뒤 부분 mutation 가능성
- constructor-bound immutable member 거부
- JSON object에서 list로의 암묵 변환 거부
- legacy generic overwrite가 기존 target을 사용함

Phase 0~5 테스트와 Phase 6 테스트를 합쳐 총 188개 테스트가 통과한다.

## 검증 결과

- 격리된 clean-equivalent Release build 성공
- legacy nullable 경고는 20개에서 18개로 감소, Phase 6 코드의 새 nullable 경고 없음
- Release test 188개 통과
- Release pack 성공
- 완전히 새로운 NuGet package cache에서 overwrite를 포함한 별도 package smoke test 성공

## 의도적으로 남긴 경계

- converter, custom activator의 typed 통합, public strict mode와 immutable options는 Phase 7에 남겼다.
- 완전 transactional rollback은 성능/할당 예산이 승인되기 전까지 보장하지 않는다.
- polymorphic metadata, UnityEngine.Object, runtime code generation과 Expression.Compile은 추가하지 않았다.
