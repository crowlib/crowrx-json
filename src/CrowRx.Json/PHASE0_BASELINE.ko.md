# CrowRx.Json 2.x Phase 0 기준선

작성일: 2026-06-20
대상 구현: CrowRx.Json 1.0.2

## 1. 프로젝트 기준선

- 코어 라이브러리: `netstandard2.1`, C# 9, nullable 활성화
- 테스트: `CrowRx.Json.Tests`, `net8.0`, xUnit
- 벤치마크: `CrowRx.Json.Benchmarks`, `net8.0`, BenchmarkDotNet
- 패키지 소비 smoke test: `tests/CrowRx.Json.PackageSmoke`
- Phase 0에서는 제품 코드를 변경하지 않았다.

`LegacyBehaviorTests`는 유지할 1.x 사용법을, `KnownDefectBaselineTests`는 현재 결함을 재현한다. 결함 기준 테스트는 2.x에서 올바른 실패 계약이 구현되는 단계에 새 계약 테스트로 교체해야 한다.

## 2. 현재 공개 API 분류

| API | 2.x 방침 | 비고 |
| --- | --- | --- |
| `object.ToJson()` | 유지 | 새 serializer API를 호출하는 호환 래퍼로 유지 |
| `string.ConvertFromJson<T>()` | 유지 | 실패 시 예외를 제공하는 새 API와 의미를 맞추고 마이그레이션 안내 추가 |
| `string.ConvertFromJson(string, Type)` 계열 | 유지 | non-generic 호환 경로 |
| `string.TryConvertFromJson<T>()` | 유지 | 실패 이유를 버리지 않는 새 API로 이동하도록 안내 |
| `string.TryConvertFromJson(string, Type, ...)` 계열 | 유지 | activator 포함 기존 호출부 호환 |
| `TryConvertFromJsonOverwrite` 계열 | 유지 후 정리 | overwrite/append 의미를 분리하고 기존 overload는 호환 래퍼 또는 `Obsolete` 후보 |
| `ICustomActivator` | 일단 유지 | 새 options 기반 activator 계약으로 연결; 전역 상태는 추가하지 않음 |
| 공개 `Serializer` 타입 | 호환성 검토 | 현재 public 메서드가 없는 표면이며 새 `JsonSerializer` 도입 시 이름/이관 정책 필요 |

## 3. 2.x에서 중단할 1.x 암묵 동작

| 현재 동작 | 확인된 기준 | 2.x 목표 |
| --- | --- | --- |
| 뒤쪽 쓰레기 문자를 무시하고 성공 | `Parser_AcceptsTrailingGarbage` | 뒤쪽 공백 외 입력이 남으면 실패 |
| 배열의 누락/중복 쉼표 허용 | `Parser_AcceptsMalformedArraySeparators` | JSON 문법 오류로 실패 |
| 잘못된 숫자를 `0`으로 변환 | `Parser_ConvertsInvalidNumberToZero` | 숫자 문법/범위 오류로 실패 |
| 빈 객체 키 거부 | `Parser_RejectsEmptyObjectKey` | 빈 문자열 키 허용 |
| 중복 객체 키는 마지막 값 사용 | `Parser_DuplicateObjectKeyUsesLastValue` | 기본 정책으로 중복 키 거부 |
| 루트 `null`과 파싱 실패를 같은 실패로 처리 | `RootNull_IsReportedAsFailureForNullableReference` | 대상 타입의 null 허용 여부에 따라 구분 |
| DTO 멤버 변환 실패를 무시하고 성공 | `DtoMemberConversionFailure_IsSilentlyIgnored` | 전체 작업 실패와 경로 제공 |
| 리스트 요소 변환 실패를 건너뛰고 성공 | `ListElementConversionFailure_IsSilentlySkipped` | 전체 작업 실패 |
| JSON 객체 값을 리스트로 암묵 변환 | `JsonObject_IsImplicitlyConvertedToListValues` | 토큰/대상 컬렉션 종류 불일치로 실패 |
| generic overwrite가 기존 대상을 버리고 새 객체 생성 | `GenericOverwrite_DoesNotUseExistingTarget` | 명시한 overwrite 계약에 따라 기존 대상 갱신 |
| private 필드를 기본 포함 | `FieldDto_RoundTripsPublicAndPrivateFields` | 명시적 include 특성이 있을 때만 포함 |
| 비문자열 딕셔너리 키 자동 변환 | 코드 기준 | 문자열 키 외에는 명시적 converter 요구 |
| 임의 타입을 `Activator.CreateInstance`로 생성 | 코드 기준 | 결정적인 생성자 바인딩 또는 등록 activator 요구 |

## 4. nullable 경고 분류

기준 `dotnet build --no-incremental` 결과는 오류 0개, nullable 경고 29개다.

| 파일 | 개수 | 주된 원인 | 처리 단계 |
| --- | ---: | --- | --- |
| `Extension.cs` | 13 | 실패를 `null/default`로 표현하지만 public signature와 activator 매개변수가 non-nullable | 새 결과/오류 모델 도입 시 API annotation 정리 |
| `Deserializer.cs` | 7 | null 초기 객체, 실패 sentinel, nullable collection cast를 non-nullable 내부 계약에 전달 | typed 변환/생성 경로에서 nullable flow를 명시 |
| `MiniJson.cs` | 9 | parser 실패와 JSON null이 동일한 null, parser 내부 컬렉션 지연 생성, dispose 후 null 대입 | Phase 1 reader가 성공/오류를 분리하며 제거 |

진단 코드별 개수는 `CS8625` 16개, `CS8603` 7개, `CS8600` 3개, `CS8601` 2개, `CS8604` 1개다. 경고 억제나 null-forgiving 연산자로 일괄 제거하지 않는다.

## 5. CrowRx 의존성 분석

| 의존성 | 실제 사용 | 결정 |
| --- | --- | --- |
| `CrowRx.Core` | `Deserializer.cs`의 `Log.Info` 1곳과 `Log.Error` 3곳 | 제거 가능. Phase 1 이후 구조화된 `JsonError`가 대체하므로 코어 패키지 의존성을 유지할 이유가 없음 |
| `CrowRx.Pool` | `MiniJson.cs`에서 `StringBuilderPool.Get()` 4곳과 `IPooledStringBuilder` 사용 | 제거 가능성이 높음. Phase 1 reader는 문자열 슬라이스/인덱스를 사용하고 writer 버퍼 풀링은 벤치마크로 승인할 항목이므로 2.x 기준 구현의 필수 의존성으로 두지 않음 |

Phase 0에서는 1.x 동작과 벤치마크를 보존하기 위해 두 PackageReference를 제거하지 않는다. 해당 코드를 교체하는 phase에서 각각 제거하고 package smoke test로 전이 의존성이 사라졌는지 확인한다.

## 6. 검증 명령

```powershell
rtk dotnet build src/CrowRx.Json/CrowRx.Json.slnx --configuration Release
rtk dotnet test src/CrowRx.Json/CrowRx.Json.slnx --configuration Release --no-build
rtk dotnet pack src/CrowRx.Json/CrowRx.Json.csproj --configuration Release --output artifacts/packages --no-build
dotnet restore tests/CrowRx.Json.PackageSmoke/CrowRx.Json.PackageSmoke.csproj --packages artifacts/smoke-packages
rtk dotnet run --project tests/CrowRx.Json.PackageSmoke/CrowRx.Json.PackageSmoke.csproj --configuration Release --no-restore
```

패키지 smoke 프로젝트는 ProjectReference가 아니라 `CrowRx.Json` 1.0.2 PackageReference만 사용한다.
