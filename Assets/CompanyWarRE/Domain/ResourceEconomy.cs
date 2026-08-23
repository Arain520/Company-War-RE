using System;
using System.Collections.Generic;

namespace CompanyWarRE.Domain
{
    public sealed class ResourceEconomy
    {
        private readonly Dictionary<GridPosition, int> _transmitters = new Dictionary<GridPosition, int>();
        private readonly Dictionary<string, double> _lastDeploymentTime =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        private double _elapsedSeconds;
        private double _fixedProductionInterval = 5d;
        private double _transmitterProductionInterval = 3d;
        private double _fixedProductionTimer;
        private double _transmitterProductionTimer;

        public int Resources { get; private set; }
        public int TickCount => (int)Math.Floor(_elapsedSeconds + 0.0001d);
        public double ElapsedSeconds => _elapsedSeconds;

        public void Reset(int initialResources)
        {
            Resources = Math.Max(0, initialResources);
            _elapsedSeconds = 0d;
            _fixedProductionTimer = 0d;
            _transmitterProductionTimer = 0d;
            _transmitters.Clear();
            _lastDeploymentTime.Clear();
        }

        public void ConfigureProduction(double fixedIntervalSeconds, double transmitterIntervalSeconds)
        {
            _fixedProductionInterval = Math.Max(0.01d, fixedIntervalSeconds);
            _transmitterProductionInterval = Math.Max(0.01d, transmitterIntervalSeconds);
        }

        public void RegisterTransmitter(GridPosition position, int amount = 1)
        {
            _transmitters[position] = Math.Max(1, amount);
        }

        public void UnregisterTransmitter(GridPosition position)
        {
            _transmitters.Remove(position);
        }

        public void Advance(double deltaSeconds = 1d)
        {
            var delta = Math.Max(0.0001d, deltaSeconds);
            _elapsedSeconds += delta;
            _fixedProductionTimer += delta;
            _transmitterProductionTimer += delta;

            while (_fixedProductionTimer >= _fixedProductionInterval)
            {
                Resources++;
                _fixedProductionTimer -= _fixedProductionInterval;
            }

            while (_transmitters.Count > 0 && _transmitterProductionTimer >= _transmitterProductionInterval)
            {
                foreach (var amount in _transmitters.Values)
                {
                    Resources += amount;
                }

                _transmitterProductionTimer -= _transmitterProductionInterval;
            }
        }

        public void Gain(int amount)
        {
            if (amount > 0)
            {
                Resources += amount;
            }
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (Resources < amount)
            {
                return false;
            }

            Resources -= amount;
            return true;
        }

        public bool CanDeploy(UnitDefinition unit)
        {
            if (unit == null || Resources < unit.ResourceCost)
            {
                return false;
            }

            return GetRemainingCooldown(unit) <= 0d;
        }

        public double GetRemainingCooldown(UnitDefinition unit)
        {
            if (unit == null || unit.DeploymentCooldownSeconds <= 0d)
            {
                return 0d;
            }

            if (!_lastDeploymentTime.TryGetValue(unit.Id, out var lastDeployment))
            {
                return 0d;
            }

            return Math.Max(0d, unit.DeploymentCooldownSeconds - (_elapsedSeconds - lastDeployment));
        }

        public bool TryDeploy(UnitDefinition unit)
        {
            if (!CanDeploy(unit) || !TrySpend(unit.ResourceCost))
            {
                return false;
            }

            _lastDeploymentTime[unit.Id] = _elapsedSeconds;
            return true;
        }

        public void RefundDeployment(UnitDefinition unit)
        {
            if (unit == null)
            {
                return;
            }

            Resources += unit.ResourceCost;
            _lastDeploymentTime.Remove(unit.Id);
        }
    }
}
