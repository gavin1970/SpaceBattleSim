namespace DynamicTimeDraw
{
    /// <summary>
    /// Represents the various statuses a spaceship can have, ranging from fully operational to completely destroyed.
    /// </summary>
    public enum ShipStatus
    {
        /// <summary>
        /// Dead: The ship is completely destroyed and non-functional. It cannot be
        /// repaired or used in any capacity.
        /// </summary>
        Dead = 0,
        /// <summary>
        /// Critical: The ship is severely damaged and on the verge of destruction.
        /// Immediate repairs are necessary to prevent total loss.
        /// </summary>
        Critical = 1,
        /// <summary>
        /// Damaged: The ship has sustained significant damage but is still
        /// operational. Repairs are recommended to restore full functionality.
        /// </summary>
        Damaged = 2,
        /// <summary>
        /// Scratched: The ship has minor damage but is still fully operational. No
        /// immediate repairs are necessary.
        /// </summary>
        Scratched = 3,
        /// <summary>
        /// Operational: The ship is fully functional and capable of performing all its
        /// intended operations.
        /// </summary>
        Operational = 4,
    }

    /// <summary>
    /// Represents the different types of spaceships, each with unique characteristics and roles.
    /// </summary>
    public enum ShipType
    {
        /// <summary>
        /// TowRig ships are specialized in towing ships after they are disabled.  They have no 
        /// guns and will not be fired apon, but they are also very slow and have low shields. 
        /// They are used to salvage disabled ships and bring them back to HomeBase for tear 
        /// down for parts.
        /// </summary>
        TowRig = 0,
        /// <summary>
        /// Transport ships are designed for carrying cargo and personnel. They have moderate 
        /// shields and power, but are not equipped for combat. They are used to transport 
        /// goods and people between locations, and may be vulnerable to attacks due to their 
        /// lack of offensive capabilities.
        /// </summary>
        Transport = 1,
        /// <summary>
        /// Raider are specialized in hit-and-run tactics and quick strikes.  Fighters will be 
        /// dispatched to intercept and engage Raider, while bombers will be deployed to target 
        /// Raider with precision strikes. Raider are designed for speed and agility, allowing 
        /// them to quickly strike and retreat before enemy forces can respond effectively.
        /// </summary>
        Raider = 3,
        /// <summary>
        /// Fighters are specialized in combat and offensive operations.
        /// </summary>
        Fighter = 6,
        /// <summary>
        /// Bombers are specialized in aerial attacks and strategic bombing missions.
        /// </summary>
        Bomber = 8,
        /// <summary>
        /// Capital ships are heavily armored and equipped with powerful weaponry.<br/>
        /// Theyserve as the backbone of a fleet, providing both offensive and defensive<br/>
        /// capabilities.
        /// </summary>
        Capital = 10
    }
}
