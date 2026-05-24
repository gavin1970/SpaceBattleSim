namespace SpaceBattleSim
{
    /// <summary>
    /// These values are only used for BattleStats.<br/>
    /// Specifies the type of action or event that can occur within the battle per ship. 
    /// </summary>
    /// <remarks>Use this enumeration to represent distinct actions such as kills, deaths, critical transfers,
    /// healing, near-death situations, or being under attack. The specific meaning of each value depends on the context
    /// in which it is used.</remarks>
    public enum ActionType
    {
        /// <summary>
        /// This ship stole health from another ship.
        /// </summary>
        StoleHealth,
        /// <summary>
        /// This ship killed another ship.
        /// </summary>
        Kill,
        /// <summary>
        /// This ship died by another ship.
        /// </summary>
        Death,
        /// <summary>
        /// This ship used a special ability to sacrifice power to transfer into health.
        /// </summary>
        CriticalTransfer,
        /// <summary>
        /// This ship healed another ship or itself.
        /// </summary>
        Heal,
        /// <summary>
        /// This ship is almost dead and requires immediate attention.
        /// </summary>
        AlmostDead,
        /// <summary>
        /// This ship is currently under attack.
        /// </summary>
        UnderAttack,
        /// <summary>
        /// While this ship is being repaired, it is considered to be in a vulnerable 
        /// state, as it may not be able to defend itself effectively against enemy attacks.
        /// </summary>
        BeingRepaired,
    }

    /// <summary>
    /// Specifies the relative priority or severity for recovery operations.
    /// </summary>
    /// <remarks>Use this enumeration to indicate the order in which recovery actions should be performed,
    /// with higher values representing greater urgency. The values range from <see cref="RecoverOrder.None"/> (no
    /// recovery required) to <see cref="RecoverOrder.Critical"/> (highest priority for recovery).</remarks>
    public enum RecoverOrder
    {
        /// <summary>
        /// Not to be recovered.
        /// </summary>
        None = 0,
        /// <summary>
        /// Indicates a low level of severity or priority, typically used for grunt / Fighter ships that can 
        /// fight, but are not critical to the overall success of the mission. They can take some damage, but 
        /// are not as durable as other ship types.
        /// </summary>
        Low = 1,
        /// <summary>
        /// Indicates a medium level of severity or priority, typically used for ships that can take more 
        /// and provide heavier damaged than fighters, but less than Capital ships.
        /// </summary>
        Medium = 2,
        /// <summary>
        /// Indicates a high level of severity or priority, typically used for ships that can take more 
        /// and provide heavier damaged than other, such as Capital ships.
        /// </summary>
        High = 3,
        /// <summary>
        /// Indicates a critical severity level, typically used for RepairRig that require immediate attention.
        /// </summary>
        /// <remarks>Use this value to represent the most severe level like healers,
        /// where the home team may be unable to continue running without immediate intervention.</remarks>
        Critical = 4
    }

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
        /// <summary>
        /// Indicates that the Fighter is currently being repaired.
        /// </summary>
        BeingRepaired = 5,
    }

    /// <summary>
    /// Specifies the operational status or current mission assignment of a ship.
    /// </summary>
    /// <remarks>Use this enumeration to indicate whether a ship is idle, actively engaged in a mission,
    /// undergoing repairs, or on standby awaiting deployment. The values can be used to control ship behavior or
    /// display status information in user interfaces.</remarks>
    public enum ShipMission
    {
        /// <summary>
        /// The ship is currently idle and not assigned to any specific task or operation.
        /// </summary>
        Idle = 0,
        /// <summary>
        /// The ship is actively engaged in a mission, which could involve combat, support, or other operational tasks.
        /// </summary>
        Active = 1,
        /// <summary>
        /// RepairRig ships are currently on a repair mission, assisting disabled ships by repairing them.
        /// </summary>
        OnRepair = 2,
        /// <summary>
        /// Heading back to home base.
        /// </summary>
        HeadingHome = 3,
    }

    /// <summary>
    /// Specifies the functional role of a ship within a fleet, such as support, combat, or utility.    
    /// </summary>
    /// <remarks>Use this enumeration to categorize ships based on their primary purpose or operational
    /// capabilities. The role determines the ship's typical functions and may influence how it is deployed or interacts
    /// with other ships.</remarks>
    public enum ShipRole
    {
        /// <summary>
        /// Support ships provide assistance to other ships, such as repairs, resupply. 
        /// They are not designed for combat and may have limited offensive capabilities.
        /// </summary>
        Support = 0,
        /// <summary>
        /// Combat ships are designed for engaging in battles and offensive operations. They
        /// are equipped with weapons and armor to withstand enemy attacks and deal damage to
        /// opponents.
        /// </summary>
        Combat = 1,
        /// <summary>
        /// Utility ships perform various non-combat functions, such as reconnaissance, mining,
        /// or exploration. They may have specialized equipment for their specific tasks but are
        /// not primarily focused on combat.
        /// </summary>
        Utility = 2
    }

    /// <summary>
    /// Represents the different types of spaceships, each with unique characteristics and roles.
    /// </summary>
    public enum ShipType
    {
        /// <summary>
        /// RepairRig ships are specialized in repairing ships after they are disabled.  They have no 
        /// guns and will not be fired apon, but they are also very slow and have low shields. 
        /// They are used to salvage disabled ships and bring them back to HomeBase for tear 
        /// down for parts.
        /// </summary>
        RepairRig = 0,
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
