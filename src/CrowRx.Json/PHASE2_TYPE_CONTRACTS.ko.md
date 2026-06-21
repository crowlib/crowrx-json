# CrowRx.Json 2.x Phase 2 결과

작성일: 2026-06-20
범위: 타입 계약과 메타데이터 캐시

## 구현 결과

- `TypeContractKind`를 scalar, enum, nullable, collection, dictionary, DTO로 분리했다.
- `TypeContract`가 대상 타입, 요소/키/값/underlying 타입, DTO 멤버와 명시적 생성자를 보관한다.
- `MemberContract` 하나가 JSON 이름, 멤버 타입과 직렬화/역직렬화 가능 여부를 함께 보관한다.
- `ConcurrentDictionary<Type, TypeContract>` 기반 `TypeContractCache`를 추가했다.
- cache miss 빌드를 lock 안에서 한 번만 수행해 동시 최초 접근에서도 리플렉션 탐색이 중복되지 않는다.
- DTO 멤버 순서는 기반 타입부터 파생 타입, 같은 선언 타입에서는 metadata token 순서로 결정한다.
- override property는 가장 파생된 선언 하나만 계약에 포함한다.
- 상속/이름 재정의 결과 같은 JSON 이름이 생기면 계약 생성에 실패한다.

## 공개 특성 API

- `[JsonInclude]`: 기본 대상이 아닌 non-public 또는 읽기 전용 멤버를 명시적으로 포함
- `[JsonIgnore]`: 기본/명시 포함 여부와 관계없이 제외
- `[JsonPropertyName("name")]`: JSON 이름 재정의; 빈 문자열 이름도 허용
- `[JsonConstructor]`: 향후 생성자 바인딩에 사용할 생성자를 명시

한 타입에 `[JsonConstructor]` 생성자가 둘 이상이면 계약 오류다. 실제 생성자 우선순위와 매개변수 바인딩은 Phase 5에서 구현한다.

## 기본 멤버 선택

- public mutable instance field는 양방향 대상이다.
- public readonly field는 기본 제외한다.
- public getter/setter property는 각 accessor 공개 여부에 따라 방향별로 참여한다.
- setter가 없는 계산/read-only property는 기본 제외한다.
- `[JsonInclude]` read-only 멤버는 직렬화 전용으로 계약에 들어갈 수 있다.
- static, const, indexer, event/delegate 타입 멤버는 항상 제외한다.
- `[JsonIgnore]`가 `[JsonInclude]`보다 우선한다.
- 명시적으로 포함한 기반 타입 private 멤버도 계약에 포함한다.

## 지원 타입 검증

계약 생성 시 다음 타입을 거부한다.

- open generic, pointer, by-ref, void
- interface, abstract class, delegate, tuple
- 다차원 배열
- 문자열 이외 키의 dictionary
- `IList<T>`/`IDictionary<string, TValue>`가 아닌 collection shape
- 지원 불가 요소 타입을 가진 collection
- 지원 불가 타입의 선택된 DTO 멤버

커스텀 converter가 도입되면 converter가 이러한 기본 계약 판정보다 우선하도록 Phase 7에서 연결한다.

## 테스트 기준

`TypeContractTests`는 타입 분류, 요소/키/값 타입, 양방향 멤버 플래그,
readonly/static/indexer/delegate 제외, include/ignore/name/constructor 특성,
상속 private 멤버, override 중복 제거, 이름 충돌, 지원 불가 타입,
동일 인스턴스 cache 재사용과 병렬 최초 접근을 검증한다.

## 의도적으로 남긴 경계

- legacy writer와 deserializer는 아직 `TypeContract`를 사용하지 않는다.
- Phase 3 writer와 Phase 4 typed reader가 같은 cached contract를 소비하도록 연결한다.
- constructor-bound read-only 멤버의 최종 참여 여부는 Phase 5에서 확장한다.
- converter/activator가 지원 불가 타입 판정을 재정의하는 기능은 Phase 7 범위다.
- 순환 객체 검출과 최대 직렬화 깊이는 Phase 3 범위다.

nullable 경고는 Phase 1과 동일한 legacy API/mapper 경고 20개이며, Phase 2 코드에서는 새 경고를 추가하지 않았다.
