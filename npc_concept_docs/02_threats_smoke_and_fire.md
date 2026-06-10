# 02. Угрозы сцены: дым и открытый огонь

## Общие правила

В сцене используются две угрозы:

- дым;
- открытый огонь.

Угрозы влияют на состояние NPC, текущую панику, видимость, слышимость сигналов и опасность маршрута.

## Дым

Дым хранится по зонам сцены.

Каждая зона имеет:

```text
SmokeLevel: 0..1
VentilationRate: 0..1
ConnectedZones
```

Дым:

- накапливается от активного огня;
- распространяется в соседние зоны;
- рассеивается через вентиляцию и открытые окна;
- снижает видимость;
- снижает слышимость голосовых сигналов;
- наносит накопительный ущерб NPC;
- повышает текущую панику NPC.

### Накопление дыма

```text
SmokeLevel += FireIntensity * SmokeEmissionRate * DeltaTime
```

`SmokeLevel` ограничивается диапазоном `0..1`.

### Распространение дыма

```text
SmokeFlow = (SmokeLevel_A - SmokeLevel_B) * ConnectionFlowRate * DeltaTime
```

После расчёта поток уменьшает `SmokeLevel_A` и увеличивает `SmokeLevel_B`.

### Рассеивание дыма

```text
SmokeLevel -= VentilationRate * DeltaTime
```

`SmokeLevel` не может быть меньше `0`.

### Влияние дыма на видимость

```text
EffectiveVisibility = BaseVisibility * (1 - SmokeLevel)
```

Чем выше `SmokeLevel`, тем ниже видимость в зоне.

### Влияние дыма на слышимость сигнала

```text
SmokeSignalMultiplier = 1 - SmokeLevel * SmokeSignalReduction
```

```text
EffectiveSignalRadius = BaseSignalRadius * SignalPower * SmokeSignalMultiplier
```

## Открытый огонь

Открытый огонь представлен очагом.

Каждый очаг имеет:

```text
FireIntensity: 0..1
SmokeEmissionRate: 0..1
CanBeExtinguished: true/false
FireRadius
```

Огонь:

- создаёт дым;
- наносит высокий ущерб рядом с очагом;
- повышает панику NPC;
- увеличивает опасность маршрута;
- может быть локально потушен, если `CanBeExtinguished = true`.

### Создание дыма огнём

```text
SmokeLevel += FireIntensity * SmokeEmissionRate * DeltaTime
```

### Тушение огня

```text
FireIntensity -= ExtinguishPower * DeltaTime
```

Когда `FireIntensity <= 0`, очаг считается потушенным и перестаёт создавать дым.

### Влияние огня на опасность маршрута

```text
PathDanger += FireIntensity * FirePathDangerWeight
```

## Окна

Окно относится к конкретной зоне.

Окно имеет:

```text
IsOpen
SmokeOutRate
OxygenInRate
```

Если окно открыто, оно снижает дым в зоне:

```text
SmokeLevel -= SmokeOutRate * DeltaTime
```

Если в этой зоне есть активный огонь, открытое окно усиливает огонь:

```text
FireIntensity += OxygenInRate * DeltaTime
```

Окно используется как рискованное действие: оно помогает снижать дым, но при активном огне может усилить очаг.

## Низкое движение

Игрок и NPC могут двигаться низко.

Для NPC низкое движение может быть отдельной командой игрока.

Низкое движение:

- снижает скорость перемещения;
- снижает воздействие дыма;
- снижает рост паники от дыма;
- не защищает от прямого воздействия огня.

Пример множителей:

```text
LowMovementSpeedMultiplier = 0.5
LowMovementSmokeDamageMultiplier = 0.5
LowMovementSmokePanicMultiplier = 0.7
```
