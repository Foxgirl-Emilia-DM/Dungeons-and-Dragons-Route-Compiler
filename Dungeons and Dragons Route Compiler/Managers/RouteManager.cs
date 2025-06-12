// RouteManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YourFantasyWorldProject.Classes;
using YourFantasyWorldProject.Utils; // For ConsoleInput
using YourFantasyWorldProject.Pathfinding; // Added for Pathfinder reference
using YourFantasyWorldProject.Managers; // Ensure DataManager is accessible

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
            LoadAllRoutes(); // Load all data at startup
        }

        // --- Internal Helper: Load All Routes ---
        // This method should be called periodically or when data is modified outside the manager
        private void LoadAllRoutes() // This method remains private as it's an internal helper
        {
            _allLandRoutes.Clear();
            _allSeaRoutes.Clear();

            var allRegions = _dataManager.GetAllRegions();
            Console.WriteLine($"Loading routes from {allRegions.Count} regions...");

            foreach (var region in allRegions)
            {
                Console.WriteLine($"  Loading routes for region: {region}");
                _allLandRoutes.AddRange(_dataManager.LoadLandRoutesFromRegionFile(region));
                _allSeaRoutes.AddRange(_dataManager.LoadSeaRoutesFromRegionFile(region));
            }

            Console.WriteLine($"Finished loading. Total land routes: {_allLandRoutes.Count}, Total sea routes: {_allSeaRoutes.Count}.");
            RebuildGraph(); // Rebuild graph after loading all routes
        }

        /// <summary>
        /// Displays the currently loaded routes and settlements.
        /// </summary>
        public void DisplayLoadedData()
        {
            Console.WriteLine("\n--- Currently Loaded Route Data ---");
            LoadAllRoutes(); // Ensure all routes are loaded before displaying
            Console.WriteLine($"Current in-memory data: {_allLandRoutes.Count} land routes and {_allSeaRoutes.Count} sea routes.");

            // Calculate and display unique settlements
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

            Console.WriteLine($"Total number of unique settlements across all files: {uniqueSettlements.Count}");
            // Optionally, list them
            // Console.WriteLine("Unique Settlements:");
            // foreach (var s in uniqueSettlements.OrderBy(s => s.Region).ThenBy(s => s.Name))
            // {
            //     Console.WriteLine($"  - {s}");
            // }
        }

        /// <summary>
        /// Triggers the graph rebuilding process in the Pathfinder.
        /// This should be called after any modifications to the route data.
        /// </summary>
        public void RebuildGraph()
        {
            _pathfinder.BuildGraph(_allLandRoutes, _allSeaRoutes);
            Console.WriteLine("Graph rebuilt successfully.");
        }

        // --- New Methods to address Program.cs errors and user requests ---

        /// <summary>
        /// Allows the DM to add a new route (land or sea) with an option to save as custom or default.
        /// </summary>
        public void AddRoute()
        {
            bool isDone = false;
            do
            {
                Console.WriteLine("\n--- Add New Route ---");
                Console.WriteLine("Select Route Type:");
                Console.WriteLine("1. Land Route");
                Console.WriteLine("2. Sea Route");
                Console.WriteLine("3. Exit");
                string choice = ConsoleInput.GetStringInput("Enter your choice: ");

                switch (choice)
                {
                    case "1":
                        _pathfinder.CreateDmLandRoute(); // Call the updated method
                        break;
                    case "2":
                        _pathfinder.CreateDmSeaRoute(); // Call the updated method
                        break;
                    case "3":
                        break;
                    default:
                        Console.WriteLine("Invalid route type choice.");
                        break;
                }
                LoadAllRoutes(); // Reload all routes to include the newly added one
            } while (!isDone);
        }

        /// <summary>
        /// Placeholder for removing an existing route.
        /// This would involve identifying the route (e.g., by origin/destination/type),
        /// removing it from in-memory lists, and then updating the file via DataManager.
        /// </summary>
        public void RemoveRoute()
        {
            Console.WriteLine("\n--- Remove Route (Not Implemented) ---");
            Console.WriteLine("This feature is not yet implemented. Future development would allow removing routes by specifying origin, destination, and type.");
            // Implementation would involve:
            // 1. Prompting for origin, destination, and route type.
            // 2. Finding the specific route in _allLandRoutes or _allSeaRoutes.
            // 3. Removing it from the list.
            // 4. Calling a method in DataManager to rewrite the relevant file without the removed route.
            // 5. Calling LoadAllRoutes() and RebuildGraph().
        }

        /// <summary>
        /// Placeholder for editing an existing route.
        /// This would involve identifying the route, allowing modifications,
        /// and then updating the file via DataManager.
        /// </summary>
        public void EditRoute()
        {
            Console.WriteLine("\n--- Edit Route (Not Implemented) ---");
            Console.WriteLine("This feature is not yet implemented. Future development would allow editing route details (e.g., distance, biomes).");
            // Implementation would involve:
            // 1. Prompting for route to edit (similar to RemoveRoute).
            // 2. Displaying current route details.
            // 3. Prompting for new details.
            // 4. Updating the route object and then rewriting the file via DataManager.
            // 5. Calling LoadAllRoutes() and RebuildGraph().
        }

        /// <summary>
        /// Prompts for a region file and performs validation on it.
        /// </summary>
        public void FileValidation()
        {
            Console.WriteLine("\n--- File Validation ---");
            string regionName = ConsoleInput.GetStringInput("Enter region name for file validation: ");
            RouteType routeType = ConsoleInput.GetEnumInput<RouteType>("Select route type to validate:");

            // Determine file path based on type and preference (checking default then custom)
            string filePath = GetRouteFilePath(regionName, routeType, preferCustom: false) ?? // Check default first
                              GetRouteFilePath(regionName, routeType, preferCustom: true);   // Then check custom

            if (filePath == null)
            {
                Console.WriteLine($"Could not find a file for region '{regionName}' of type '{routeType}'.");
                return;
            }

            Console.WriteLine($"Validating file: {filePath}");
            List<string> issues = _dataManager.ValidateAndRepairFile(filePath, routeType);

            if (issues.Any())
            {
                Console.WriteLine("\nValidation Issues Found:");
                foreach (var issue in issues)
                {
                    Console.WriteLine($"- {issue}");
                }
            }
            else
            {
                Console.WriteLine("File appears valid and no issues were found.");
            }
        }

        /// <summary>
        /// Prompts for a region file and attempts to repair it.
        /// Calls the same DataManager method as validation, which handles repair internally.
        /// </summary>
        public void FileRepair()
        {
            Console.WriteLine("\n--- File Repair ---");
            string regionName = ConsoleInput.GetStringInput("Enter region name for file repair: ");
            RouteType routeType = ConsoleInput.GetEnumInput<RouteType>("Select route type to repair:");

            string filePath = GetRouteFilePath(regionName, routeType, preferCustom: false) ??
                              GetRouteFilePath(regionName, routeType, preferCustom: true);

            if (filePath == null)
            {
                Console.WriteLine($"Could not find a file for region '{regionName}' of type '{routeType}'.");
                return;
            }

            Console.WriteLine($"Attempting to repair file: {filePath}");
            List<string> issues = _dataManager.ValidateAndRepairFile(filePath, routeType); // This method also performs repairs

            if (issues.Any())
            {
                Console.WriteLine("\nRepair Report:");
                foreach (var issue in issues)
                {
                    Console.WriteLine($"- {issue}");
                }
            }
            else
            {
                Console.WriteLine("File either already valid or no repairs were possible/necessary.");
            }
            LoadAllRoutes(); // Reload all routes after potential repair
        }


        /// <summary>
        /// Prompts for a region file and prints its content to the console.
        /// </summary>
        public void FilePrint()
        {
            Console.WriteLine("\n--- Print File Content ---");
            string regionName = ConsoleInput.GetStringInput("Enter region name to print file content for: ");
            RouteType routeType = ConsoleInput.GetEnumInput<RouteType>("Select route type to print:");

            string filePath = GetRouteFilePath(regionName, routeType, preferCustom: false) ??
                              GetRouteFilePath(regionName, routeType, preferCustom: true);

            if (filePath == null)
            {
                Console.WriteLine($"Could not find a file for region '{regionName}' of type '{routeType}'.");
                return;
            }

            _dataManager.PrintFileContent(filePath);
        }

        /// <summary>
        /// Helper method to construct and check file paths.
        /// </summary>
        /// <param name="regionName">Name of the region.</param>
        /// <param name="routeType">Type of route (Land/Sea).</param>
        /// <param name="preferCustom">If true, checks custom path first. If false, checks default path first.</param>
        /// <returns>The existing file path, or null if not found.</returns>
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


        /// <summary>
        /// Displays information about the total unique settlements loaded in memory.
        /// This is essentially a wrapper for DisplayLoadedData.
        /// </summary>
        public void FileCheck()
        {
            DisplayLoadedData();
        }
    }
}
