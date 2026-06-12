# 06. Физическое взаимодействие игрока и NPC

## Назначение

Физическое взаимодействие описывает ситуацию, когда игрок касается NPC и удерживает его за часть тела.

Это не команда игрока.

Это состояние взаимодействия, возникающее из фактического контакта.

## Основные правила

Если игрок удерживает NPC, NPC переходит в состояние:

```text
AssistedByPlayer
```

В этом состоянии:

- NPC продолжает иметь собственное желательное направление;
- игрок может вести NPC;
- движение игрока замедляется из-за веса NPC, мобильности NPC и сопротивления NPC;
- сопротивление возникает, если направление движения игрока противоречит желательному направлению NPC;
- сила сопротивления влияет на вибрацию контроллера;
- NPC может вырваться, если расстояние между игроком и NPC становится слишком большим.

## Условие входа в `AssistedByPlayer`

Состояние включается, если:

```text
PlayerTouchesNPC == true
PlayerHoldsNPC == true
NPC.State != Incapacitated
NPC.State != Evacuated
```

Если NPC находится в `Incapacitated`, он не переходит в `AssistedByPlayer`. Перетаскивание недееспособного NPC обрабатывается отдельно, при сохранении состояния `Incapacitated`.

## Желательное направление NPC

NPC всегда имеет собственную наиболее желательную точку, рассчитанную общей формулой выбора.

Желательное направление NPC:

```text
NPCDesiredDirection = Normalize(NPCDesiredPoint.Position - NPC.Position)
```

Если NPC не имеет желательной точки, направление считается нулевым.

## Направление движения игрока

Направление движения игрока:

```text
PlayerMoveDirection = Normalize(PlayerCurrentPosition - PlayerPreviousPosition)
```

Если игрок не движется, сопротивление не должно резко увеличиваться.

## Конфликт направлений

Конфликт направлений показывает, насколько игрок ведёт NPC не туда, куда NPC хочет двигаться.

```text
DirectionConflict = (1 - Dot(PlayerMoveDirection, NPCDesiredDirection)) / 2
```

Диапазон:

```text
0..1
```

`0` — игрок движется в том же направлении, куда хочет двигаться NPC.

`1` — игрок движется в противоположном направлении.

## Сила желания NPC

Сила желания NPC зависит от разницы между лучшей точкой и текущей позицией.

```text
NPCDesireStrength = Clamp01(BestPointDesirability - CurrentPositionDesirability)
```

Если NPC почти не заинтересован двигаться, сопротивление низкое.

Если NPC явно хочет двигаться в другую сторону, сопротивление выше.

## Сопротивление NPC

Сопротивление рассчитывается через конфликт направлений, силу желания NPC и доверие к спасателю.

```text
Resistance = DirectionConflict * NPCDesireStrength * (1 - TrustToRescuer)
```

Значение ограничивается диапазоном `0..1`.

```text
Resistance = Clamp01(Resistance)
```

`SpatialOrientation` не входит в сопротивление отдельным членом. Она уже влияет на желательность точек, а через них — на `NPCDesiredDirection` и `NPCDesireStrength`.

## Снижение скорости игрока

При удержании NPC скорость игрока снижается.

Факторы снижения:

- вес NPC;
- ограничение мобильности NPC;
- сопротивление NPC.

Пример расчёта:

```text
PlayerAssistSpeedMultiplier =
    1
    - Weight * AssistWeightPenalty
    - MobilityLimit * AssistMobilityPenalty
    - Resistance * AssistResistancePenalty
```

Множитель ограничивается минимальным значением:

```text
PlayerAssistSpeedMultiplier = Max(PlayerAssistSpeedMultiplier, MinAssistSpeedMultiplier)
```

## Вибрация контроллера

Вибрация контроллера отражает физическое сопротивление NPC.

Даже при нормальном ведении присутствует слабая базовая вибрация.

```text
ControllerVibration = MinVibration + Resistance * VibrationMultiplier
```

Значение ограничивается диапазоном `0..1`.

```text
ControllerVibration = Clamp01(ControllerVibration)
```

Пример:

```text
MinVibration = 0.1
Resistance = 0.0 -> ControllerVibration = 0.1
Resistance = 0.5 -> средняя вибрация
Resistance = 1.0 -> максимальная вибрация
```

## Разрыв удержания

NPC может вырваться из удержания, если игрок отошёл слишком далеко.

```text
if Distance(PlayerHoldPoint, NPCHoldPoint) > AssistBreakDistance:
    BreakAssist()
```

После разрыва:

```text
State = Idle
```

NPC возвращается к самостоятельному выбору точки.

## Переход в панику во время удержания

Если во время физического ведения паника достигает критического порога, NPC переходит в `Chaotic`.

```text
if CurrentPanic >= PanicCriticalThreshold:
    State = Chaotic
```

В `Chaotic` удержание должно быть разорвано.

## Переход в недееспособность во время удержания

Если во время физического ведения ущерб достигает критического порога, NPC переходит в `Incapacitated`.

```text
if CurrentDamage >= CriticalDamageThreshold:
    State = Incapacitated
```

В этом случае обычное ведение прекращается.

Дальнейшая эвакуация выполняется как перетаскивание недееспособного NPC.

## Настраиваемые параметры

Все параметры физического взаимодействия должны быть доступны в Unity Inspector:

```text
AssistWeightPenalty
AssistMobilityPenalty
AssistResistancePenalty
MinAssistSpeedMultiplier
MinVibration
VibrationMultiplier
AssistBreakDistance
```
