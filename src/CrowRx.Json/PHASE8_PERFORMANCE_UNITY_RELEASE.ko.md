# CrowRx.Json 2.x Phase 8 결과

작성일: 2026-06-21
범위: 성능 측정, Unity 검증 및 2.0.0 배포 정리

## 성능

- 1.x 저장 기준선, CrowRx.Json 2.x와 `System.Text.Json`을 flat DTO 기준으로 비교했다.
- nested DTO, 256개 list, 64개 dictionary, mutable/immutable DTO를 serialize/deserialize 각각 측정했다.
- cold contract 생성은 약 5.01 μs/3,992 B, warm cache 조회는 8.5 ns/0 B였다.
- writer의 매 호출 `StringBuilder`/cycle `HashSet` 할당이 확인되어 두 상태만 내부 pool로 재사용했다.
- pooling 후 serialize 할당은 flat 808→288 B, nested 8,248→5,328 B, list 10,824→8,096 B, dictionary 10,104→7,376 B로 감소했다.
- reader의 DOM/number/path/staging 할당은 streaming typed reader 재설계 없이는 제거하기 어려워 이번 단계에서 구조를 바꾸지 않았다.

## Unity

- 저장소의 사용자 Unity 프로젝트를 열거나 설정을 변경하지 않고 별도 임시 smoke 프로젝트를 생성했다.
- Unity 6000.5.0f1 Windows Standalone, Managed Stripping Level High에서 Mono와 IL2CPP player를 각각 build/run했다.
- mutable DTO, immutable constructor binding, converter/options, numeric enum 및 overwrite를 player 실행에서 검증했다.
- stripping 보존 규칙이 없으면 reflection constructor가 제거되는 실패를 재현했고, 명시적 `link.xml` 적용 후 Mono/IL2CPP 모두 통과했다.
- 재현 runner는 `tests/UnitySmoke/Run-UnitySmoke.ps1`이다.

## 배포 정리

- 패키지 버전을 breaking contract에 맞게 2.0.0으로 올렸다.
- 사용하지 않는 `CrowRx.Core` package dependency를 제거했다.
- README, API, 1.x migration, benchmark 결과 및 Unity stripping 안내를 추가했다.
- Unity package metadata에서 과거 CrowRx/Pool/ZLinq 의존성을 제거하고 sample을 2.x API로 갱신했다.
- legacy extension의 nullable annotation/sentinel을 정리했다.

## 경계

- 코어에 `UnityEngine` 참조나 Unity 값 타입 converter를 추가하지 않았다.
- runtime code generation과 `Expression.Compile`을 사용하지 않았다.
- overwrite apply 단계 rollback 의미는 Phase 6 계약 그대로다.
- reader를 streaming typed parser로 재작성하지 않았다.

## 최종 검증

- clean-equivalent Release build: 성공, warning 0
- 전체 test: 199개 통과
- `CrowRx.Json.2.0.0.nupkg` pack 성공 및 불필요한 CrowRx dependency 없음 확인
- 완전히 새로운 NuGet cache의 별도 소비 smoke 성공
- Unity 6000.5.0f1 Windows Mono High stripping build/run 성공
- Unity 6000.5.0f1 Windows IL2CPP High stripping build/run 성공
- 코어 `UnityEngine` 참조, runtime code generation 및 `Expression.Compile` 없음 확인
