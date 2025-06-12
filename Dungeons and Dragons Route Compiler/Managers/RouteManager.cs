// RouteManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YourFantasyWorldProject.Classes;
using YourFantasyWorldProject.Utils; // For ConsoleInput
using YourFantasyWorldProject.Pathfinding; // Added for Pathfinder reference

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
        private void LoadAllRoutes()
        {
            _allLandRoutes.Clear();
            _allSeaRoutes.Clear();

            var allRegions = _dataManager.GetAllRegionNames();
            foreach (var region in allRegions)
            {
                var (land, sea) = _dataManager.LoadRoutes(region);
                _allLandRoutes.AddRange(land);
                _allSeaRoutes.AddRange(sea);
            }

            Console.WriteLine($"Loaded {_allLandRoutes.Count} land routes and {_allSeaRoutes.Count} sea routes across {allRegions.Count()} regions.");
        }

        // --- Add New Route ---
        public void AddRoute()
        {
            Console.WriteLine("\n--- Add New Route ---");
            RouteType type = ConsoleInput.GetEnumInput<RouteType>("Select route type:");

            Console.WriteLine("Enter Origin Settlement Name and Region:");
            string originName = ConsoleInput.GetStringInput("Origin Settlement Name: ");
            string originRegion = ConsoleInput.GetStringInput("Origin Region Name: ");
            Settlement origin = _dataManager.GetSettlementByNameAndRegion(originName, originRegion);

            Console.WriteLine("Enter Destination Settlement Name and Region:");
            string destName = ConsoleInput.GetStringInput("Destination Settlement Name: ");
            string destRegion = ConsoleInput.GetStringInput("Destination Region Name: ");
            Settlement destination = _dataManager.GetSettlementByNameAndRegion(destName, destRegion);

            if (origin == null || destination == null)
            {
                Console.WriteLine("Invalid origin or destination settlement. Please ensure both exist.");
                return;
            }

            bool isCustom = ConsoleInput.GetBooleanInput("Is this a custom route (Y/N)?");

            if (type == RouteType.Land)
            {
                List<string> biomes = new List<string>();
                List<double> biomeDistances = new List<double>();
                bool addMoreBiomes = true;
                while (addMoreBiomes)
                {
                    string biome = ConsoleInput.GetStringInput("Enter biome name (e.g., GRASSLANDS, WETLANDS):");
                    double distance = ConsoleInput.GetDoubleInput($"Enter distance in km for {biome}:", 0.1);
                    biomes.Add(biome);
                    biomeDistances.Add(distance);
                    addMoreBiomes = ConsoleInput.GetBooleanInput("Add another biome segment (Y/N)?");
                }
                bool isMapped = ConsoleInput.GetBooleanInput("Is this route mapped (Y/N)?");

                LandRoute newRoute = new LandRoute(origin, destination, biomes, biomeDistances, isMapped);

                if (isCustom)
                {
                    _dataManager.SaveCustomRoute(newRoute, RouteType.Land, origin.Region);
                }
                else
                {
                    // Add to current list for immediate use
                    _allLandRoutes.Add(newRoute);
                    // Save all routes of this region (including the new one)
                    // Ensure you're only saving the routes relevant to the origin region.
                    _dataManager.SaveRoutes(_allLandRoutes.Where(r => r.Origin.Region.Equals(origin.Region, StringComparison.OrdinalIgnoreCase)).ToList(), RouteType.Land);
                }
            }
            else // RouteType.Sea
            {
                double distance = ConsoleInput.GetDoubleInput("Enter distance in kilometers: ", 0.1);
                SeaRoute newRoute = new SeaRoute(origin, destination, distance);

                if (isCustom)
                {
                    _dataManager.SaveCustomRoute(newRoute, RouteType.Sea, origin.Region);
                }
                else
                {
                    // Add to current list for immediate use
                    _allSeaRoutes.Add(newRoute);
                    // Save all routes of this region (including the new one)
                    // Ensure you're only saving the routes relevant to the origin region.
                    _dataManager.SaveRoutes(_allSeaRoutes.Where(r => r.Origin.Region.Equals(origin.Region, StringComparison.OrdinalIgnoreCase)).ToList(), RouteType.Sea);
                }
            }
            Console.WriteLine("Route added successfully! Remember to rebuild the graph for it to be included in pathfinding.");
            // RebuildGraph(); // Handled by Program.cs now
        }

        // --- Remove Route ---
        public void RemoveRoute()
        {
            Console.WriteLine("\n--- Remove Route ---");
            RouteType type = ConsoleInput.GetEnumInput<RouteType>("Select route type to remove:");

            Console.WriteLine("Enter Origin Settlement Name and Region for the route to remove:");
            string originName = ConsoleInput.GetStringInput("Origin Settlement Name: ");
            string originRegion = ConsoleInput.GetStringInput("Origin Region Name: ");

            Console.WriteLine("Enter Destination Settlement Name and Region for the route to remove:");
            string destName = ConsoleInput.GetStringInput("Destination Settlement Name: ");
            string destRegion = ConsoleInput.GetStringInput("Destination Region Name: ");

            bool removed = false;

            if (type == RouteType.Land)
            {
                var routesToRemove = _allLandRoutes.Where(r =>
                    r.Origin.Name.Equals(originName, StringComparison.OrdinalIgnoreCase) &&
                    r.Origin.Region.Equals(originRegion, StringComparison.OrdinalIgnoreCase) &&
                    r.Destination.Name.Equals(destName, StringComparison.OrdinalIgnoreCase) &&
                    r.Destination.Region.Equals(destRegion, StringComparison.OrdinalIgnoreCase)
                ).ToList();

                if (routesToRemove.Any())
                {
                    foreach (var route in routesToRemove)
                    {
                        _allLandRoutes.Remove(route);
                        removed = true;
                    }
                    // Save the modified list for the region
                    _dataManager.SaveRoutes(_allLandRoutes.Where(r => r.Origin.Region.Equals(originRegion, StringComparison.OrdinalIgnoreCase)).ToList(), RouteType.Land);
                    Console.WriteLine("Land route(s) removed and file updated.");
                }
            }
            else // RouteType.Sea
            {
                var routesToRemove = _allSeaRoutes.Where(r =>
                    r.Origin.Name.Equals(originName, StringComparison.OrdinalIgnoreCase) &&
                    r.Origin.Region.Equals(originRegion, StringComparison.OrdinalIgnoreCase) &&
                    r.Destination.Name.Equals(destName, StringComparison.OrdinalIgnoreCase) &&
                    r.Destination.Region.Equals(destRegion, StringComparison.OrdinalIgnoreCase)
                ).ToList();

                if (routesToRemove.Any())
                {
                    foreach (var route in routesToRemove)
                    {
                        _allSeaRoutes.Remove(route);
                        removed = true;
                    }
                    // Save the modified list for the region
                    _dataManager.SaveRoutes(_allSeaRoutes.Where(r => r.Origin.Region.Equals(originRegion, StringComparison.OrdinalIgnoreCase)).ToList(), RouteType.Sea);
                    Console.WriteLine("Sea route(s) removed and file updated.");
                }
            }

            if (!removed)
            {
                Console.WriteLine("No matching route found to remove.");
            }
            Console.WriteLine("Remember to rebuild the graph for changes to take effect in pathfinding.");
            // RebuildGraph(); // Handled by Program.cs now
        }

        // --- Edit Route ---
        public void EditRoute()
        {
            Console.WriteLine("\n--- Edit Route ---");
            RouteType type = ConsoleInput.GetEnumInput<RouteType>("Select route type to edit:");

            Console.WriteLine("Enter Origin Settlement Name and Region for the route to edit:");
            string originName = ConsoleInput.GetStringInput("Origin Settlement Name: ");
            string originRegion = ConsoleInput.GetStringInput("Origin Region Name: ");

            Console.WriteLine("Enter Destination Settlement Name and Region for the route to edit:");
            string destName = ConsoleInput.GetStringInput("Destination Settlement Name: ");
            string destRegion = ConsoleInput.GetStringInput("Destination Region Name: ");

            IRoute routeToEdit = null;

            if (type == RouteType.Land)
            {
                routeToEdit = _allLandRoutes.FirstOrDefault(r =>
                    r.Origin.Name.Equals(originName, StringComparison.OrdinalIgnoreCase) &&
                    r.Origin.Region.Equals(originRegion, StringComparison.OrdinalIgnoreCase) &&
                    r.Destination.Name.Equals(destName, StringComparison.OrdinalIgnoreCase) &&
                    r.Destination.Region.Equals(destRegion, StringComparison.OrdinalIgnoreCase)
                );
            }
            else // RouteType.Sea
            {
                routeToEdit = _allSeaRoutes.FirstOrDefault(r =>
                    r.Origin.Name.Equals(originName, StringComparison.OrdinalIgnoreCase) &&
                    r.Origin.Region.Equals(originRegion, StringComparison.OrdinalIgnoreCase) &&
                    r.Destination.Name.Equals(destName, StringComparison.OrdinalIgnoreCase) &&
                    r.Destination.Region.Equals(destRegion, StringComparison.OrdinalIgnoreCase)
                );
            }

            if (routeToEdit == null)
            {
                Console.WriteLine("No matching route found to edit.");
                return;
            }

            Console.WriteLine($"Found route: {routeToEdit}");

            if (type == RouteType.Land && routeToEdit is LandRoute landRoute)
            {
                // Prompt for new biome data
                List<string> newBiomes = new List<string>();
                List<double> newBiomeDistances = new List<double>();
                bool addMoreBiomes = true;
                while (addMoreBiomes)
                {
                    string biome = ConsoleInput.GetStringInput("Enter new biome name (leave empty to keep current):");
                    if (string.IsNullOrWhiteSpace(biome))
                    {
                        Console.WriteLine("Skipping biome entry.");
                    }
                    else
                    {
                        double distance = ConsoleInput.GetDoubleInput($"Enter distance in km for {biome}:", 0.1);
                        newBiomes.Add(biome);
                        newBiomeDistances.Add(distance);
                    }
                    addMoreBiomes = ConsoleInput.GetBooleanInput("Add another biome segment (Y/N)?");
                }

                if (newBiomes.Any() && newBiomes.Count == newBiomeDistances.Count)
                {
                    landRoute.Biomes = newBiomes;
                    landRoute.BiomeDistances = newBiomeDistances;
                    Console.WriteLine("Biomes and distances updated.");
                }
                else if (newBiomes.Any())
                {
                    Console.WriteLine("Warning: Biome and distance counts mismatch. Biome data not updated.");
                }


                bool newIsMapped = ConsoleInput.GetBooleanInput($"Is this route mapped (current: {landRoute.IsMapped}) (Y/N)?");
                landRoute.IsMapped = newIsMapped;
                Console.WriteLine("IsMapped status updated.");

                // Save the modified list for the region
                _dataManager.SaveRoutes(_allLandRoutes.Where(r => r.Origin.Region.Equals(originRegion, StringComparison.OrdinalIgnoreCase)).ToList(), RouteType.Land);
            }
            else if (type == RouteType.Sea && routeToEdit is SeaRoute seaRoute)
            {
                double newDistance = ConsoleInput.GetDoubleInput($"Enter new distance in kilometers (current: {seaRoute.Distance}km): ", 0.1);
                seaRoute.Distance = newDistance;
                Console.WriteLine("Distance updated.");

                // Save the modified list for the region
                _dataManager.SaveRoutes(_allSeaRoutes.Where(r => r.Origin.Region.Equals(originRegion, StringComparison.OrdinalIgnoreCase)).ToList(), RouteType.Sea);
            }

            Console.WriteLine("Route edited successfully! Remember to rebuild the graph for changes to take effect in pathfinding.");
            // RebuildGraph(); // Handled by Program.cs now
        }

        // --- File Management ---
        public void FileValidation()
        {
            Console.WriteLine("\n--- File Validation ---");
            Console.WriteLine("Enter Region Name to validate files for:");
            string regionName = ConsoleInput.GetStringInput("Region Name: ");

            string landFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorldData", "Land", $"{regionName}.txt");
            string seaFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorldData", "Sea", $"{regionName}.txt");
            string customLandFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorldData", "CustomRoutes", "Land", $"Custom_{regionName}.txt");
            string customSeaFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorldData", "CustomRoutes", "Sea", $"Custom_{regionName}.txt");


            Console.WriteLine($"\nValidating Land file for {regionName}...");
            bool landValid = _dataManager.IsFileFormatValid(landFilePath, RouteType.Land);
            Console.WriteLine($"Land file is {(landValid ? "VALID" : "INVALID")}");

            Console.WriteLine($"\nValidating Sea file for {regionName}...");
            bool seaValid = _dataManager.IsFileFormatValid(seaFilePath, RouteType.Sea);
            Console.WriteLine($"Sea file is {(seaValid ? "VALID" : "INVALID")}");

            Console.WriteLine($"\nValidating Custom Land file for {regionName}...");
            bool customLandValid = _dataManager.IsFileFormatValid(customLandFilePath, RouteType.Land);
            Console.WriteLine($"Custom Land file is {(customLandValid ? "VALID" : "INVALID")}");

            Console.WriteLine($"\nValidating Custom Sea file for {regionName}...");
            bool customSeaValid = _dataManager.IsFileFormatValid(customSeaFilePath, RouteType.Sea);
            Console.WriteLine($"Custom Sea file is {(customSeaValid ? "VALID" : "INVALID")}");
        }

        public void FileRepair()
        {
            Console.WriteLine("\n--- File Repair ---");
            Console.WriteLine("Enter Region Name to repair files for:");
            string regionName = ConsoleInput.GetStringInput("Region Name: ");

            string landFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorldData", "Land", $"{regionName}.txt");
            string seaFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorldData", "Sea", $"{regionName}.txt");
            string customLandFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorldData", "CustomRoutes", "Land", $"Custom_{regionName}.txt");
            string customSeaFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorldData", "CustomRoutes", "Sea", $"Custom_{regionName}.txt");

            Console.WriteLine($"\nAttempting to repair Land file: {Path.GetFileName(landFilePath)}");
            List<string> landIssues = _dataManager.RepairFile(landFilePath, RouteType.Land);
            if (landIssues.Any())
            {
                Console.WriteLine("Land file repair completed with issues:");
                foreach (var issue in landIssues) Console.WriteLine($"- {issue}");
            }
            else
            {
                Console.WriteLine("Land file repaired with no issues found.");
            }

            Console.WriteLine($"\nAttempting to repair Sea file: {Path.GetFileName(seaFilePath)}");
            List<string> seaIssues = _dataManager.RepairFile(seaFilePath, RouteType.Sea);
            if (seaIssues.Any())
            {
                Console.WriteLine("Sea file repair completed with issues:");
                foreach (var issue in seaIssues) Console.WriteLine($"- {issue}");
            }
            else
            {
                Console.WriteLine("Sea file repaired with no issues found.");
            }

            Console.WriteLine($"\nAttempting to repair Custom Land file: {Path.GetFileName(customLandFilePath)}");
            List<string> customLandIssues = _dataManager.RepairFile(customLandFilePath, RouteType.Land);
            if (customLandIssues.Any())
            {
                Console.WriteLine("Custom Land file repair completed with issues:");
                foreach (var issue in customLandIssues) Console.WriteLine($"- {issue}");
            }
            else
            {
                Console.WriteLine("Custom Land file repaired with no issues found.");
            }

            Console.WriteLine($"\nAttempting to repair Custom Sea file: {Path.GetFileName(customSeaFilePath)}");
            List<string> customSeaIssues = _dataManager.RepairFile(customSeaFilePath, RouteType.Sea);
            if (customSeaIssues.Any())
            {
                Console.WriteLine("Custom Sea file repair completed with issues:");
                foreach (var issue in customSeaIssues) Console.WriteLine($"- {issue}");
            }
            else
            {
                Console.WriteLine("Custom Sea file repaired with no issues found.");
            }

            Console.WriteLine("\nFile repair process finished. It's recommended to validate files after repair.");
            LoadAllRoutes(); // Reload data after repair to reflect changes in memory
            // RebuildGraph(); // Handled by Program.cs now
        }

        public void FilePrint()
        {
            Console.WriteLine("\n--- File Print ---");
            Console.WriteLine("Enter Region Name to print files for:");
            string regionName = ConsoleInput.GetStringInput("Region Name: ");

            string landFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorldData", "Land", $"{regionName}.txt");
            string seaFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorldData", "Sea", $"{regionName}.txt");
            string customLandFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorldData", "CustomRoutes", "Land", $"Custom_{regionName}.txt");
            string customSeaFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorldData", "CustomRoutes", "Sea", $"Custom_{regionName}.txt");

            _dataManager.PrintFileContent(landFilePath);
            _dataManager.PrintFileContent(seaFilePath);
            _dataManager.PrintFileContent(customLandFilePath);
            _dataManager.PrintFileContent(customSeaFilePath);
        }

        public void FileCheck()
        {
            Console.WriteLine("\n--- File Check ---");
            LoadAllRoutes(); // Ensure all routes are loaded
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
    }
}
