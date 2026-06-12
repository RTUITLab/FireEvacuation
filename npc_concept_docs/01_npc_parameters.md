# 01. Параметры и состояния NPC-пострадавших

## Общие правила

Все постоянные параметры NPC имеют диапазон `0..1`.

Значение `0` соответствует минимальному влиянию параметра.

Значение `1` соответствует максимальному влиянию параметра.

Параметры используются для расчёта:

- самостоятельного выбора цели NPC;
- реакции на команды игрока;
- движения по NavMesh;
- сопротивления при физическом ведении;
- роста паники;
- роста ущерба;
- перехода в недееспособное состояние.

## Постоянные параметры NPC

```text
MoveSpeed
CommandReactionDelay
FollowDistance
Weight
MobilityLimit
DangerAvoidance
TrustToRescuer
SpatialOrientation
SignalPower
SignalFrequency
Fearfulness
ChaoticBehaviorDuration
```

## 1. `MoveSpeed` — скорость движения

**Смысл параметра:** определяет базовую скорость самостоятельного движения NPC.

**Влияние:** чем выше значение, тем быстрее NPC движется к выбранной точке, к игроку или по команде.



## 2. `CommandReactionDelay` — задержка реакции на команду

**Смысл параметра:** определяет задержку перед выполнением команды игрока.



Задержка применяется ко всем командам:

```text
FollowPlayer
Stop
GoThere
LowMovement
```

Повторная команда добавляется в очередь и активируется спустя задержку.

## 3. `FollowDistance` — максимальная дистанция следования

**Смысл параметра:** определяет максимальную дистанцию, на которой NPC продолжает выполнять команду следования за игроком.



Если после команды `FollowPlayer` игрок удаляется от NPC дальше `MaxFollowDistance`, NPC прекращает следование и возвращается к самостоятельной логике выбора точки.

`FollowDistance` не заставляет NPC ускоряться. NPC движется только с фактической скоростью, рассчитанной по его параметрам и состояниям.

## 4. `Weight` — вес NPC

**Смысл параметра:** определяет сложность физического ведения или перетаскивания NPC.

Влияет на:

- снижение скорости игрока при удержании NPC;
- силу сопротивления при конфликте направлений;
- сложность перетаскивания недееспособного NPC.

Пример множителя при физическом ведении:

```text
AssistSpeedMultiplier = 1 - Weight * AssistWeightPenalty
```

## 5. `MobilityLimit` — ограничение мобильности

Данный параметр решено удалить из сборки

## 6. `DangerAvoidance` — избегание опасности

**Смысл параметра:** определяет, насколько NPC избегает опасных точек и маршрутов.

Опасность точки учитывается при расчёте желательности:

```text
DangerPenalty = PointDanger * DangerAvoidance * DangerWeight
```

Чем выше `DangerAvoidance`, тем сильнее NPC избегает зон рядом с огнём и зон с высоким уровнем дыма.

## 7. `TrustToRescuer` — доверие к спасателю

**Смысл параметра:** определяет, насколько присутствие спасателя повышает приоритет точек рядом с ним и снижает панику NPC.

Точки рядом с видимым игроком получают бонус:

```text
RescuerBonus = RescuerProximity * TrustToRescuer * RescuerWeight
```

Если доверие низкое, игрок слабо влияет на выбор точки.

Если доверие высокое, точки рядом со спасателем становятся значительно более желательными.

`TrustToRescuer` также влияет на скорость снижения паники рядом с игроком:

```text
PanicRecovery = BasePanicRecoveryRate * TrustToRescuer * DeltaTime
```

## 8. `SpatialOrientation` — ориентация в пространстве

**Смысл параметра:** определяет, насколько NPC учитывает близость точки к выходу.

Если `SpatialOrientation = 0`, близость к выходу не влияет на выбор точки.

Если `SpatialOrientation = 1`, близость к выходу влияет максимально.

Пример вклада ориентации:

