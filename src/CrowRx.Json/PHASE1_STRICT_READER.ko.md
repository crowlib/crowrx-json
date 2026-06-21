# CrowRx.Json 2.x Phase 1 결과

작성일: 2026-06-20
범위: 오류 모델과 엄격한 JSON reader

## 구현 결과

- 문자열 인덱스 기반 `JsonReader`를 추가했다.
- 객체, 배열, 문자열, 숫자, 불리언, `null` 루트 값을 읽는다.
- 루트 값 뒤에는 JSON 공백만 허용한다.
- 객체 키는 문자열만 허용하고 빈 문자열 키는 정상 처리한다.
- 중복 객체 키를 기본적으로 거부한다.
- 문자열 escape, 4자리 Unicode escape, raw/escaped surrogate pair와 제어 문자를 검증한다.
- JSON 숫자의 부호, 정수부, 소수부와 지수부 문법을 검증한다.
- 기본 최대 깊이는 64이며 내부 reader 호출에서 조정할 수 있다.
- `JsonError`는 범주, 메시지, JSON 경로, 0-based 원본 위치, 1-based 줄/열을 제공한다.
- 기존 `TryConvertFromJson`과 overwrite 진입점은 새 reader를 사용한다.
- 기존 `MiniJson.Parser`는 제거했다. writer와 DTO mapper는 변경하지 않았다.

## 오류 범주

`UnexpectedEnd`, `UnexpectedToken`, `TrailingContent`, `ExpectedPropertyName`,
`ExpectedColon`, `ExpectedCommaOrEnd`, `InvalidEscape`, `InvalidUnicodeEscape`,
`InvalidSurrogatePair`, `UnescapedControlCharacter`, `InvalidNumber`,
`DuplicatePropertyName`, `MaximumDepthExceeded`를 구분한다.

## 테스트 기준

`JsonReaderTests`는 다음을 검증한다.

- 모든 JSON 루트 종류와 뒤쪽 공백
- 닫히지 않은 객체, 배열과 문자열
- 누락/중복 쉼표, 잘못된 객체 키와 colon
- 정상/비정상 숫자 문법
- 빈 객체 키와 중복 객체 키
- escape, Unicode와 surrogate pair
- 중첩 오류의 경로, 위치와 줄/열
- 최대 깊이

Phase 0에서 결함으로 기록했던 trailing content, malformed separator, invalid
number, empty key와 duplicate key 테스트는 Phase 1의 올바른 계약으로 갱신했다.

## 의도적으로 남긴 경계

- typed API는 아직 legacy DOM과 `Deserializer`를 사용한다. JSON 토큰에서 대상 타입으로 직접 변환하는 작업은 Phase 4 범위다.
- `JsonError`를 반환하는 새 public serializer API는 타입 변환 오류와 같은 의미 체계를 공유해야 하므로 이후 public API 단계에서 연결한다.
- DTO 멤버 변환 실패 무시, 객체에서 리스트로의 암묵 변환, generic overwrite의 기존 대상 유실은 parser 결함이 아니므로 아직 유지된다.
- 생성자 바인딩, 타입 계약, 멤버 선택과 metadata cache는 Phase 2 이후 범위다.

## nullable 기준

dead parser 제거로 nullable 경고는 29개에서 20개로 줄었다. 남은 경고는 legacy
extension API의 null sentinel과 `Deserializer`의 객체 생성/변환 흐름에 속한다.
Phase 1에서는 경고 억제를 추가하지 않았다.
