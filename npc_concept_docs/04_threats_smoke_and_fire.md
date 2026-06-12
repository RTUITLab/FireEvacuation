# 04. Угрозы сцены: дым и открытый огонь

## Общие правила

В сцене используются две основные угрозы:

```text
SmokeZone
FireSource
```

Дым и огонь влияют на:

- опасность точек пространства;
- рост текущей паники NPC;
- рост текущего ущерба NPC;
- блокировку точек рядом с огнём.

Дым не блокирует путь.

Дым не снижает ориентацию NPC.

Дым не снижает слышимость кашля NPC.

## Дым

Дым задаётся зонами-триггерами.

Каждая зона дыма имеет:

```text
SmokeZoneId
SmokeLevel: 0..1
```

`SmokeLevel = 0` означает отсутствие дыма.

`SmokeLevel = 1` означает максимальное задымление.

## Определение дыма для точки

Каждая `NavigationProbePoint` должна определять, находится ли она внутри зоны дыма.

Если точка находится внутри `SmokeZone`, она получает опасность дыма:

```text
PointSmokeDanger = SmokeZone.SmokeLevel * SmokeDangerWeight
```

Если точка не находится в зоне дыма:

```text
PointSmokeDanger = 0
```

## Воздействие дыма на NPC

Дым влияет на NPC только через:

- рост `CurrentDamage`;
- рост `CurrentPanic`.

Ущерб от дыма:

```text
CurrentDamage += SmokeLevel * SmokeDamageRate * DeltaTime
```

Рост паники от дыма:

```text
CurrentPanic += SmokeLevel * Fearfulness * SmokePanicGain * DeltaTime
```

Если включено низкое движение:

```text
CurrentDamage += SmokeLevel * SmokeDamageRate * LowMovementSmokeDamageMultiplier * DeltaTime
CurrentPanic += SmokeLevel * Fearfulness * SmokePanicGain * LowMovementSmokePanicMultiplier * DeltaTime
```

## Что дым не делает

Дым не должен:

- блокировать точки;
- блокировать маршруты;
- снижать `SpatialOrientation`;
- снижать слышимость кашля;
- напрямую снижать скорость движения;
- снижать видимость для проверки точек.

## Огонь

Огонь задаётся очагами `FireSource`.

Каждый очаг имеет:

```text
FireIntensity: 0..1
CanBeExtinguished: true/false
FireBlockRadius
FireDangerRadius
FireDamageRate
FirePanicGain
```

`FireIntensity = 0` означает, что очаг потушен.

`FireIntensity = 1` означает максимальную интенсивность.

## Ближний радиус огня

Если точка находится в ближнем радиусе активного огня, точка блокируется.

```text
if Distance(Point, FireSource) <= FireBlockRadius and FireIntensity > 0:
    Point.IsBlocked = true
```

Заблокированная точка не участвует в выборе NPC.

```text
PointDesirability = -Infinity
```

## Дальний радиус огня

Если точка находится в дальнем радиусе огня, но вне ближнего радиуса, она получает повышенную опасность.

```text
if Distance(Point, FireSource) <= FireDangerRadius:
    PointFireDanger = FireIntensity * FireDangerByDistance * FireDangerWeight
```

Чем ближе точка к огню, тем выше `PointFireDanger`.

Пример нормализации:

```text
FireDangerByDistance = 1 - NormalizedDistanceToFire
```

## Ущерб от огня для NPC

Если NPC находится рядом с активным огнём, он получает ущерб.

```text
CurrentDamage += FireThreatLevel * FireDamageRate * DeltaTime
```

`FireThreatLevel` рассчитывается по интенсивности огня и расстоянию до него.

## Рост паники от огня

```text
CurrentPanic += FireThreatLevel * Fearfulness * FirePanicGain * DeltaTime
```

## Тушение огня

Если `CanBeExtinguished = true`, игрок может тушить очаг.

```text
FireIntensity -= ExtinguishPower * DeltaTime
```

Когда интенсивность становится равной нулю:

```text
FireIntensity = 0
FireSource is inactive
```

После тушения:

- точки в ближнем радиусе больше не блокируются этим очагом;
- точки в дальнем радиусе больше не получают опасность от этого очага;
- NPC может выбирать ранее опасные точки, если на них не действуют другие угрозы.

## Распространение огня

В MVP распространение огня не реализуется собственной системой.

Если используется готовая библиотека распространения огня, её результат должен приводиться к набору активных `FireSource` с параметрами интенсивности и радиусов.

## Окна и вентиляция

Окна, открытие окон и закрытие окон в первой версии не реализуются.

NPC не взаимодействует с окнами.

Игрок не взаимодействует с окнами.
