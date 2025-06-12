// Interfaces.cs
using YourFantasyWorldProject.Classes; // Ensure Settlement is recognized

namespace YourFantasyWorldProject.Classes
{
    public interface IRoute
    {
        // All routes must have an Origin and a Destination
        Settlement Origin { get; }
        Settlement Destination { get; }
    }
}
