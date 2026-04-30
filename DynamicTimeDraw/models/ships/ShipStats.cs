using System.Text;

namespace DynamicTimeDraw
{
    public enum RecoverOrder
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }
    public class ShipStats
    {
        static private readonly Dictionary<ShipType, (uint Shields, uint Power, uint HitBox, float Speed, RecoverOrder Recovery)> _shipsAvailable = 
                            new Dictionary<ShipType, (uint Shields, uint Power, uint HitBox, float Speed, RecoverOrder Recovery)>()
        {
            { ShipType.TowRig, (500, 1, 30, 2.0f, RecoverOrder.Critical) },
            { ShipType.Transport, (1000, 0, 40, 2.0f, RecoverOrder.Low) },
            { ShipType.Raider, (400, 16, 50, 1.0f, RecoverOrder.None) },
            { ShipType.Fighter, (200, 4, 50, 1.0f, RecoverOrder.Low) },
            { ShipType.Bomber, (400, 6, 60, 0.5f, RecoverOrder.Medium) },
            { ShipType.Capital, (800, 8, 75, 0.3f, RecoverOrder.High) }
        };

        private uint _shields = 0;
        private uint _power = 0;
        private float _speed = 0.0f;
        private uint _hitbox = 0;
        private RecoverOrder _recovery = RecoverOrder.None;

        public ShipStats(ShipType type)
        {
            Type = type;
            if (_shipsAvailable.TryGetValue(type, out (uint shields, uint power, uint hitBox, float speed, RecoverOrder recovery) value))
            {
                _shields = value.shields;
                _power = value.power;
                _speed = value.speed;
                _hitbox = value.hitBox;
                _recovery = value.recovery;
            }
        }
        public ShipType Type { get; }
        public uint Shields { get { return _shields; } }
        public uint Power { get { return _power; } }
        public RecoverOrder Recovery { get { return _recovery; } }
        public float Speed { get { return _speed; } }
        public uint Hitbox { get { return _hitbox; } }
    }
}
