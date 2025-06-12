// RouteManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YourFantasyWorldProject.Classes;
using YourFantasyWorldProject.Utils; // For ConsoleInput
using YourFantasyWorldProject.Pathfinding; // Added for Pathfinder reference
using YourFantasyWorldProject.Managers; // Ensure DataManager is accessible
using System.Globalization; // Added for TextInfo

namespace YourFantasyWorldProject.Managers
{
    public class RouteManager
    {
        private readonly DataManager _dataManager;
        private readonly Pathfinder _pathfinder; // Added Pathfinder dependency
        private List<LandRoute> _allLandRoutes;
        private List<SeaRoute> _allSeaRoutes;
        public IReadOnlyList<LandRoute> AllLandRoutes => _allLandRoutes.AsReadOnly();
        public IReadOnlyList<SeaRoute> AllSeaRoutes => _allSeaRoutes.AsReadOnly();

        public RouteManager(DataManager dataManager, Pathfinder pathfinder) // Added Pathfinder to constructor
        {
            _dataManager = dataManager;
            _pathfinder = pathfinder; // Assign Pathfinder
            _allLandRoutes = new List<LandRoute>();
            _allSeaRoutes = new List<SeaRoute>();
            // Load all data at startup, after DataManager ensures initial data presence
            LoadAllRoutes();
        }

        // --- Internal Helper: Load All Routes ---
        // This method should be called periodically or when data is modified outside the manager
        private void LoadAllRoutes() // This method remains private as it's an internal helper
        {
            _allLandRoutes.Clear();
            _allSeaRoutes.Clear();
            _pathfinder.ClearGraph(); // Clear the graph before rebuilding

            Console.WriteLine("Loading all routes...");

            // Get all files in the LandRoutes and CustomRoutes/Land directories
            string defaultLandPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorldData", "LandRoutes");
            string customLandPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorldData", "CustomRoutes", "Land");

            if (Directory.Exists(defaultLandPath))
            {
                foreach (string filePath in Directory.GetFiles(defaultLandPath, "*.txt"))
                {
                    _allLandRoutes.AddRange(_dataManager.LoadRoutesFromFile(filePath, RouteType.Land).Cast<LandRoute>());
                }
            }
            if (Directory.Exists(customLandPath))
            {
                foreach (string filePath in Directory.GetFiles(customLandPath, "*.txt"))
                {
                    _allLandRoutes.AddRange(_dataManager.LoadRoutesFromFile(filePath, RouteType.Land).Cast<LandRoute>());
                }
            }


            // Get all files in the SeaRoutes and CustomRoutes/Sea directories
            string defaultSeaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorldData", "SeaRoutes");
            string customSeaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorldData", "CustomRoutes", "Sea");

            if (Directory.Exists(defaultSeaPath))
            {
                foreach (string filePath in Directory.GetFiles(defaultSeaPath, "*.txt"))
                {
                    _allSeaRoutes.AddRange(_dataManager.LoadRoutesFromFile(filePath, RouteType.Sea).Cast<SeaRoute>());
                }
            }
            if (Directory.Exists(customSeaPath))
            {
                foreach (string filePath in Directory.GetFiles(customSeaPath, "*.txt"))
                {
                    _allSeaRoutes.AddRange(_dataManager.LoadRoutesFromFile(filePath, RouteType.Sea).Cast<SeaRoute>());
                }
            }

            Console.WriteLine($"Loaded {_allLandRoutes.Count} land routes and {_allSeaRoutes.Count} sea routes.");

            // After loading, rebuild the graph in Pathfinder
            RebuildGraph();
        }


        /// <summary>
        /// Public method to save all currently loaded routes back to files.
        /// This method should be called after any modifications to routes (e.g., adding custom routes).
        /// </summary>
        public void SaveAllRoutes()
        {
            _dataManager.SaveAllRoutes(_allLandRoutes, _allSeaRoutes);
        }

        /// <summary>
        /// Rebuilds the internal graph used by the Pathfinder from the currently loaded routes.
        /// This should be called after loading new data or modifying existing routes.
        /// </summary>
        public void RebuildGraph()
        {
            Console.WriteLine("Rebuilding graph...");
            // Pass all routes to the pathfinder to build its graph
            _pathfinder.BuildGraph(_allLandRoutes, _allSeaRoutes);
            Console.WriteLine("Graph rebuilt successfully.");
        }

        /// <summary>
        /// Adds a new land route to the in-memory collection and triggers a graph rebuild and save.
        /// </summary>
        public void AddLandRoute(LandRoute route)
        {
            // Check for duplicate to prevent adding the exact same route multiple times
            if (!_allLandRoutes.Contains(route))
            {
                _allLandRoutes.Add(route);
                // Also add the reverse route if it doesn't already exist
                LandRoute reverseRoute = new LandRoute(route.Destination, route.Origin, route.Biomes, route.BiomeDistances, route.IsMapped);
                if (!_allLandRoutes.Contains(reverseRoute))
                {
                    _allLandRoutes.Add(reverseRoute);
                }
                Console.WriteLine($"Added new land route: {route}");
                RebuildGraph(); // Rebuild graph after adding new routes
                SaveAllRoutes(); // Save all routes to files after modification
            }
            else
            {
                Console.WriteLine($"Warning: Land route from {route.Origin} to {route.Destination} already exists.");
            }
        }

