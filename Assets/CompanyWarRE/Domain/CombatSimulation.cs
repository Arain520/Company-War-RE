using System;
using System.Collections.Generic;
using System.Linq;

namespace CompanyWarRE.Domain
{
    public sealed class CombatantDefinition
    {
        public CombatantDefinition(
            string id,
            string type,
            int durability,
            double attack,
            double speed,
            double attackIntervalSeconds,
            int range,
            int assaultScoreReward = 0,
            string effect = "",
            string name = "")
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A combatant id is required.", nameof(id));
            }

            Id = id;
            Type = type ?? string.Empty;
            Durability = Math.Max(1, durability);
            Attack = Math.Max(0d, attack);
            Speed = Math.Max(0d, speed);
            AttackIntervalSeconds = Math.Max(0.1d, attackIntervalSeconds);
            Range = Math.Max(1, range);
            AssaultScoreReward = Math.Max(0, assaultScoreReward);
            Effect = effect ?? string.Empty;
            Name = name ?? string.Empty;
        }

        public string Id { get; }
        public string Type { get; }
        public int Durability { get; }
        public double Attack { get; }
        public double Speed { get; }
        public double AttackIntervalSeconds { get; }
        public int Range { get; }
        public int AssaultScoreReward { get; }
        public string Effect { get; }
        public string Name { get; }
        public bool IsStealth =>
            string.Equals(Effect, "Stealth", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Id, "U30", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Id, "U31", StringComparison.OrdinalIgnoreCase);
        public bool IsBuilding =>
            !IsStealth && Type.IndexOf("build", StringComparison.OrdinalIgnoreCase) >= 0;
        public bool HasPersistentEnemyCurse =>
            string.Equals(Id, "E13", StringComparison.OrdinalIgnoreCase);
        public bool HasKillHeal =>
            string.Equals(Id, "E14", StringComparison.OrdinalIgnoreCase);
        public bool HasExecutionCast =>
            string.Equals(Id, "E15", StringComparison.OrdinalIgnoreCase);
        public bool HasHealingAction =>
            string.Equals(Id, "U21", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Id, "U33", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Effect, "HealLowest", StringComparison.OrdinalIgnoreCase);
        public bool HasConversionAction =>
            string.Equals(Id, "U32", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Effect, "ConvertEnemy", StringComparison.OrdinalIgnoreCase);
        public bool HasAuthorityPushback =>
            string.Equals(Id, "U27", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Effect, "AuthorityPushback", StringComparison.OrdinalIgnoreCase);
        public bool HasHeavyStrike =>
            string.Equals(Id, "U16", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Id, "U19", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Id, "U20", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Id, "U22", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Id, "U23", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Id, "E09", StringComparison.OrdinalIgnoreCase);
        public int FootprintColumns => IsBuilding ? BattleGrid.ControlBlockSize : 1;
        public int FootprintRows => IsBuilding ? BattleGrid.ControlBlockSize : 1;
        public bool IsMovingMelee =>
            Speed > 0d && Range <= 1 && !IsBuilding &&
            string.Equals(Type, "Staff", StringComparison.OrdinalIgnoreCase);
        public bool CanMove => Speed > 0d && !IsBuilding;
    }

    public enum CombatEventType
    {
        Attack,
        MeleeBattlefieldStarted,
        MeleeBattlefieldEnded,
        Heal,
        Conversion,
        Pushback,
        Pollution,
        Death
    }

    public sealed class CombatEvent
    {
        public CombatEvent(
            long sequence,
            CombatEventType type,
            string actorId,
            string targetActorId,
            double amount,
            int column = 0,
            int row = 0,
            int controlBlockColumn = 0,
            int controlBlockRow = 0)
        {
            Sequence = sequence;
            Type = type;
            ActorId = actorId ?? string.Empty;
            TargetActorId = targetActorId ?? string.Empty;
            Amount = amount;
            Column = column;
            Row = row;
            ControlBlockColumn = controlBlockColumn;
            ControlBlockRow = controlBlockRow;
        }

        public long Sequence { get; }
        public CombatEventType Type { get; }
        public string ActorId { get; }
        public string TargetActorId { get; }
        public double Amount { get; }
        public int Column { get; }
        public int Row { get; }
        public int ControlBlockColumn { get; }
        public int ControlBlockRow { get; }
    }

    public sealed class CombatActorSnapshot
    {
        internal CombatActorSnapshot(CombatActor actor, int gridColumns, int gridRows)
        {
            ActorId = actor.ActorId;
            TemplateId = actor.Definition.Id;
            Team = actor.Team;
            Column = actor.Column;
            LanePosition = actor.LanePosition;
            MovementSpeed = actor.Definition.Speed;
            HitPoints = Math.Max(0d, actor.HitPoints);
            MaximumHitPoints = actor.Definition.Durability;
            AttackProgress = actor.AttackProgress;
            IsAlive = actor.IsAlive;
            IsBuilding = actor.Definition.IsBuilding;
            Effect = actor.Definition.Effect;
            IsStealth = actor.Definition.IsStealth;
            HasHealingAction = actor.Definition.HasHealingAction;
            HasConversionAction = actor.Definition.HasConversionAction;
            HasAuthorityPushback = actor.Definition.HasAuthorityPushback;
            HasHeavyStrike = actor.Definition.HasHeavyStrike;
            AssaultScoreReward = actor.Definition.AssaultScoreReward;
            FootprintColumns = actor.Definition.FootprintColumns;
            FootprintRows = actor.Definition.FootprintRows;
            var actorRow = (int)Math.Round(actor.LanePosition);
            if (IsBuilding)
            {
                FootprintStartColumn = GetControlBlockStart(actor.Column);
                FootprintStartRow = GetControlBlockStart(actorRow);
            }
            else
            {
                FootprintStartColumn = actor.Column;
                FootprintStartRow = actorRow;
            }

            FootprintEndColumn = Math.Min(gridColumns, FootprintStartColumn + FootprintColumns - 1);
            FootprintEndRow = Math.Min(gridRows, FootprintStartRow + FootprintRows - 1);
        }

        public string ActorId { get; }
        public string TemplateId { get; }
        public Team Team { get; }
        public int Column { get; }
        public double LanePosition { get; }
        public double MovementSpeed { get; }
        public double HitPoints { get; }
        public double MaximumHitPoints { get; }
        public double AttackProgress { get; }
        public bool IsAlive { get; }
        public bool IsBuilding { get; }
        public string Effect { get; }
        public bool IsStealth { get; }
        public bool HasHealingAction { get; }
        public bool HasConversionAction { get; }
        public bool HasAuthorityPushback { get; }
        public bool HasHeavyStrike { get; }
        public int AssaultScoreReward { get; }
        public int FootprintColumns { get; }
        public int FootprintRows { get; }
        public int FootprintStartColumn { get; }
        public int FootprintEndColumn { get; }
        public int FootprintStartRow { get; }
        public int FootprintEndRow { get; }

        private static int GetControlBlockStart(int cellIndex)
        {
            return ((Math.Max(1, cellIndex) - 1) / BattleGrid.ControlBlockSize) *
                   BattleGrid.ControlBlockSize + 1;
        }
    }

    internal sealed class CombatActor
    {
        private const double ExecutionCasterInitialDelaySeconds = 15d;

        public CombatActor(
            string actorId,
            Team team,
            CombatantDefinition definition,
            int column,
            double lanePosition)
        {
            ActorId = actorId;
            Team = team;
            Definition = definition;
            Column = column;
            LanePosition = lanePosition;
            HitPoints = definition.Durability;
            if (definition.HasExecutionCast)
            {
                AttackProgress = definition.AttackIntervalSeconds - ExecutionCasterInitialDelaySeconds;
            }
        }

        public string ActorId { get; }
        public Team Team { get; set; }
        public CombatantDefinition Definition { get; }
        public int Column { get; set; }
        public double LanePosition { get; set; }
        public double HitPoints { get; set; }
        public double AttackProgress { get; set; }
        public double DamageCarry { get; set; }
        public bool DeathReported { get; set; }
        public int DeathCleanupTicksRemaining { get; set; }
        public bool DeathEchoSpawned { get; set; }
        public bool IsAlive => HitPoints > 0d;
    }

    internal sealed class MeleeEngagement
    {
        public CombatActor Ally;
        public CombatActor Enemy;
        public int ControlBlockColumn;
        public int ControlBlockRow;
        public double AllyStopLane;
        public double EnemyStopLane;
    }

    internal sealed class PendingAttack
    {
        public CombatActor Attacker;
        public CombatActor Target;
        public double Damage;
    }

    public sealed class CombatSimulation
    {
        public const double ContactDistance = 0.12d;
        public const double ContactEpsilon = 0.02d;
        public const double E13CurseDamagePerSecond = 0.1d;

        private readonly int _columns;
        private readonly int _rows;
        private readonly List<CombatActor> _actors = new List<CombatActor>();
        private readonly List<CombatEvent> _events = new List<CombatEvent>();
        private readonly List<MeleeEngagement> _meleeEngagements = new List<MeleeEngagement>();
        private readonly HashSet<string> _activeMeleeBattlefields =
            new HashSet<string>(StringComparer.Ordinal);
        private long _nextEventSequence = 1;
        private int _deathEchoSequence;
        private int _enemyFreezeTicks;
        private double _enemySpeedMultiplier = 1d;
        private double _enemySpeedMultiplierRemainingSeconds;

        public CombatSimulation(int columns, int rows)
        {
            if (columns <= 0 || rows <= 0)
            {
                throw new ArgumentException("Combat dimensions must be positive.");
            }

            _columns = columns;
            _rows = rows;
        }

        public IReadOnlyList<CombatEvent> Events => _events;

        public int EnemyFreezeTicksRemaining => _enemyFreezeTicks;
        public double EnemySpeedMultiplier => _enemySpeedMultiplier;

        public bool ContainsActor(string actorId)
        {
            return !string.IsNullOrWhiteSpace(actorId) &&
                   _actors.Any(actor => string.Equals(actor.ActorId, actorId, StringComparison.Ordinal));
        }

        public bool RemoveActor(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId)) return false;
            var removed = _actors.RemoveAll(actor =>
                string.Equals(actor.ActorId, actorId, StringComparison.Ordinal)) > 0;
            if (!removed) return false;
            _meleeEngagements.RemoveAll(engagement =>
                string.Equals(engagement.Ally?.ActorId, actorId, StringComparison.Ordinal) ||
                string.Equals(engagement.Enemy?.ActorId, actorId, StringComparison.Ordinal));
            return true;
        }

        public bool TryAddActor(
            string actorId,
            Team team,
            CombatantDefinition definition,
            int column,
            double lanePosition)
        {
            if (string.IsNullOrWhiteSpace(actorId) || definition == null || ContainsActor(actorId) ||
                column < 1 || column > _columns || lanePosition < 1d || lanePosition > _rows)
            {
                return false;
            }

            if (definition.IsBuilding)
            {
                var buildingRow = (int)Math.Round(lanePosition);
                var startColumn = GetControlBlockStart(column);
                var startRow = GetControlBlockStart(buildingRow);
                if (Math.Abs(lanePosition - buildingRow) > 0.0001d ||
                    startColumn + BattleGrid.ControlBlockSize - 1 > _columns ||
                    startRow + BattleGrid.ControlBlockSize - 1 > _rows)
                {
                    return false;
                }
            }

            _actors.Add(new CombatActor(actorId, team, definition, column, lanePosition));
            return true;
        }

        public void Advance(double deltaSeconds)
        {
            Advance(deltaSeconds, null);
        }

        public void Advance(double deltaSeconds, BattleGrid grid)
        {
            if (grid != null && (grid.Columns != _columns || grid.Rows != _rows))
            {
                throw new ArgumentException("Combat and territory dimensions must match.", nameof(grid));
            }

            var delta = Math.Max(0.0001d, deltaSeconds);
            AdvanceDeathCleanupAndSpawnEchoes();
            ApplyPersistentEnemyBuildingEffects(delta);
            TickNegativeActionLocks(delta);
            RefreshMeleeEngagements();
            MoveActors(delta);
            ResolveEnemyTerritoryBreaches(grid);
            RefreshMeleeBattlefieldEvents();
            ResolveAttacks(delta, grid);
            ReportDeaths();
            RefreshMeleeEngagements();
            RefreshMeleeBattlefieldEvents();
            TickTimedEnemyMovementEffects(delta);
        }

        public void ApplyGlobalFreeze(int durationTicks)
        {
            _enemyFreezeTicks = Math.Max(_enemyFreezeTicks, Math.Max(0, durationTicks));
        }

        public void ApplyEnemySpeedMultiplier(double multiplier, double durationSeconds)
        {
            var normalizedMultiplier = Math.Max(0d, Math.Min(1d, multiplier));
            if (durationSeconds <= 0d)
            {
                return;
            }

            _enemySpeedMultiplier = Math.Min(_enemySpeedMultiplier, normalizedMultiplier);
            _enemySpeedMultiplierRemainingSeconds = Math.Max(
                _enemySpeedMultiplierRemainingSeconds,
                durationSeconds);
        }

        public int ApplySupportDamage(GridPosition target, int controlBlockRadius, double buildingDamage)
        {
            if (target.Column < 1 || target.Column > _columns || target.Row < 1 || target.Row > _rows)
            {
                return 0;
            }

            var targetBlockColumn = GetControlBlockColumn(target.Column);
            var targetBlockRow = GetControlBlockRow(target.Row);
            var radius = Math.Max(0, controlBlockRadius);
            var affected = 0;
            foreach (var actor in _actors.Where(candidate =>
                         candidate.IsAlive &&
                         candidate.Team == Team.Enemy &&
                         Math.Abs(GetControlBlockColumn(candidate.Column) - targetBlockColumn) <= radius &&
                         Math.Abs(GetControlBlockRow(ToDiscretePosition(candidate).Row) - targetBlockRow) <= radius)
                     .ToList())
            {
                actor.HitPoints = actor.Definition.IsBuilding
                    ? actor.HitPoints - Math.Min(actor.HitPoints, Math.Max(0d, buildingDamage))
                    : 0d;
                affected++;
            }

            ReportDeaths();
            return affected;
        }

        public int ApplyLure(GridPosition target, int controlBlockRadius, double lockSeconds)
        {
            if (target.Column < 1 || target.Column > _columns || target.Row < 1 || target.Row > _rows)
            {
                return 0;
            }

            var targetBlockColumn = GetControlBlockColumn(target.Column);
            var targetBlockRow = GetControlBlockRow(target.Row);
            var targetColumn = GetControlBlockCenterColumn(targetBlockColumn);
            var radius = Math.Max(0, controlBlockRadius);
            var affected = 0;
            foreach (var actor in _actors.Where(candidate =>
                         candidate.IsAlive &&
                         candidate.Team == Team.Enemy &&
                         !candidate.Definition.IsBuilding &&
                         Math.Abs(GetControlBlockColumn(candidate.Column) - targetBlockColumn) <= radius &&
                         Math.Abs(GetControlBlockRow(ToDiscretePosition(candidate).Row) - targetBlockRow) <= radius)
                     .ToList())
            {
                actor.Column = targetColumn;
                actor.AttackProgress = -Math.Max(0d, lockSeconds);
                affected++;
            }

            return affected;
        }

        public IReadOnlyList<CombatActorSnapshot> CreateSnapshot()
        {
            return _actors
                .OrderBy(actor => actor.ActorId, StringComparer.Ordinal)
                .Select(actor => new CombatActorSnapshot(actor, _columns, _rows))
                .ToArray();
        }

        private void ApplyPersistentEnemyBuildingEffects(double deltaSeconds)
        {
            var curseSources = _actors
                .Where(actor =>
                    actor.IsAlive &&
                    actor.Team == Team.Enemy &&
                    actor.Definition.HasPersistentEnemyCurse)
                .ToList();
            if (curseSources.Count == 0)
            {
                return;
            }

            foreach (var ally in _actors.Where(actor => actor.IsAlive && actor.Team == Team.Ally).ToList())
            {
                var affectingSources = curseSources.Count(source =>
                    ControlBlockDistance(source, ally) <= Math.Max(1, source.Definition.Range));
                if (affectingSources == 0)
                {
                    continue;
                }

                ally.DamageCarry += E13CurseDamagePerSecond * affectingSources * deltaSeconds;
                var completedDamage = (int)Math.Floor(ally.DamageCarry + 0.0001d);
                if (completedDamage <= 0)
                {
                    continue;
                }

                ally.HitPoints -= completedDamage;
                ally.DamageCarry = Math.Max(0d, ally.DamageCarry - completedDamage);
            }
        }

        private void MoveActors(double deltaSeconds)
        {
            var positions = _actors.ToDictionary(actor => actor.ActorId, actor => actor.LanePosition);
            foreach (var actor in _actors.Where(candidate => candidate.IsAlive && candidate.Definition.CanMove))
            {
                if (actor.AttackProgress < 0d)
                {
                    continue;
                }

                if (actor.Team == Team.Enemy && _enemyFreezeTicks > 0)
                {
                    continue;
                }

                var movementSpeed = actor.Definition.Speed *
                                    (actor.Team == Team.Enemy ? _enemySpeedMultiplier : 1d);
                if (actor.Definition.IsMovingMelee)
                {
                    var meleeTargetInBlock = FindNearestMeleeOpponentInSameBlock(actor, positions);
                    if (meleeTargetInBlock != null)
                    {
                        MoveActorTowardBattleLine(
                            actor,
                            GetControlBlockRow(ToDiscretePosition(actor).Row),
                            movementSpeed * deltaSeconds);
                        continue;
                    }

                    var engagement = FindMeleeEngagement(actor);
                    if (engagement != null)
                    {
                        MoveActorTowardBattleLine(
                            actor,
                            engagement.ControlBlockRow,
                            movementSpeed * deltaSeconds);
                        continue;
                    }
                }

                if (actor.Definition.Attack > 0d && FindAttackTarget(actor) != null)
                {
                    continue;
                }

                var target = FindForwardTarget(actor, positions, false);
                if (target == null)
                {
                    actor.LanePosition = ClampLane(
                        actor.LanePosition + Direction(actor.Team) * movementSpeed * deltaSeconds);
                    continue;
                }

                var separation = Math.Abs(positions[target.ActorId] - positions[actor.ActorId]);
                var remaining = Math.Max(0d, separation - ContactDistance);
                if (remaining <= 0d)
                {
                    continue;
                }

                var targetSpeed = target.Definition.CanMove
                    ? target.Definition.Speed * (target.Team == Team.Enemy ? _enemySpeedMultiplier : 1d)
                    : 0d;
                var totalSpeed = movementSpeed + targetSpeed;
                var actorShare = totalSpeed <= 0d
                    ? 0d
                    : remaining * movementSpeed / totalSpeed;
                var movement = Math.Min(movementSpeed * deltaSeconds, actorShare);
                actor.LanePosition = ClampLane(
                    actor.LanePosition + Direction(actor.Team) * movement);
            }
        }

        private void TickTimedEnemyMovementEffects(double deltaSeconds)
        {
            if (_enemyFreezeTicks > 0)
            {
                _enemyFreezeTicks--;
            }

            if (_enemySpeedMultiplierRemainingSeconds <= 0d)
            {
                return;
            }

            _enemySpeedMultiplierRemainingSeconds = Math.Max(
                0d,
                _enemySpeedMultiplierRemainingSeconds - deltaSeconds);
            if (_enemySpeedMultiplierRemainingSeconds <= 0d)
            {
                _enemySpeedMultiplier = 1d;
            }
        }

        private void RefreshMeleeEngagements()
        {
            _meleeEngagements.RemoveAll(engagement =>
                engagement == null ||
                !IsMovingMelee(engagement.Ally) ||
                !IsMovingMelee(engagement.Enemy) ||
                GetControlBlockColumn(engagement.Ally.Column) !=
                GetControlBlockColumn(engagement.Enemy.Column) ||
                Math.Abs(
                    GetControlBlockRow(ToDiscretePosition(engagement.Ally).Row) -
                    GetControlBlockRow(ToDiscretePosition(engagement.Enemy).Row)) > 1 ||
                engagement.Ally.LanePosition > engagement.Enemy.LanePosition + ContactEpsilon);

            var engagedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var engagement in _meleeEngagements)
            {
                engagedIds.Add(engagement.Ally.ActorId);
                engagedIds.Add(engagement.Enemy.ActorId);
            }

            var candidates =
                (from ally in _actors
                 from enemy in _actors
                 where IsMovingMelee(ally) &&
                       IsMovingMelee(enemy) &&
                       ally.Team == Team.Ally &&
                       enemy.Team == Team.Enemy &&
                       !engagedIds.Contains(ally.ActorId) &&
                       !engagedIds.Contains(enemy.ActorId) &&
                       GetControlBlockColumn(ally.Column) == GetControlBlockColumn(enemy.Column) &&
                       enemy.LanePosition + ContactEpsilon >= ally.LanePosition &&
                       GetControlBlockRow(ToDiscretePosition(enemy).Row) -
                       GetControlBlockRow(ToDiscretePosition(ally).Row) >= 0 &&
                       GetControlBlockRow(ToDiscretePosition(enemy).Row) -
                       GetControlBlockRow(ToDiscretePosition(ally).Row) <= 1
                 select new
                 {
                     Ally = ally,
                     Enemy = enemy,
                     Gap = Math.Abs(enemy.LanePosition - ally.LanePosition)
                 })
                .OrderBy(candidate => candidate.Gap)
                .ThenBy(candidate => candidate.Ally.ActorId, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Enemy.ActorId, StringComparer.Ordinal)
                .ToList();

            foreach (var candidate in candidates)
            {
                if (engagedIds.Contains(candidate.Ally.ActorId) || engagedIds.Contains(candidate.Enemy.ActorId))
                {
                    continue;
                }

                var engagement = CreateMeleeEngagement(candidate.Ally, candidate.Enemy);
                if (engagement == null)
                {
                    continue;
                }

                _meleeEngagements.Add(engagement);
                engagedIds.Add(candidate.Ally.ActorId);
                engagedIds.Add(candidate.Enemy.ActorId);
            }
        }

        private MeleeEngagement CreateMeleeEngagement(CombatActor ally, CombatActor enemy)
        {
            var allyBlockRow = GetControlBlockRow(ToDiscretePosition(ally).Row);
            var enemyBlockRow = GetControlBlockRow(ToDiscretePosition(enemy).Row);
            if (allyBlockRow == enemyBlockRow)
            {
                return BuildMeleeEngagement(ally, enemy, allyBlockRow);
            }

            if (enemyBlockRow != allyBlockRow + 1)
            {
                return null;
            }

            var boundaryLane = GetControlBlockStartRow(enemyBlockRow);
            var allyArrivalSeconds = ResolveArrivalSeconds(
                boundaryLane - ally.LanePosition,
                ally.Definition.Speed);
            var enemyArrivalSeconds = ResolveArrivalSeconds(
                enemy.LanePosition - boundaryLane,
                enemy.Definition.Speed);
            var allyArrivesFirst = allyArrivalSeconds < enemyArrivalSeconds - ContactEpsilon;
            if ((double.IsPositiveInfinity(allyArrivalSeconds) &&
                 double.IsPositiveInfinity(enemyArrivalSeconds)) ||
                Math.Abs(allyArrivalSeconds - enemyArrivalSeconds) <= ContactEpsilon)
            {
                allyArrivesFirst = string.Compare(
                    ally.ActorId,
                    enemy.ActorId,
                    StringComparison.Ordinal) <= 0;
            }

            return BuildMeleeEngagement(
                ally,
                enemy,
                allyArrivesFirst ? enemyBlockRow : allyBlockRow);
        }

        private MeleeEngagement BuildMeleeEngagement(
            CombatActor ally,
            CombatActor enemy,
            int controlBlockRow)
        {
            return new MeleeEngagement
            {
                Ally = ally,
                Enemy = enemy,
                ControlBlockColumn = GetControlBlockColumn(ally.Column),
                ControlBlockRow = controlBlockRow,
                AllyStopLane = GetControlBlockStartRow(controlBlockRow),
                EnemyStopLane = GetControlBlockEndRow(controlBlockRow)
            };
        }

        private MeleeEngagement FindMeleeEngagement(CombatActor actor)
        {
            return _meleeEngagements.FirstOrDefault(engagement =>
                engagement.Ally == actor || engagement.Enemy == actor);
        }

        private CombatActor FindNearestMeleeOpponentInSameBlock(
            CombatActor actor,
            IReadOnlyDictionary<string, double> positions = null)
        {
            if (!IsMovingMelee(actor))
            {
                return null;
            }

            var actorPosition = ToDiscretePosition(actor, positions);
            var actorBlockColumn = GetControlBlockColumn(actorPosition.Column);
            var actorBlockRow = GetControlBlockRow(actorPosition.Row);
            return _actors
                .Where(other =>
                    IsMovingMelee(other) &&
                    other.Team != actor.Team &&
                    GetControlBlockColumn(ToDiscretePosition(other, positions).Column) == actorBlockColumn &&
                    GetControlBlockRow(ToDiscretePosition(other, positions).Row) == actorBlockRow)
                .OrderBy(other => Math.Abs(GetLanePosition(other, positions) - GetLanePosition(actor, positions)))
                .ThenBy(other => Math.Abs(other.Column - actor.Column))
                .ThenBy(other => other.ActorId, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private void MoveActorTowardBattleLine(
            CombatActor actor,
            int controlBlockRow,
            double maximumMovement)
        {
            var targetLane = actor.Team == Team.Ally
                ? GetControlBlockStartRow(controlBlockRow)
                : GetControlBlockEndRow(controlBlockRow);
            actor.LanePosition = MoveTowards(actor.LanePosition, targetLane, maximumMovement);
        }

        private void RefreshMeleeBattlefieldEvents()
        {
            var occupiedBattlefields = _actors
                .Where(IsMovingMelee)
                .GroupBy(actor => new
                {
                    Column = GetControlBlockColumn(actor.Column),
                    Row = GetControlBlockRow(ToDiscretePosition(actor).Row)
                })
                .Where(group =>
                    group.Any(actor => actor.Team == Team.Ally) &&
                    group.Any(actor => actor.Team == Team.Enemy))
                .Select(group => group.Key)
                .OrderBy(block => block.Column)
                .ThenBy(block => block.Row)
                .ToList();

            var currentKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var battlefield in occupiedBattlefields)
            {
                var key = BuildMeleeBattlefieldKey(battlefield.Column, battlefield.Row);
                currentKeys.Add(key);
                if (_activeMeleeBattlefields.Add(key))
                {
                    AddMeleeBattlefieldEvent(
                        CombatEventType.MeleeBattlefieldStarted,
                        battlefield.Column,
                        battlefield.Row);
                }
            }

            foreach (var endedKey in _activeMeleeBattlefields.Where(key => !currentKeys.Contains(key)).ToList())
            {
                var parts = endedKey.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out var column) && int.TryParse(parts[1], out var row))
                {
                    AddMeleeBattlefieldEvent(CombatEventType.MeleeBattlefieldEnded, column, row);
                }

                _activeMeleeBattlefields.Remove(endedKey);
            }
        }

        private void AddMeleeBattlefieldEvent(CombatEventType type, int blockColumn, int blockRow)
        {
            _events.Add(new CombatEvent(
                _nextEventSequence++,
                type,
                string.Empty,
                string.Empty,
                0d,
                GetControlBlockCenterColumn(blockColumn),
                GetControlBlockCenterRow(blockRow),
                blockColumn,
                blockRow));
        }

        private CombatActor FindAttackTarget(CombatActor actor)
        {
            return FindNearestMeleeOpponentInSameBlock(actor) ??
                   FindForwardTarget(actor, null, true);
        }

        private void ResolveAttacks(double deltaSeconds, BattleGrid grid)
        {
            var pendingAttacks = new List<PendingAttack>();
            foreach (var actor in _actors
                         .Where(candidate =>
                             candidate.IsAlive &&
                             (candidate.Definition.Attack > 0d ||
                              candidate.Definition.HasExecutionCast ||
                              candidate.Definition.HasHealingAction ||
                              candidate.Definition.HasConversionAction ||
                              candidate.Definition.HasAuthorityPushback))
                         .OrderBy(candidate => candidate.Team == Team.Enemy ? 1 : 0)
                         .ThenBy(candidate => candidate.Team == Team.Ally
                             ? -candidate.LanePosition
                             : candidate.LanePosition)
                         .ThenBy(candidate => candidate.Column))
            {
                if (actor.AttackProgress < 0d && !actor.Definition.HasExecutionCast)
                {
                    continue;
                }

                if (actor.Definition.HasHealingAction)
                {
                    var healCount = AdvanceActionCounter(actor, deltaSeconds);
                    if (healCount > 0)
                    {
                        ApplyHealOperations(actor, healCount);
                    }

                    continue;
                }

                if (actor.Definition.HasConversionAction)
                {
                    var convertCount = AdvanceActionCounter(actor, deltaSeconds);
                    if (convertCount > 0 && grid != null)
                    {
                        ApplyConvertOperations(actor, convertCount, grid);
                    }

                    continue;
                }

                if (actor.Definition.HasExecutionCast)
                {
                    ResolveExecutionCasts(actor, deltaSeconds);
                    continue;
                }

                var target = FindAttackTarget(actor);
                if (target == null)
                {
                    actor.AttackProgress = 0d;
                    continue;
                }

                actor.AttackProgress += deltaSeconds;
                var attackCount = (int)Math.Floor(actor.AttackProgress / actor.Definition.AttackIntervalSeconds);
                if (attackCount <= 0)
                {
                    continue;
                }

                actor.AttackProgress -= attackCount * actor.Definition.AttackIntervalSeconds;
                var damage = attackCount * actor.Definition.Attack;
                pendingAttacks.Add(new PendingAttack
                {
                    Attacker = actor,
                    Target = target,
                    Damage = damage
                });
                if (actor.Definition.HasAuthorityPushback)
                {
                    ApplyAuthorityPushback(actor, target, 3, 2d, false);
                }
                for (var index = 0; index < attackCount; index++)
                {
                    _events.Add(new CombatEvent(
                        _nextEventSequence++,
                        CombatEventType.Attack,
                        actor.ActorId,
                        target.ActorId,
                        actor.Definition.Attack));
                }
            }

            foreach (var pending in pendingAttacks)
            {
                if (pending.Attacker == null || pending.Target == null || pending.Damage <= 0d)
                {
                    continue;
                }

                if (pending.Attacker.Definition.HasHeavyStrike)
                {
                    foreach (var target in _actors.Where(candidate =>
                                 candidate.IsAlive &&
                                 candidate.Team != pending.Attacker.Team &&
                                 SameControlBlock(candidate, pending.Target)).ToList())
                    {
                        ApplyPendingDamage(pending.Attacker, target, pending.Damage);
                    }
                }
                else
                {
                    ApplyPendingDamage(pending.Attacker, pending.Target, pending.Damage);
                }
            }
        }

        private static int AdvanceActionCounter(CombatActor actor, double deltaSeconds)
        {
            actor.AttackProgress += deltaSeconds;
            var count = (int)Math.Floor(actor.AttackProgress / actor.Definition.AttackIntervalSeconds);
            if (count > 0)
            {
                actor.AttackProgress = Math.Max(
                    0d,
                    actor.AttackProgress - count * actor.Definition.AttackIntervalSeconds);
            }

            return count;
        }

        private void TickNegativeActionLocks(double deltaSeconds)
        {
            foreach (var actor in _actors.Where(candidate =>
                         candidate.IsAlive &&
                         candidate.AttackProgress < 0d &&
                         !candidate.Definition.HasExecutionCast))
            {
                actor.AttackProgress = Math.Min(0d, actor.AttackProgress + deltaSeconds);
            }
        }

        private void ApplyHealOperations(CombatActor healer, int healCount)
        {
            var blockRange = Math.Max(1, healer.Definition.Range) / 2;
            for (var index = 0; index < healCount; index++)
            {
                var healerPosition = ToDiscretePosition(healer);
                var target = _actors
                    .Where(candidate =>
                        candidate.IsAlive &&
                        candidate.Team == healer.Team &&
                        candidate != healer &&
                        Math.Abs(GetControlBlockColumn(ToDiscretePosition(candidate).Column) -
                                 GetControlBlockColumn(healerPosition.Column)) <= blockRange &&
                        Math.Abs(GetControlBlockRow(ToDiscretePosition(candidate).Row) -
                                 GetControlBlockRow(healerPosition.Row)) <= blockRange)
                    .OrderBy(candidate => candidate.HitPoints)
                    .ThenBy(candidate =>
                        Math.Abs(candidate.Column - healer.Column) +
                        Math.Abs(candidate.LanePosition - healer.LanePosition))
                    .FirstOrDefault();
                if (target == null)
                {
                    break;
                }

                target.HitPoints += 1d;
                _events.Add(new CombatEvent(
                    _nextEventSequence++,
                    CombatEventType.Heal,
                    healer.ActorId,
                    target.ActorId,
                    1d));
            }
        }

        private void ApplyConvertOperations(
            CombatActor converter,
            int convertCount,
            BattleGrid grid)
        {
            for (var index = 0; index < convertCount; index++)
            {
                var targets = _actors
                    .Where(candidate =>
                        candidate.IsAlive &&
                        candidate.Team == Team.Enemy &&
                        !candidate.Definition.IsBuilding)
                    .OrderBy(candidate => DistanceToNearestControlledBlock(candidate, grid))
                    .ThenBy(candidate =>
                        Math.Abs(candidate.Column - converter.Column) +
                        Math.Abs(candidate.LanePosition - converter.LanePosition))
                    .Take(3)
                    .ToList();
                if (targets.Count == 0)
                {
                    break;
                }

                foreach (var target in targets)
                {
                    target.Team = Team.Ally;
                    _events.Add(new CombatEvent(
                        _nextEventSequence++,
                        CombatEventType.Conversion,
                        converter.ActorId,
                        target.ActorId,
                        1d));
                }
            }
        }

        private int DistanceToNearestControlledBlock(CombatActor actor, BattleGrid grid)
        {
            var position = ToDiscretePosition(actor);
            var actorBlockColumn = GetControlBlockColumn(position.Column);
            var actorBlockRow = GetControlBlockRow(position.Row);
            var best = int.MaxValue;
            for (var column = 1; column <= grid.ControlBlockColumns; column++)
            {
                for (var row = 1; row <= grid.ControlBlockRows; row++)
                {
                    var block = grid.GetControlBlock(new GridPosition(column, row));
                    if (block == null || !block.IsControlled || block.IsPolluted)
                    {
                        continue;
                    }

                    best = Math.Min(best, Math.Abs(column - actorBlockColumn) + Math.Abs(row - actorBlockRow));
                }
            }

            return best;
        }

        private void ApplyAuthorityPushback(
            CombatActor source,
            CombatActor target,
            int controlBlocks,
            double lockSeconds,
            bool affectBuildings)
        {
            if (target == null || !target.IsAlive || (!affectBuildings && target.Definition.IsBuilding))
            {
                return;
            }

            var targetRow = ToDiscretePosition(target).Row;
            var direction = target.Team == Team.Enemy ? 1 : -1;
            var targetBlockRow = GetControlBlockRow(targetRow) + direction * Math.Max(1, controlBlocks);
            targetBlockRow = Math.Max(1, Math.Min(GetControlBlockRow(_rows), targetBlockRow));
            target.LanePosition = target.Team == Team.Enemy
                ? GetControlBlockEndRow(targetBlockRow)
                : GetControlBlockStartRow(targetBlockRow);
            target.AttackProgress = -Math.Max(0d, lockSeconds);
            var targetPosition = ToDiscretePosition(target);
            _events.Add(new CombatEvent(
                _nextEventSequence++,
                CombatEventType.Pushback,
                source?.ActorId,
                target.ActorId,
                Math.Max(1, controlBlocks),
                targetPosition.Column,
                targetPosition.Row));
        }

        private void ApplyPendingDamage(CombatActor attacker, CombatActor target, double damage)
        {
            if (attacker == null || target == null || damage <= 0d)
            {
                return;
            }

            var targetWasAlive = target.IsAlive;
            target.DamageCarry += damage;
            var wholeDamage = (int)Math.Floor(target.DamageCarry + 0.0001d);
            if (wholeDamage > 0)
            {
                target.HitPoints -= wholeDamage;
                target.DamageCarry = Math.Max(0d, target.DamageCarry - wholeDamage);
            }

            if (attacker.Definition.HasKillHeal && targetWasAlive && !target.IsAlive)
            {
                attacker.HitPoints += 1d;
                _events.Add(new CombatEvent(
                    _nextEventSequence++,
                    CombatEventType.Heal,
                    attacker.ActorId,
                    attacker.ActorId,
                    1d));
            }
        }

        private void ResolveExecutionCasts(CombatActor caster, double deltaSeconds)
        {
            caster.AttackProgress += deltaSeconds;
            var interval = Math.Max(0.1d, caster.Definition.AttackIntervalSeconds);
            var castCount = (int)Math.Floor(caster.AttackProgress / interval);
            if (castCount <= 0)
            {
                return;
            }

            for (var index = 0; index < castCount; index++)
            {
                var target = _actors
                    .Where(candidate =>
                        candidate.IsAlive &&
                        candidate.Team != caster.Team &&
                        !candidate.Definition.IsBuilding &&
                        !(caster.Team == Team.Enemy && IsStealthActor(candidate)) &&
                        ControlBlockDistance(caster, candidate) <= Math.Max(1, caster.Definition.Range))
                    .OrderBy(candidate => candidate.HitPoints)
                    .ThenBy(candidate => ControlBlockDistance(caster, candidate))
                    .ThenBy(candidate =>
                        Math.Abs(candidate.Column - caster.Column) +
                        Math.Abs(candidate.LanePosition - caster.LanePosition))
                    .FirstOrDefault();
                if (target == null)
                {
                    break;
                }

                var executedHitPoints = target.HitPoints;
                _events.Add(new CombatEvent(
                    _nextEventSequence++,
                    CombatEventType.Attack,
                    caster.ActorId,
                    target.ActorId,
                    executedHitPoints));
                target.HitPoints = 0d;
            }

            caster.AttackProgress = Math.Max(0d, caster.AttackProgress - castCount * interval);
        }

        private static bool IsStealthActor(CombatActor actor)
        {
            return actor != null && actor.Definition.IsStealth;
        }

        private void ResolveEnemyTerritoryBreaches(BattleGrid grid)
        {
            if (grid == null)
            {
                return;
            }

            foreach (var enemy in _actors.Where(actor =>
                         actor.IsAlive &&
                         actor.Team == Team.Enemy &&
                         !actor.Definition.IsBuilding).ToList())
            {
                var position = ToDiscretePosition(enemy);
                var cell = grid.GetCell(position);
                if (cell == null || !cell.IsOwned)
                {
                    continue;
                }

                var enemyBlock = grid.GetControlBlockForCell(position);
                var defended = _actors.Any(actor =>
                    actor.IsAlive &&
                    actor.Team == Team.Ally &&
                    actor.Definition.IsMovingMelee &&
                    SameControlBlock(grid, enemyBlock, ToDiscretePosition(actor)));
                if (defended)
                {
                    continue;
                }

                grid.SetPollution(position, true);
                if (enemyBlock != null)
                {
                    foreach (var affectedCell in enemyBlock.Cells)
                    {
                        affectedCell.IsOwned = false;
                    }
                }

                _events.Add(new CombatEvent(
                    _nextEventSequence++,
                    CombatEventType.Pollution,
                    enemy.ActorId,
                    string.Empty,
                    0d,
                    position.Column,
                    position.Row,
                    enemyBlock?.Position.Column ?? 0,
                    enemyBlock?.Position.Row ?? 0));
                enemy.HitPoints = 0d;
            }
        }

        private void ReportDeaths()
        {
            foreach (var actor in _actors.Where(candidate => !candidate.IsAlive && !candidate.DeathReported))
            {
                actor.DeathReported = true;
                actor.DeathCleanupTicksRemaining = 2;
                _events.Add(new CombatEvent(
                    _nextEventSequence++,
                    CombatEventType.Death,
                    actor.ActorId,
                    string.Empty,
                    0d));
            }
        }

        private void AdvanceDeathCleanupAndSpawnEchoes()
        {
            var echoSources = new List<CombatActor>();
            foreach (var actor in _actors.Where(candidate =>
                         candidate.DeathReported &&
                         !candidate.DeathEchoSpawned &&
                         candidate.DeathCleanupTicksRemaining > 0))
            {
                actor.DeathCleanupTicksRemaining--;
                if (actor.DeathCleanupTicksRemaining == 0 &&
                    string.Equals(actor.Definition.Id, "E11", StringComparison.OrdinalIgnoreCase))
                {
                    actor.DeathEchoSpawned = true;
                    echoSources.Add(actor);
                }
            }

            foreach (var source in echoSources)
            {
                _deathEchoSequence++;
                TryAddActor(
                    source.ActorId + "-echo-" + _deathEchoSequence,
                    source.Team,
                    source.Definition,
                    source.Column,
                    Math.Max(1d, Math.Min(_rows, source.LanePosition)));
            }
        }

        private CombatActor FindForwardTarget(
            CombatActor actor,
            IReadOnlyDictionary<string, double> positions,
            bool requireAttackRange)
        {
            var actorLane = positions == null ? actor.LanePosition : positions[actor.ActorId];
            CombatActor selected = null;
            var selectedDistance = double.MaxValue;
            foreach (var candidate in _actors)
            {
                if (!candidate.IsAlive ||
                    candidate.Team == actor.Team ||
                    !SharesCombatColumn(actor, candidate) ||
                    (actor.Team == Team.Enemy && IsStealthActor(candidate)) ||
                    (actor.Definition.IsMovingMelee && candidate.Definition.IsMovingMelee))
                {
                    continue;
                }

                var candidateLane = positions == null ? candidate.LanePosition : positions[candidate.ActorId];
                var signedDistance = (candidateLane - actorLane) * Direction(actor.Team);
                if (signedDistance < -ContactEpsilon)
                {
                    continue;
                }

                var distance = Math.Abs(candidateLane - actorLane);
                var actorRow = ToDiscretePosition(actor, positions).Row;
                var candidateRow = ToDiscretePosition(candidate, positions).Row;
                var blockDistance = Math.Abs(
                    GetControlBlockRow(candidateRow) - GetControlBlockRow(actorRow));
                var inRange = actor.Definition.IsMovingMelee
                    ? distance <= ContactDistance + ContactEpsilon
                    : blockDistance <= Math.Max(1, actor.Definition.Range);
                if (requireAttackRange && !inRange)
                {
                    continue;
                }

                var targetPriority = blockDistance * (_rows + 1d) + distance;
                if (candidate.Definition.IsBuilding)
                {
                    targetPriority -= 0.25d;
                }

                if (targetPriority < selectedDistance)
                {
                    selected = candidate;
                    selectedDistance = targetPriority;
                }
            }

            return selected;
        }

        private static bool SharesCombatColumn(CombatActor first, CombatActor second)
        {
            return GetControlBlockColumn(first.Column) == GetControlBlockColumn(second.Column);
        }

        private static bool OccupiesColumn(CombatActor actor, int column)
        {
            if (!actor.Definition.IsBuilding)
            {
                return actor.Column == column;
            }

            var startColumn = GetControlBlockStart(actor.Column);
            return column >= startColumn && column < startColumn + BattleGrid.ControlBlockSize;
        }

        private static int GetControlBlockStart(int cellIndex)
        {
            return ((Math.Max(1, cellIndex) - 1) / BattleGrid.ControlBlockSize) *
                   BattleGrid.ControlBlockSize + 1;
        }

        private int ControlBlockDistance(CombatActor first, CombatActor second)
        {
            if (first == null || second == null)
            {
                return int.MaxValue;
            }

            var firstPosition = ToDiscretePosition(first);
            var secondPosition = ToDiscretePosition(second);
            return Math.Abs(GetControlBlockColumn(firstPosition.Column) -
                            GetControlBlockColumn(secondPosition.Column)) +
                   Math.Abs(GetControlBlockRow(firstPosition.Row) -
                            GetControlBlockRow(secondPosition.Row));
        }

        private bool SameControlBlock(CombatActor first, CombatActor second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            var firstPosition = ToDiscretePosition(first);
            var secondPosition = ToDiscretePosition(second);
            return GetControlBlockColumn(firstPosition.Column) == GetControlBlockColumn(secondPosition.Column) &&
                   GetControlBlockRow(firstPosition.Row) == GetControlBlockRow(secondPosition.Row);
        }

        private double ClampLane(double lanePosition)
        {
            return Math.Max(1d, Math.Min(_rows, lanePosition));
        }

        private GridPosition ToDiscretePosition(
            CombatActor actor,
            IReadOnlyDictionary<string, double> positions = null)
        {
            var lanePosition = GetLanePosition(actor, positions);
            var row = actor.Team == Team.Ally
                ? (int)Math.Floor(lanePosition + ContactEpsilon)
                : (int)Math.Ceiling(lanePosition - ContactEpsilon);
            return new GridPosition(actor.Column, Math.Max(1, Math.Min(_rows, row)));
        }

        private static bool IsMovingMelee(CombatActor actor)
        {
            return actor != null && actor.IsAlive && actor.Definition.IsMovingMelee;
        }

        private static double GetLanePosition(
            CombatActor actor,
            IReadOnlyDictionary<string, double> positions)
        {
            return positions == null ? actor.LanePosition : positions[actor.ActorId];
        }

        private static int GetControlBlockColumn(int column)
        {
            return ((Math.Max(1, column) - 1) / BattleGrid.ControlBlockSize) + 1;
        }

        private static int GetControlBlockRow(int row)
        {
            return ((Math.Max(1, row) - 1) / BattleGrid.ControlBlockSize) + 1;
        }

        private static int GetControlBlockStartRow(int controlBlockRow)
        {
            return ((Math.Max(1, controlBlockRow) - 1) * BattleGrid.ControlBlockSize) + 1;
        }

        private int GetControlBlockEndRow(int controlBlockRow)
        {
            return Math.Min(_rows, GetControlBlockStartRow(controlBlockRow) + BattleGrid.ControlBlockSize - 1);
        }

        private int GetControlBlockCenterColumn(int controlBlockColumn)
        {
            var start = ((Math.Max(1, controlBlockColumn) - 1) * BattleGrid.ControlBlockSize) + 1;
            return Math.Min(_columns, start + 1);
        }

        private int GetControlBlockCenterRow(int controlBlockRow)
        {
            return Math.Min(_rows, GetControlBlockStartRow(controlBlockRow) + 1);
        }

        private static double ResolveArrivalSeconds(double distance, double speed)
        {
            if (distance <= ContactEpsilon)
            {
                return 0d;
            }

            return speed <= 0.0001d ? double.PositiveInfinity : distance / speed;
        }

        private static double MoveTowards(double current, double target, double maximumDelta)
        {
            if (Math.Abs(target - current) <= maximumDelta)
            {
                return target;
            }

            return current + Math.Sign(target - current) * Math.Max(0d, maximumDelta);
        }

        private static string BuildMeleeBattlefieldKey(int controlBlockColumn, int controlBlockRow)
        {
            return controlBlockColumn + ":" + controlBlockRow;
        }

        private static bool SameControlBlock(
            BattleGrid grid,
            ControlBlock expected,
            GridPosition position)
        {
            var actual = grid.GetControlBlockForCell(position);
            return expected != null && actual != null && expected.Position.Equals(actual.Position);
        }

        private static int Direction(Team team)
        {
            return team == Team.Ally ? 1 : -1;
        }
    }
}
