# 03. Взаимодействие NPC с угрозами

## Общие правила

NPC реагирует на угрозы через параметры и динамические состояния.

Постоянные параметры NPC:

- `MoveSpeed`;
- `CommandReactionDelay`;
- `FollowDistance`;
- `Weight`;
- `MobilityLimit`;
- `DangerAvoidance`;
- `TrustToRescuer`;
- `SignalPower`;
- `SignalFrequency`;
- `Fearfulness`;
- `ChaoticBehaviorDuration`;
- `HidingTendency`;
- `BarricadeTendency`.

Динамические состояния NPC:

- `CurrentPanic`;
- `CurrentDamage`;
- `IsInLowMovement`;
- `IsInChaoticBehavior`;
- `CurrentZoneId`.

## Дым и NPC

Дым наносит накопительный ущерб NPC:

```text
NPCDamage += SmokeLevel * SmokeDamageRate * DeltaTime
```

Дым повышает текущую панику NPC с учётом пугливости:

```text
CurrentPanic += SmokeLevel * Fearfulness * SmokePanicGain * DeltaTime
```

Дым снижает видимость:

```text
EffectiveVisibility = BaseVisibility * (1 - SmokeLevel)
```

Дым снижает радиус слышимости голосового сигнала:

```text
EffectiveSignalRadius = BaseSignalRadius * SignalPower * (1 - SmokeLevel * SmokeSignalReduction)
```

`SignalFrequency` определяет, как часто NPC подаёт сигнал:

```text
SignalInterval = MaxSignalInterval - SignalFrequency * (MaxSignalInterval - MinSignalInterval)
```

`MobilityLimit` снижает скорость выхода из задымлённой зоны:

```text
EffectiveSpeed = ActualSpeed * (1 - MobilityLimit * MobilityPenalty)
```

`HidingTendency` влияет на выбор безопасной точки в задымлённой зоне или рядом с ней.

## Огонь и NPC

Огонь наносит высокий ущерб рядом с очагом:

```text
NPCDamage += FireIntensity * FireDamageRate * DeltaTime
```

Огонь повышает текущую панику NPC с учётом пугливости:

```text
CurrentPanic += FireIntensity * Fearfulness * FirePanicGain * DeltaTime
```

Огонь увеличивает опасность маршрута:

```text
PathDanger += FireIntensity * FirePathDangerWeight
```

## Доверие и избегание опасности

NPC оценивает не только объективную опасность маршрута, но и субъективную опасность.

```text
SubjectivePathDanger = PathDanger * DangerAvoidance - TrustToRescuer * RescuerSafetyBonus
```

Поведение выбирается по `SubjectivePathDanger`.

| `SubjectivePathDanger` | Поведение NPC |
|---:|---|
| `0.0..0.2` | NPC идёт за игроком |
| `0.2..0.5` | NPC идёт медленно или с остановками |
| `0.5..0.8` | NPC сопротивляется и требует повторной команды |
| `0.8..1.0` | NPC отказывается идти через маршрут |

Значение `SubjectivePathDanger` приводится к диапазону `0..1`.

## Паника

`CurrentPanic` — динамическое состояние NPC.

Оно растёт от дыма и огня с учётом `Fearfulness`.

```text
CurrentPanic += ThreatLevel * Fearfulness * PanicGainMultiplier * DeltaTime
```

Если `CurrentPanic` достигает критического порога, NPC входит в хаотичное состояние.

```text
if CurrentPanic >= PanicCriticalThreshold:
    StartChaoticBehavior()
```

Длительность хаотичного состояния:

```text
ChaoticDuration = MinChaoticDuration + ChaoticBehaviorDuration * MaxExtraChaoticDuration
```

После завершения хаотичного состояния паника постепенно снижается.

```text
CurrentPanic -= PanicRecoveryRate * DeltaTime
```

`CurrentPanic` ограничивается диапазоном `0..1`.

## Поведение до появления игрока

До контакта со спасателем NPC выбирает точки на сцене на основе субъективной опасности.

Каждая точка имеет:

```text
PointDanger: 0..1
```

Оценка точки:

```text
PointScore = PointDanger * DangerAvoidance
```

При высокой `HidingTendency` NPC выбирает точку с минимальным `PointScore`.

При низкой `HidingTendency` NPC может перемещаться между доступными точками, если это позволяет найти более безопасную позицию.

Если точка является закрытой или труднодоступной, `BarricadeTendency` повышает вероятность её выбора.

```text
PointScore -= BarricadeTendency * PointBarricadeValue
```

## Появление игрока

Когда NPC видит игрока, область рядом с игроком получает бонус безопасности.

```text
RescuerPointScore = PointDanger - TrustToRescuer * RescuerSafetyBonus
```

NPC может начать движение к игроку, если путь к нему имеет допустимую субъективную опасность.

```text
CanMoveToRescuer = SubjectivePathDanger < FollowAllowedDangerThreshold
```

Если `CanMoveToRescuer = true`, NPC может начать движение к игроку или выполнить команду следования после задержки `CommandReactionDelay`.

Если `CanMoveToRescuer = false`, NPC остаётся в текущей точке, сопротивляется маршруту или ждёт приближения игрока.
