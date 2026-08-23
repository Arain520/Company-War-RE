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
            int assaultScoreReward = 0)
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
        }

        public string Id { get; }
        public string Type { get; }
        public int Durability { get; }
        public double Attack { get; }
        public double Speed { get; }
        public double AttackIntervalSeconds { get; }
        public int Range { get; }
        public int AssaultScoreReward { get; }
        public bool IsMovingMelee =>
            Speed > 0d && Range <= 1 && string.Equals(Type, "Staff", StringComparison.OrdinalIgnoreCase);
    }

    public enum CombatEventType
    {
        Attack,
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
        internal CombatActorSnapshot(CombatActor actor)
        {
            ActorId = actor.ActorId;
            TemplateId = actor.Definition.Id;
            Team = actor.Team;
            Column = actor.Column;
            LanePosition = actor.LanePosition;
            HitPoints = Math.Max(0d, actor.HitPoints);
            MaximumHitPoints = actor.Definition.Durability;
            AttackProgress = actor.AttackProgress;
            IsAlive = actor.IsAlive;
        }

        public string ActorId { get; }
        public string TemplateId { get; }
        public Team Team { get; }
        public int Column { get; }
        public double LanePosition { get; }
        public double HitPoints { get; }
        public double MaximumHitPoints { get; }
        public double AttackProgress { get; }
        public bool IsAlive { get; }
    }

    internal sealed class CombatActor
    {
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
        }

        public string ActorId { get; }
        public Team Team { get; }
        public CombatantDefinition Definition { get; }
        public int Column { get; }
        public double LanePosition { get; set; }
        public double HitPoints { get; set; }
        public double AttackProgress { get; set; }
        public bool DeathReported { get; set; }
        public bool IsAlive => HitPoints > 0d;
    }

    public sealed class CombatSimulation
    {
        public const double ContactDistance = 0.12d;
        public const double ContactEpsilon = 0.02d;

        private readonly int _columns;
        private readonly int _rows;
        private readonly List<CombatActor> _actors = new List<CombatActor>();
        private readonly List<CombatEvent> _events = new List<CombatEvent>();
        private long _nextEventSequence = 1;

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

        public bool ContainsActor(string actorId)
        {
            return !string.IsNullOrWhiteSpace(actorId) &&
                   _actors.Any(actor => string.Equals(actor.ActorId, actorId, StringComparison.Ordinal));
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
            MoveActors(delta);
            ResolveEnemyTerritoryBreaches(grid);
            ResolveAttacks(delta);
            ReportDeaths();
        }

        public IReadOnlyList<CombatActorSnapshot> CreateSnapshot()
        {
            return _actors
                .OrderBy(actor => actor.ActorId, StringComparer.Ordinal)
                .Select(actor => new CombatActorSnapshot(actor))
                .ToArray();
        }

        private void MoveActors(double deltaSeconds)
        {
            var positions = _actors.ToDictionary(actor => actor.ActorId, actor => actor.LanePosition);
            foreach (var actor in _actors.Where(candidate => candidate.IsAlive && candidate.Definition.IsMovingMelee))
            {
                var target = FindForwardTarget(actor, positions, false);
                if (target == null)
                {
                    actor.LanePosition = ClampLane(
                        actor.LanePosition + Direction(actor.Team) * actor.Definition.Speed * deltaSeconds);
                    continue;
                }

                var separation = Math.Abs(positions[target.ActorId] - positions[actor.ActorId]);
                var remaining = Math.Max(0d, separation - ContactDistance);
                if (remaining <= 0d)
                {
                    continue;
                }

                var targetSpeed = target.Definition.IsMovingMelee ? target.Definition.Speed : 0d;
                var totalSpeed = actor.Definition.Speed + targetSpeed;
                var actorShare = totalSpeed <= 0d
                    ? 0d
                    : remaining * actor.Definition.Speed / totalSpeed;
                var movement = Math.Min(actor.Definition.Speed * deltaSeconds, actorShare);
                actor.LanePosition = ClampLane(
                    actor.LanePosition + Direction(actor.Team) * movement);
            }
        }

        private void ResolveAttacks(double deltaSeconds)
        {
            var pendingDamage = new Dictionary<CombatActor, double>();
            foreach (var actor in _actors.Where(candidate => candidate.IsAlive))
            {
                var target = FindForwardTarget(actor, null, true);
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
                if (!pendingDamage.ContainsKey(target))
                {
                    pendingDamage[target] = 0d;
                }

                pendingDamage[target] += damage;
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

            foreach (var pair in pendingDamage)
            {
                pair.Key.HitPoints -= pair.Value;
            }
        }

        private void ResolveEnemyTerritoryBreaches(BattleGrid grid)
        {
            if (grid == null)
            {
                return;
            }

            foreach (var enemy in _actors.Where(actor => actor.IsAlive && actor.Team == Team.Enemy).ToList())
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
                _events.Add(new CombatEvent(
                    _nextEventSequence++,
                    CombatEventType.Death,
                    actor.ActorId,
                    string.Empty,
                    0d));
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
                if (!candidate.IsAlive || candidate.Team == actor.Team || candidate.Column != actor.Column)
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
                var maximumDistance = actor.Definition.IsMovingMelee
                    ? ContactDistance + ContactEpsilon
                    : actor.Definition.Range;
                if (requireAttackRange && distance > maximumDistance)
                {
                    continue;
                }

                if (distance < selectedDistance)
                {
                    selected = candidate;
                    selectedDistance = distance;
                }
            }

            return selected;
        }

        private double ClampLane(double lanePosition)
        {
            return Math.Max(1d, Math.Min(_rows, lanePosition));
        }

        private GridPosition ToDiscretePosition(CombatActor actor)
        {
            var row = actor.Team == Team.Ally
                ? (int)Math.Floor(actor.LanePosition + ContactEpsilon)
                : (int)Math.Ceiling(actor.LanePosition - ContactEpsilon);
            return new GridPosition(actor.Column, Math.Max(1, Math.Min(_rows, row)));
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
