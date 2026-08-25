using System;

namespace CompanyWarRE.Domain
{
    public enum Team
    {
        Ally,
        Enemy
    }

    public enum UnitFootprint
    {
        SmallCell,
        ControlBlock
    }

    public enum DeploymentMode
    {
        StandardUnit,
        Building,
        SupportEffect,
        TerrainBuild,
        OuterRing
    }

    public readonly struct GridPosition : IEquatable<GridPosition>
    {
        public GridPosition(int column, int row)
        {
            Column = column;
            Row = row;
        }

        public int Column { get; }
        public int Row { get; }

        public bool Equals(GridPosition other)
        {
            return Column == other.Column && Row == other.Row;
        }

        public override bool Equals(object obj)
        {
            return obj is GridPosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Column * 397) ^ Row;
            }
        }

        public override string ToString()
        {
            return $"({Column}, {Row})";
        }
    }

    public sealed class UnitDefinition
    {
        public UnitDefinition(
            string id,
            int resourceCost,
            double deploymentCooldownSeconds,
            DeploymentMode deploymentMode = DeploymentMode.StandardUnit,
            UnitFootprint footprint = UnitFootprint.SmallCell,
            string name = "",
            string effect = "",
            double resourceRate = 0d,
            double scoreRate = 0d,
            bool canDeployOutside = false)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A unit id is required.", nameof(id));
            }

            Id = id;
            ResourceCost = Math.Max(0, resourceCost);
            DeploymentCooldownSeconds = Math.Max(0d, deploymentCooldownSeconds);
            DeploymentMode = deploymentMode;
            Footprint = footprint;
            Name = name ?? string.Empty;
            Effect = effect ?? string.Empty;
            ResourceRate = Math.Max(0d, resourceRate);
            ScoreRate = Math.Max(0d, scoreRate);
            CanDeployOutside = canDeployOutside;
        }

        public string Id { get; }
        public int ResourceCost { get; }
        public double DeploymentCooldownSeconds { get; }
        public DeploymentMode DeploymentMode { get; }
        public UnitFootprint Footprint { get; }
        public string Name { get; }
        public string Effect { get; }
        public double ResourceRate { get; }
        public double ScoreRate { get; }
        public bool CanDeployOutside { get; }
        public int DeploymentEchoCount =>
            string.Equals(Effect, "DeployEcho1", StringComparison.OrdinalIgnoreCase)
                ? 1
                : string.Equals(Effect, "DeployEcho3", StringComparison.OrdinalIgnoreCase)
                    ? 3
                    : 0;
    }
}
