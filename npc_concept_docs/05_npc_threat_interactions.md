# 05. Взаимодействие NPC с угрозами

## Общие правила

NPC реагирует на угрозы через:

- опасность точек пространства;
- рост текущей паники;
- рост текущего ущерба;
- переход в хаотичное состояние;
- переход в недееспособное состояние.

Паника и ущерб являются динамическими состояниями NPC.

```text
CurrentPanic: 0..1
CurrentDamage: 0..1
```

## Опасность точки

Итоговая опасность точки складывается из дыма и огня.

```text
PointDanger = PointSmokeDanger + PointFireDanger
```

Значение ограничивается диапазоном `0..1`.

```text
PointDanger = Clamp01(PointDanger)
```

Если точка заблокирована огнём:

```text
IsBlocked = true
```

Заблокированная точка исключается из выбора.

## Дым и NPC

Дым определяется зоной-триггером.

Если NPC находится внутри `SmokeZone`, используется `SmokeLevel` этой зоны.

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

## Огонь и NPC

Огонь воздействует на NPC через расстояние до активного очага и интенсивность очага.

```text
FireThreatLevel = FireIntensity * FireThreatByDistance
```

Ущерб от огня:

```text
CurrentDamage += FireThreatLevel * FireDamageRate * DeltaTime
```

Рост паники от огня:

```text
CurrentPanic += FireThreatLevel * Fearfulness * FirePanicGain * DeltaTime
```

## Паника

`CurrentPanic` растёт от дыма и огня.

Паника не входит напрямую в формулу выбора точки.

Паника используется для перехода в хаотичное состояние.

```text
if CurrentPanic >= PanicCriticalThreshold:
    State = Chaotic
```

## Снижение паники

Паника снижается только рядом со спасателем.

Условие снижения:

```text
CanSeePlayer == true
DistanceToPlayer <= PanicRecoveryRadius
TrustToRescuer > 0
```

Расчёт:

```text
CurrentPanic -= BasePanicRecoveryRate * TrustToRescuer * DeltaTime
```

`CurrentPanic` ограничивается диапазоном `0..1`.

## Хаотичное состояние

В состоянии `Chaotic` NPC:

- игнорирует команды игрока;
- не учитывает `SpatialOrientation`;
- не подаёт кашель;
- выбирает случайную незаблокированную точку;
- движется к выбранной точке через NavMeshAgent;
- остаётся в этом состоянии фиксированное время.

Выбор случайной точки:

```text
CandidatePoints = NearbyVisibleProbePoints where IsBlocked == false
SelectedPoint = Random(CandidatePoints)
```

Если видимых точек нет, допускается выбор из ближайших незаблокированных точек в радиусе поиска.

После завершения хаотичного состояния:

```text
State = Idle
```

## Ущерб

`CurrentDamage` растёт от дыма и огня.

`CurrentDamage` не снижает скорость NPC напрямую.

При достижении критического порога NPC становится недееспособным.

```text
if CurrentDamage >= CriticalDamageThreshold:
    State = Incapacitated
```

## Недееспособность

В состоянии `Incapacitated` NPC:

- лежит;
- не двигается самостоятельно;
- не выполняет команды;
- не подаёт кашель;
- может быть эвакуирован только перетаскиванием.

Смерть NPC не реализуется.

## Кашель и угрозы

NPC подаёт кашель в обычных состояниях:

```text
Idle
MoveToPoint
FollowPlayer
AssistedByPlayer
```

NPC не подаёт кашель в состояниях:

```text
Chaotic
Incapacitated
Evacuated
```

Дым не снижает слышимость кашля.

Высокая паника не увеличивает частоту кашля.

## Самостоятельное поведение при угрозах

NPC постоянно работает по общей логике выбора точек, даже если игрок не появился.

Если текущая точка становится менее желательной из-за дыма или огня, NPC может начать движение к другой точке.

Если маршрут к выходу открыт и точки к выходу имеют высокую желательность, NPC может теоретически дойти до выхода самостоятельно.

Ограничение самостоятельного выхода должно задаваться уровнем, блокировками или расположением угроз.

## Эвакуация

NPC считается эвакуированным при достижении зоны эвакуации.

```text
if NPC enters EvacuationZone:
    State = Evacuated
```

Эвакуированный NPC больше не получает ущерб, не паникует и не выполняет команды.
