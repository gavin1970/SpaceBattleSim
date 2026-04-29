namespace DynamicTimeDraw
{
    public class ShipStats
    {
        static private readonly Dictionary<ShipType, (uint Shields, uint Power, uint HitBox, float Speed)> _shipsAvailable = 
                            new Dictionary<ShipType, (uint Shields, uint Power, uint HitBox, float Speed)>()
        {
            { ShipType.TowRig, (2000, 1, 50, 2.0f) },
            { ShipType.Transport, (400, 0, 0, 2.0f) },
            { ShipType.Raider, (200, 4, 50, 1.0f) },
            { ShipType.Fighter, (200, 4, 50, 1.0f) },
            { ShipType.Bomber, (400, 4, 60, 0.5f) },
            { ShipType.Capital, (800, 4, 75, 0.3f) }
        };

        private uint _shields = 0;
        private uint _power = 0;
        private float _speed = 0.0f;
        private uint _hitbox = 0;

        public ShipStats(ShipType type)
        {
            Type = type;
            if (_shipsAvailable.TryGetValue(type, out (uint shields, uint power, uint hitBox, float speed) value))
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
        public float Speed { get { return _speed; } }
        public uint Hitbox { get { return _hitbox; } }
    }
}