        /// <summary>
        /// Adds a new sea route to the in-memory collection and triggers a graph rebuild and save.
        /// </summary>
        public void AddSeaRoute(SeaRoute route)
        {
            // Check for duplicate
            if (!_allSeaRoutes.Contains(route))
            {
                _allSeaRoutes.Add(route);
                // Also add the reverse route
                SeaRoute reverseRoute = new SeaRoute(route.Destination, route.Origin, route.Distance);
                if (!_allSeaRoutes.Contains(reverseRoute))
                {
                    _allSeaRoutes.Add(reverseRoute);
                }
                Console.WriteLine($"Added new sea route: {route}");
                RebuildGraph(); // Rebuild graph after adding new routes
                SaveAllRoutes(); // Save all routes to files after modification
            }
            else
            {
                Console.WriteLine($"Warning: Sea route from {route.Origin} to {route.Destination} already exists.");
            }
        }

        /// <summary>
        /// Gets a list of all unique settlements currently loaded across all routes.
        /// </summary>
        /// <returns>A list of unique Settlement objects.</returns>
        public List<Settlement> GetAllSettlements()
        {
            HashSet<Settlement> uniqueSettlements = new HashSet<Settlement>();
            foreach (var route in _allLandRoutes)
            {
                uniqueSettlements.Add(route.Origin);
                uniqueSettlements.Add(route.Destination);
            }
            foreach (var route in _allSeaRoutes)
            {
                uniqueSettlements.Add(route.Origin);
                uniqueSettlements.Add(route.Destination);
            }
            return uniqueSettlements.ToList();
        }

        /// <summary>
        /// Finds a settlement by name (case-insensitive) across all loaded settlements.
        /// </summary>
        /// <param name="name">The name of the settlement to find.</param>
        /// <returns>The Settlement object if found, otherwise null.</returns>
        public Settlement GetSettlementByName(string name)
        {
            return GetAllSettlements().FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Finds a settlement by name and region (case-insensitive) across all loaded settlements.
        /// </summary>
        /// <param name="name">The name of the settlement to find.</param>
        /// <param name="region">The region of the settlement to find.</param>
        /// <returns>The Settlement object if found, otherwise null.</returns>
        public Settlement GetSettlementByNameAndRegion(string name, string region)
        {
            return GetAllSettlements()
                   .FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                                        s.Region.Equals(region, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Displays information about the total unique settlements loaded in memory.
        /// </summary>
        public void DisplayLoadedData()
        {
            Console.WriteLine($"\n--- Loaded Data Summary ---");
            Console.WriteLine($"Total Land Routes: {_allLandRoutes.Count}");
            Console.WriteLine($"Total Sea Routes: {_allSeaRoutes.Count}");
            Console.WriteLine($"Total Unique Settlements: {GetAllSettlements().Count}");

            // Group settlements by their invariant (uppercase) region name
            var settlementsByInvariantRegion = GetAllSettlements()
                                        .GroupBy(s => s.Region.ToUpperInvariant()) // Group by invariant region name
                                        .OrderBy(g => g.Key);

            foreach (var group in settlementsByInvariantRegion)
            {
                // To display a consistent region name, take the 'Region' property from the first settlement in the group
                // This ensures we get one of the original casings for display.
                string displayRegionName = group.First().Region;
                Console.WriteLine($"  Region: {displayRegionName} ({group.Count()} settlements)");
                foreach (var settlement in group.OrderBy(s => s.Name))
                {
                    TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
                    // Always convert the settlement name to lowercase first, then apply Title Case.
                    // This ensures consistent capitalization (e.g., TINVERKE -> Tinverke, ul-ghuz -> Ul-Ghuz).
                    // The ToTitleCase method generally handles hyphens correctly.
                    string formattedSettlementName = textInfo.ToTitleCase(settlement.Name.ToLower());
                    Console.WriteLine($"    - {formattedSettlementName}");
                }
            }
        }

        /// <summary>
        /// Displays information about the total unique settlements loaded in memory.
        /// This is essentially a wrapper for DisplayLoadedData.
        /// </summary>
        public void FileCheck()
        {
            DisplayLoadedData();
        }

        /// <summary>
        /// Helper method to find a file path for a given region and route type.
        /// Checks both default and custom route directories based on preferCustom flag.
        /// </summary>
        /// <param name="regionName">The name of the region.</param>
        /// <param name="routeType">The type of route (Land or Sea).</param>
        /// <param name="preferCustom">If true, prefers custom route files over default ones.</param>
        /// <returns>The full path to the route file, or null if not found.</returns>
        private string GetRouteFilePath(string regionName, RouteType routeType, bool preferCustom)
        {
            string baseDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorldData");
            string defaultPath = (routeType == RouteType.Land) ?
                                  Path.Combine(baseDataPath, "LandRoutes", $"{regionName.ToUpperInvariant()}.txt") :
                                  Path.Combine(baseDataPath, "SeaRoutes", $"{regionName.ToUpperInvariant()}.txt");
            string customPath = (routeType == RouteType.Land) ?
                                 Path.Combine(baseDataPath, "CustomRoutes", "Land", $"{regionName.ToUpperInvariant()}.txt") :
                                 Path.Combine(baseDataPath, "CustomRoutes", "Sea", $"{regionName.ToUpperInvariant()}.txt");

            if (preferCustom)
            {
                if (File.Exists(customPath)) return customPath;
                if (File.Exists(defaultPath)) return defaultPath;
            }
            else
            {
                if (File.Exists(defaultPath)) return defaultPath;
                if (File.Exists(customPath)) return customPath;
            }
            return null;
        }
    }
}
