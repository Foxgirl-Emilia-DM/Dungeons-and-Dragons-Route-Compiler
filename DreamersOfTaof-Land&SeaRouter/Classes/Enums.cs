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
        SailingShip // 2 mph
    }

    // Defines different types of mounts and their typical speeds
    public enum MountType
    {
        None,           // For unmounted travel
        RidingHorse,    // 6 mph
        Warhorse        // 6 mph
        // Add more mounts as needed
    }

    // Defines user preference for route finding
    public enum RoutePreference
    {
        LandOnly,
        SeaOnly,
        Mixed // Allows a combination of land and sea routes
    }
}