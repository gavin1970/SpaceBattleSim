namespace DynamicTimeDraw
{
    public class ShipStats
    {
        static private readonly Dictionary<ShipType, (uint Shields, uint Power, uint HitBox, uint Speed)> _shipsAvailable = 
                            new Dictionary<ShipType, (uint Shields, uint Power, uint HitBox, uint Speed)>()
        {
            { ShipType.TowRig, (0, 0, 0, 10) },
            { ShipType.Transport, (400, 0, 0, 30) },
            { ShipType.Raider, (200, 4, 50, 100) },
            { ShipType.Fighter, (200, 4, 50, 150) },
            { ShipType.Bomber, (400, 4, 60, 65) },
            { ShipType.Capital, (800, 4, 75, 25) }
        };

        private uint _shields = 0;
        private uint _power = 0;
        private uint _speed = 0;
        private uint _hitbox = 0;

        public ShipStats(ShipType type)
        {
            Type = type;
            if (_shipsAvailable.TryGetValue(type, out (uint shields, uint power, uint hitBox, uint speed) value))
            {
                _shields = value.shields;
                _power = value.power;
                _speed = value.speed;
                _hitbox = value.hitBox;
            }
        }
        public ShipType Type { get; }
        public uint Shields { get { return _shields; } }
        public uint Power { get { return _power; } }
        public uint Speed { get { return _speed; } }
        public uint Hitbox { get { return _hitbox; } }
    }
}
