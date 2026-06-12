# 02. Навигация и оценка точек пространства

## Назначение

Навигация NPC строится на сочетании:

```text
Unity NavMesh + автоматически созданные NavigationProbePoint
```

`NavMeshAgent` отвечает за фактическое движение.

`NavigationProbePoint` отвечает за выбор желательной области, в которую NPC хочет двигаться.

NPC не использует вручную расставленные DecisionPoint.

## Основные компоненты

```text
NavigationProbeGenerator
NavigationProbePoint
ExitPoint
SmokeZone
FireSource
RescuerInfluenceProvider
NPCBehaviorController
NavMeshAgent
```

## Генерация точек оценки

При запуске сцены `NavigationProbeGenerator` автоматически создаёт точки оценки на поверхности NavMesh.

Шаг генерации задаётся параметром:

```text
ProbeSpacing
```

Значение задаётся в Unity Inspector.

Пример значения по умолчанию:

```text
ProbeSpacing = 2.0
```

Для каждой потенциальной позиции генератор проверяет, находится ли она на NavMesh.

Пример логики:

```text
for each grid position in scene bounds:
    if NavMesh.SamplePosition(position) succeeds:
        create NavigationProbePoint
```

## Данные точки оценки

Каждая `NavigationProbePoint` хранит:

```text
PointId
Position
ZoneId
ExitProximity
PointDanger
IsBlocked
DistanceToNPC
DistanceToPlayer
RescuerProximity
CommandTargetProximity
VisibleForNPC
```

## Выходы

Выходы задаются вручную объектами `ExitPoint`.

NPC не создаёт выходы автоматически.

Каждая точка оценки получает `ExitProximity` по ближайшему выходу.

## Расчёт близости к выходу

`ExitProximity` рассчитывается один раз при запуске сцены.

Для каждой точки определяется расстояние до ближайшего выхода по NavMesh.

```text
DistanceToNearestExit = Min(NavMeshPathDistance(Point, ExitPoint))
```

После этого расстояние нормализуется:

```text
ExitProximity = 1 - NormalizedDistanceToNearestExit
```

`ExitProximity` ограничивается диапазоном `0..1`.

Чем ближе точка к выходу, тем выше `ExitProximity`.

Если путь к выходу не найден при запуске сцены:

```text
ExitProximity = 0
```

## Динамическая опасность точки

`PointDanger` пересчитывается во время сценария.

Опасность точки формируется угрозами:

```text
PointDanger = SmokeDanger + FireDanger
```

Значение ограничивается диапазоном `0..1`.

## Блокировка точки

Если точка находится в ближнем радиусе активного огня, она становится заблокированной:

```text
IsBlocked = true
```

Заблокированная точка полностью исключается из выбора:

```text
PointDesirability = -Infinity
```

Дым не блокирует точку.

## Выбор точек для оценки NPC

NPC не оценивает все точки сцены каждый раз.

Для снижения вычислительной нагрузки NPC оценивает только:

- точки в радиусе поиска;
- точки в зоне видимости;
- текущую позицию как возможный вариант остаться на месте;
- точки рядом с игроком, если игрок видим;
- точки рядом с целевой командой `GoThere`, если команда активна.

Радиус поиска задаётся параметром:

```text
ProbeSearchRadius
```

Значение задаётся в Unity Inspector.

## Проверка видимости точки

Точка считается видимой для NPC, если:

1. расстояние до точки не превышает радиус видимости;
2. Raycast между NPC и точкой не пересекает непрозрачное препятствие.

```text
VisibleForNPC = DistanceToPoint <= ViewRadius && RaycastIsClear(NPC, Point)
```

Дым не снижает видимость точки.

## Текущая позиция как точка выбора

NPC всегда может остаться на текущей позиции.

Для этого текущая позиция добавляется в список кандидатов как временная точка оценки.

Если команда `Stop` активна, текущая позиция получает дополнительный бонус.

## Пересчёт решения

NPC пересчитывает желательную точку с заданным интервалом:

```text
DecisionUpdateInterval
```

Значение задаётся в Unity Inspector.

Пример значения по умолчанию:

```text
DecisionUpdateInterval = 3.0
```

Если текущий путь заблокирован, NPC пересчитывает цель немедленно.

## Защита от дёргания между точками

NPC не должен менять цель при незначительной разнице оценки.

Новая точка выбирается только если она лучше текущей минимум на порог:

```text
if NewPointDesirability > CurrentTargetDesirability + SwitchTargetThreshold:
    switch target
```

`SwitchTargetThreshold` задаётся в Unity Inspector.

## Формула желательности точки

Базовая формула:

```text
PointDesirability =
    ExitProximity * SpatialOrientation * OrientationWeight
    - PointDanger * DangerAvoidance * DangerWeight
    - DistanceToPoint * DistanceWeight
    + RescuerProximity * TrustToRescuer * RescuerWeight
    + CommandTargetProximity * CommandWeight
    + CurrentPositionBonus
```

## Компонент близости к выходу

```text
ExitScore = ExitProximity * SpatialOrientation * OrientationWeight
```

Если `SpatialOrientation = 0`, близость к выходу не влияет на выбор.

Если `SpatialOrientation = 1`, близость к выходу влияет максимально.

## Компонент опасности

```text
DangerPenalty = PointDanger * DangerAvoidance * DangerWeight
```

Чем выше опасность точки и чем выше избегание опасности, тем ниже итоговая желательность.

## Компонент расстояния

```text
DistancePenalty = DistanceToPoint * DistanceWeight
```

Этот штраф нужен, чтобы NPC не выбирал далёкую точку, если рядом есть почти такой же хороший вариант.

## Компонент спасателя

Точки рядом с видимым игроком получают бонус.

```text
RescuerScore = RescuerProximity * TrustToRescuer * RescuerWeight
```

`RescuerProximity` рассчитывается по расстоянию до игрока:

```text
RescuerProximity = 1 - NormalizedDistanceToPlayer
```

Если NPC не видит игрока:

```text
RescuerProximity = 0
```

## Компонент команды

Команды игрока не принуждают NPC к действию напрямую.

Команды повышают приоритет соответствующих точек.

Для команды `GoThere`:

```text
CommandTargetScore = CommandTargetProximity * CommandWeight
```

Для команды `FollowPlayer` командной целью считаются точки рядом с игроком.

Для команды `Stop` командной целью считается текущая позиция NPC.

## Компонент текущей позиции

Текущая позиция получает бонус, чтобы NPC не метался между близкими по оценке точками.

```text
CurrentPositionBonus = StayPointBonus
```

При активной команде `Stop` бонус текущей позиции повышается:

```text
CurrentPositionBonus = StayPointBonus + StopCommandBonus
```

## Обработка выбранной точки

Если выбрана новая точка:

```text
NavMeshAgent.SetDestination(SelectedPoint.Position)
State = MoveToPoint
```

Если лучшей точкой остаётся текущая позиция:

```text
State = Idle
```

## Список настраиваемых параметров

Все веса и интервалы должны быть доступны в Unity Inspector:

```text
ProbeSpacing
ProbeSearchRadius
ViewRadius
DecisionUpdateInterval
SwitchTargetThreshold
OrientationWeight
DangerWeight
DistanceWeight
RescuerWeight
CommandWeight
StayPointBonus
StopCommandBonus
FireBlockRadius
FireDangerRadius
SmokeDangerWeight
FireDangerWeight
```
