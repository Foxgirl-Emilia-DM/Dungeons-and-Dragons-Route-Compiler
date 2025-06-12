// Enums.cs
namespace YourFantasyWorldProject.Classes
{
    // Defines the type of route (Land or Sea)
    public enum RouteType
    {
        Land,
        Sea
    }

    // Defines different types of ships and their typical speeds
    public enum ShipType
    {
        None,       // For when no ship is used or relevant
        Rowboat,    // 1.5 mph
        Keelboat,   // 2 mph
        Longship,   // 3 mph
        Galley,     // 4 mph (often rowed for max speed)
        SailingShip, // 2 mph
        Warship     // 5 mph (New: Added for specific scenarios)
    }

    // Defines different types of mounts and their typical speeds
    public enum MountType
    {
        None,           // For unmounted travel or when no specific mount is chosen
        Foot,           // 3 mph (Added explicitly for clarity of unmounted travel)
        DraftHorse,     // 3 mph (New: Added for drawing wagons/carriages)
        RidingHorse,    // 6 mph
        Warhorse,       // 6 mph
        Pony,           // 4 mph (New: Added)
        Mastiff         // 3 mph (New: Added)
    }

    // Defines user preference for route finding
    public enum RoutePreference
    {
        LandOnly,
        SeaOnly,
        Mixed // Allows a combination of land and sea routes
    }
}
