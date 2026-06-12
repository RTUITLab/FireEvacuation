# 00. Индекс документации NPC и угроз

## Назначение документации

Документация описывает реализацию поведения NPC-пострадавших в VR-симуляции эвакуации при пожаре.

Документация предназначена для последующей реализации в Unity через `NavMeshAgent`, автоматически создаваемые точки оценки пространства и набор параметров NPC.

## Основной принцип модели

NPC не использует отдельную сложную систему рассуждений. Поведение строится через:

1. постоянные параметры NPC;
2. динамические состояния NPC;
3. автоматические точки оценки на NavMesh;
4. суммарную оценку желательности точек;
5. простую конечную машину состояний;
6. команды и физическое взаимодействие игрока;
7. воздействие дыма и огня.

Игрок не отключает самостоятельную логику NPC. Команды игрока и физическое присутствие спасателя изменяют приоритеты точек и тем самым влияют на поведение NPC.

## Состав файлов

1. `01_npc_parameters.md` — постоянные параметры NPC и динамические состояния.
2. `02_navigation_and_point_scoring.md` — автоматические точки оценки, NavMesh и формула выбора цели.
3. `03_npc_states_and_commands.md` — состояния NPC, переходы и команды игрока.
4. `04_threats_smoke_and_fire.md` — дым, огонь и их влияние на пространство.
5. `05_npc_threat_interactions.md` — влияние угроз на NPC, панику, ущерб и недееспособность.
6. `06_player_npc_physical_interaction.md` — физическое ведение NPC, сопротивление и вибрация контроллера.

## Реализационные ограничения MVP

В первой версии не реализуются:

- смерть NPC;
- ручная расстановка DecisionPoint по сцене;
- укрытия;
- баррикадность;
- групповое поведение NPC;
- распространение огня, если оно не предоставлено готовой библиотекой;
- логирование решений NPC;
- визуальная отладка через Gizmos как обязательная механика.

## Рекомендуемые Unity-компоненты

```text
NPCBehaviorController
NPCParameterSet
NPCStateMachine
NavigationProbeGenerator
NavigationProbePoint
NPCCommandReceiver
NPCAssistInteraction
SmokeZone
FireSource
ExitPoint
RescuerInfluenceProvider
NavMeshAgent
```

## Центральная логика

Центральной частью поведения является выбор точки, в которую NPC хочет двигаться.

Точка выбирается не вручную заданным сценарием, а по суммарной оценке:

```text
PointDesirability =
    ExitProximity * SpatialOrientation * OrientationWeight
    - PointDanger * DangerAvoidance * DangerWeight
    - DistanceToPoint * DistanceWeight
    + RescuerProximity * TrustToRescuer * RescuerWeight
    + CommandTargetProximity * CommandWeight
    + CurrentPositionBonus
```

Если точка заблокирована:

```text
PointDesirability = -Infinity
```

Паника не входит напрямую в формулу выбора точки. Паника используется для перехода в хаотичное состояние.