```text
OrientationScore = ExitProximity * SpatialOrientation * OrientationWeight
```

`ExitProximity` рассчитывается один раз при запуске сцены по расстоянию от точки до ближайшего выхода.

Дым не снижает `SpatialOrientation`.

В хаотичном состоянии `SpatialOrientation` полностью отключается.

## 9. `SignalPower` — сила кашля

**Смысл параметра:** определяет радиус слышимости кашля NPC.

```text
EffectiveSignalRadius = BaseSignalRadius * SignalPower
```

Дым не снижает слышимость кашля.

## 10. `SignalFrequency` — частота кашля

**Смысл параметра:** определяет частоту подачи звукового сигнала в виде кашля.

```text
SignalInterval = MaxSignalInterval - SignalFrequency * (MaxSignalInterval - MinSignalInterval)
```

Высокая паника не увеличивает частоту кашля.

В состояниях `Chaotic` и `Incapacitated` кашель не подаётся.

## 11. `Fearfulness` — пугливость

**Смысл параметра:** определяет рост текущей паники при воздействии дыма и огня.

```text
CurrentPanic += ThreatLevel * Fearfulness * PanicGainMultiplier * DeltaTime
```

`Fearfulness` является постоянной характеристикой NPC.

`CurrentPanic` является динамическим состоянием.

## 12. `ChaoticBehaviorDuration` — длительность хаотичного поведения

**Смысл параметра:** определяет, как долго NPC находится в хаотичном состоянии после достижения критической паники.

```text
ChaoticDuration = MinChaoticDuration + ChaoticBehaviorDuration * MaxExtraChaoticDuration
```

В хаотичном состоянии NPC:

- не выполняет команды;
- не учитывает ориентацию в пространстве;
- выбирает случайную незаблокированную точку;
- не подаёт кашель.

## Динамические состояния NPC

```text
CurrentPanic
CurrentDamage
IsLowMovement
CurrentState
CurrentZoneId
CurrentTargetPosition
LastCommandType
LastCommandTarget
IsBeingAssisted
IsVisibleToPlayer
CanSeePlayer
```

## `CurrentPanic`

`CurrentPanic` имеет диапазон `0..1`.

Паника растёт от дыма и огня.

```text
CurrentPanic += SmokeLevel * Fearfulness * SmokePanicGain * DeltaTime
CurrentPanic += FireThreatLevel * Fearfulness * FirePanicGain * DeltaTime
```

Если паника достигает критического порога, NPC переходит в `Chaotic`.

```text
if CurrentPanic >= PanicCriticalThreshold:
    State = Chaotic
```

Паника снижается только рядом со спасателем и только с учётом доверия:

```text
if CanSeePlayer and DistanceToPlayer <= PanicRecoveryRadius:
    CurrentPanic -= BasePanicRecoveryRate * TrustToRescuer * DeltaTime
```

## `CurrentDamage`

`CurrentDamage` имеет диапазон `0..1`.

Ущерб растёт от дыма и огня.

```text
CurrentDamage += SmokeLevel * SmokeDamageRate * DeltaTime
CurrentDamage += FireThreatLevel * FireDamageRate * DeltaTime
```

При достижении критического значения NPC становится недееспособным:

```text
if CurrentDamage >= CriticalDamageThreshold:
    State = Incapacitated
```

В состоянии `Incapacitated` NPC лежит, не двигается самостоятельно, не подаёт кашель и может быть эвакуирован только перетаскиванием.

## `IsLowMovement`

`IsLowMovement` является модификатором, а не отдельным состоянием.

Низкое движение снижает скорость, но уменьшает воздействие дыма:

```text
EffectiveSpeed *= LowMovementSpeedMultiplier
SmokeDamage *= LowMovementSmokeDamageMultiplier
SmokePanicGain *= LowMovementSmokePanicMultiplier
```

Механика низкого движения относится к последнему приоритету MVP.
