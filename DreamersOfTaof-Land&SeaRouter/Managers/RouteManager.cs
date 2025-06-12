using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YourFantasyWorldProject.Classes;
using YourFantasyWorldProject.Utils; // For ConsoleInput

namespace YourFantasyWorldProject.Managers
{
    public class RouteManager
    {
        private readonly DataManager _dataManager;
        private List<LandRoute> _allLandRoutes;
        private List<SeaRoute> _allSeaRoutes;
        public IReadOnlyList<LandRoute> AllLandRoutes => _allLandRoutes.AsReadOnly();
        public IReadOnlyList<SeaRoute> AllSeaRoutes => _allSeaRoutes.AsReadOnly();

        public RouteManager(DataManager dataManager)
        {
            _dataManager = dataManager;
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

            var allCountries = _dataManager.GetAllCountryNames();
            foreach (var country in allCountries)
            {
                var (land, sea) = _dataManager.LoadRoutes(country);
                _allLandRoutes.AddRange(land);
                _allSeaRoutes.AddRange(sea);
            }
            Console.WriteLine($"Loaded {_allLandRoutes.Count} land routes and {_allSeaRoutes.Count} sea routes from all files.");
            SortAllRoutesInMemory();
        }

        // --- Internal Helper: Sort All Routes in Memory ---
        // Ensures consistent ordering for easier processing and display
        private void SortAllRoutesInMemory()
        {
            _allLandRoutes = _allLandRoutes
                .OrderBy(r => r.Origin.Country)
                .ThenBy(r => r.Origin.Name)
                .ThenBy(r => r.Destination.Country)
                .ThenBy(r => r.Destination.Name)
                .ToList();

            _allSeaRoutes = _allSeaRoutes
                .OrderBy(r => r.Origin.Country)
                .ThenBy(r => r.Origin.Name)
                .ThenBy(r => r.Destination.Country)
                .ThenBy(r => r.Destination.Name)
                .ToList();
        }

        // --- Internal Helper: Save Modified Country Files ---
        // This is crucial for persistence after any changes
        private void SaveAffectedFiles(HashSet<string> affectedCountries)
        {
            foreach (var country in affectedCountries)
            {
                // Filter routes relevant to this country for saving
                var countryLandRoutes = _allLandRoutes
                    .Where(r => r.Origin.Country.Equals(country, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var countrySeaRoutes = _allSeaRoutes
                    .Where(r => r.Origin.Country.Equals(country, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                _dataManager.SaveRoutes(country, RouteType.Land, countryLandRoutes, null); // Pass null for unused list
                _dataManager.SaveRoutes(country, RouteType.Sea, null, countrySeaRoutes); // Pass null for unused list
            }
            RebuildGraph(); // After saving, rebuild the graph for pathfinding
        }

        // --- Management Methods ---

        public void AddRoute()
        {
            Console.WriteLine("\n--- Add New Route ---");
            string originCountryName = ConsoleInput.GetStringInput("Enter origin country name: ");
            string originSettlementName = ConsoleInput.GetStringInput("Enter origin settlement name: ");
            string destinationCountryName = ConsoleInput.GetStringInput("Enter destination country name: ");
            string destinationSettlementName = ConsoleInput.GetStringInput("Enter destination settlement name: ");

            RouteType routeType = ConsoleInput.GetEnumInput<RouteType>("Select route type:");

            Settlement origin = new Settlement(originSettlementName, originCountryName);
            Settlement destination = new Settlement(destinationSettlementName, destinationCountryName);

            List<string> biomes = new List<string>();
            List<double> biomeDistances = new List<double>();
            double distance = 0;
            bool isMapped = false;

            if (routeType == RouteType.Land)
            {
                Console.WriteLine("Enter biomes and their distances (type 'done' for biome name when finished):");
                string biomeName;
                do
                {
                    biomeName = ConsoleInput.GetStringInput("Biome name (or 'done'): ", allowEmpty: true);
                    if (!string.IsNullOrWhiteSpace(biomeName) && biomeName.ToLowerInvariant() != "done")
                    {
                        double bDistance = ConsoleInput.GetDoubleInput($"Distance in {biomeName} (km): ");
                        biomes.Add(biomeName);
                        biomeDistances.Add(bDistance);
                    }
                } while (!string.IsNullOrWhiteSpace(biomeName) && biomeName.ToLowerInvariant() != "done");

                isMapped = ConsoleInput.GetBooleanInput("Is this route fully mapped?");
            }
            else // RouteType.Sea
            {
                distance = ConsoleInput.GetDoubleInput("Enter sea route distance (km): ");
            }

            // Keep track of affected countries for saving
            HashSet<string> affectedCountries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add Forward Route
            bool addedForward = AddOrUpdateRoute(origin, destination, routeType, biomes, biomeDistances, distance, isMapped, _allLandRoutes, _allSeaRoutes);
            if (addedForward) affectedCountries.Add(originCountryName);

            // Add Reverse Route
            bool addedReverse = AddOrUpdateRoute(destination, origin, routeType, biomes, biomeDistances, distance, isMapped, _allLandRoutes, _allSeaRoutes);
            if (addedReverse) affectedCountries.Add(destinationCountryName);

            if (addedForward || addedReverse)
            {
                SortAllRoutesInMemory();
                SaveAffectedFiles(affectedCountries);
                Console.WriteLine("Route(s) added successfully.");
            }
            else
            {
                Console.WriteLine("Route already existed (both forward and reverse) and no changes were made.");
            }
        }

        private bool AddOrUpdateRoute(Settlement origin, Settlement destination, RouteType routeType,
                                    List<string> biomes, List<double> biomeDistances, double distance, bool isMapped,
                                    List<LandRoute> landRoutes, List<SeaRoute> seaRoutes)
        {
            if (routeType == RouteType.Land)
            {
                var existingRoute = landRoutes.FirstOrDefault(r => r.Origin.Equals(origin) && r.Destination.Equals(destination));
                if (existingRoute == null)
                {
                    landRoutes.Add(new LandRoute(origin, destination, biomes, biomeDistances, isMapped));
                    return true;
                }
                else
                {
                    // Optionally update if existing, for now, just skip if it's identical or prefer to use edit.
                    Console.WriteLine($"Note: Land route from {origin} to {destination} already exists.");
                    return false; // Indicates no new route was added.
                }
            }
            else // SeaRoute
            {
                var existingRoute = seaRoutes.FirstOrDefault(r => r.Origin.Equals(origin) && r.Destination.Equals(destination));
                if (existingRoute == null)
                {
                    seaRoutes.Add(new SeaRoute(origin, destination, distance));
                    return true;
                }
                else
                {
                    Console.WriteLine($"Note: Sea route from {origin} to {destination} already exists.");
                    return false; // Indicates no new route was added.
                }
            }
        }


        public void RemoveRoute()
        {
            Console.WriteLine("\n--- Remove Route ---");
            string countryName = ConsoleInput.GetStringInput("Enter country name of origin settlement: ");
            string settlementName = ConsoleInput.GetStringInput("Enter origin settlement name: ");
            RouteType routeType = ConsoleInput.GetEnumInput<RouteType>("Select route type to remove:");

            Settlement originToRemove = new Settlement(settlementName, countryName);
            List<LandRoute> routesToRemoveLand = new List<LandRoute>();
            List<SeaRoute> routesToRemoveSea = new List<SeaRoute>();

            if (routeType == RouteType.Land)
            {
                routesToRemoveLand = _allLandRoutes
                    .Where(r => r.Origin.Equals(originToRemove))
                    .ToList();
            }
            else // Sea
            {
                routesToRemoveSea = _allSeaRoutes
                    .Where(r => r.Origin.Equals(originToRemove))
                    .ToList();
            }

            if (!routesToRemoveLand.Any() && !routesToRemoveSea.Any())
            {
                Console.WriteLine($"No {routeType} routes found originating from {settlementName} in {countryName}.");
                return;
            }

            Console.WriteLine($"Routes originating from {settlementName} ({countryName}):");
            List<IRoute> selectableRoutes = new List<IRoute>(); // Use a common interface or object to display both types

            int i = 1;
            if (routeType == RouteType.Land)
            {
                foreach (var r in routesToRemoveLand)
                {
                    Console.WriteLine($"  {i++}. {r}");
                    selectableRoutes.Add(r);
                }
            }
            else
            {
                foreach (var r in routesToRemoveSea)
                {
                    Console.WriteLine($"  {i++}. {r}");
                    selectableRoutes.Add(r);
                }
            }

            if (!selectableRoutes.Any())
            {
                Console.WriteLine("No destinations to remove for the selected type.");
                return;
            }

            int choice = (int)ConsoleInput.GetDoubleInput($"Enter the number of the route to delete (1-{selectableRoutes.Count}): ", 1);
            if (choice < 1 || choice > selectableRoutes.Count)
            {
                Console.WriteLine("Invalid choice.");
                return;
            }

            IRoute routeToDelete = selectableRoutes[choice - 1];

            HashSet<string> affectedCountries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Remove Forward Route
            bool removedForward = false;
            if (routeToDelete is LandRoute landRoute)
            {
                removedForward = _allLandRoutes.Remove(landRoute);
                if (removedForward) affectedCountries.Add(landRoute.Origin.Country);
            }
            else if (routeToDelete is SeaRoute seaRoute)
            {
                removedForward = _allSeaRoutes.Remove(seaRoute);
                if (removedForward) affectedCountries.Add(seaRoute.Origin.Country);
            }


            // Remove Reverse Route
            bool removedReverse = false;
            if (routeToDelete is LandRoute lr)
            {
                var reverseRoute = _allLandRoutes.FirstOrDefault(r => r.Origin.Equals(lr.Destination) && r.Destination.Equals(lr.Origin));
                if (reverseRoute != null)
                {
                    removedReverse = _allLandRoutes.Remove(reverseRoute);
                    if (removedReverse) affectedCountries.Add(reverseRoute.Origin.Country);
                }
            }
            else if (routeToDelete is SeaRoute sr)
            {
                var reverseRoute = _allSeaRoutes.FirstOrDefault(r => r.Origin.Equals(sr.Destination) && r.Destination.Equals(sr.Origin));
                if (reverseRoute != null)
                {
                    removedReverse = _allSeaRoutes.Remove(reverseRoute);
                    if (removedReverse) affectedCountries.Add(reverseRoute.Origin.Country);
                }
            }

            if (removedForward || removedReverse)
            {
                SortAllRoutesInMemory();
                SaveAffectedFiles(affectedCountries);
                Console.WriteLine("Route(s) removed successfully.");
            }
            else
            {
                Console.WriteLine("Route was not found or could not be removed.");
            }
        }

        public void EditRoute()
        {
            Console.WriteLine("\n--- Edit Route ---");
            string countryName = ConsoleInput.GetStringInput("Enter country name of origin settlement: ");
            string settlementName = ConsoleInput.GetStringInput("Enter origin settlement name: ");
            RouteType routeType = ConsoleInput.GetEnumInput<RouteType>("Select route type to edit:");

            Settlement originToEdit = new Settlement(settlementName, countryName);
            List<LandRoute> routesToEditLand = new List<LandRoute>();
            List<SeaRoute> routesToEditSea = new List<SeaRoute>();

            if (routeType == RouteType.Land)
            {
                routesToEditLand = _allLandRoutes
                    .Where(r => r.Origin.Equals(originToEdit))
                    .ToList();
            }
            else // Sea
            {
                routesToEditSea = _allSeaRoutes
                    .Where(r => r.Origin.Equals(originToEdit))
                    .ToList();
            }

            if (!routesToEditLand.Any() && !routesToEditSea.Any())
            {
                Console.WriteLine($"No {routeType} routes found originating from {settlementName} in {countryName}.");
                return;
            }

            Console.WriteLine($"Routes originating from {settlementName} ({countryName}):");
            List<IRoute> selectableRoutes = new List<IRoute>(); // Placeholder for common interface for display

            int i = 1;
            if (routeType == RouteType.Land)
            {
                foreach (var r in routesToEditLand)
                {
                    Console.WriteLine($"  {i++}. {r}");
                    selectableRoutes.Add(r);
                }
            }
            else
            {
                foreach (var r in routesToEditSea)
                {
                    Console.WriteLine($"  {i++}. {r}");
                    selectableRoutes.Add(r);
                }
            }

            if (!selectableRoutes.Any())
            {
                Console.WriteLine("No destinations to edit for the selected type.");
                return;
            }

            int choice = (int)ConsoleInput.GetDoubleInput($"Enter the number of the route to edit (1-{selectableRoutes.Count}): ", 1);
            if (choice < 1 || choice > selectableRoutes.Count)
            {
                Console.WriteLine("Invalid choice.");
                return;
            }

            IRoute selectedRoute = selectableRoutes[choice - 1];
            HashSet<string> affectedCountries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (selectedRoute is LandRoute landRouteToEdit)
            {
                Console.WriteLine($"Editing Land Route: {landRouteToEdit}");
                landRouteToEdit.Biomes.Clear();
                landRouteToEdit.BiomeDistances.Clear();

                Console.WriteLine("Enter new biomes and their distances (type 'done' for biome name when finished):");
                string biomeName;
                do
                {
                    biomeName = ConsoleInput.GetStringInput("Biome name (or 'done'): ", allowEmpty: true);
                    if (!string.IsNullOrWhiteSpace(biomeName) && biomeName.ToLowerInvariant() != "done")
                    {
                        double bDistance = ConsoleInput.GetDoubleInput($"Distance in {biomeName} (km): ");
                        landRouteToEdit.Biomes.Add(biomeName);
                        landRouteToEdit.BiomeDistances.Add(bDistance);
                    }
                } while (!string.IsNullOrWhiteSpace(biomeName) && biomeName.ToLowerInvariant() != "done");

                landRouteToEdit.IsMapped = ConsoleInput.GetBooleanInput("Is this route fully mapped?");

                // Update reverse route
                var reverseRoute = _allLandRoutes.FirstOrDefault(r => r.Origin.Equals(landRouteToEdit.Destination) && r.Destination.Equals(landRouteToEdit.Origin));
                if (reverseRoute != null)
                {
                    reverseRoute.Biomes = new List<string>(landRouteToEdit.Biomes); // Deep copy
                    reverseRoute.BiomeDistances = new List<double>(landRouteToEdit.BiomeDistances); // Deep copy
                    reverseRoute.IsMapped = landRouteToEdit.IsMapped;
                    affectedCountries.Add(reverseRoute.Origin.Country);
                }
                affectedCountries.Add(landRouteToEdit.Origin.Country);
            }
            else if (selectedRoute is SeaRoute seaRouteToEdit)
            {
                Console.WriteLine($"Editing Sea Route: {seaRouteToEdit}");
                seaRouteToEdit.Distance = ConsoleInput.GetDoubleInput("Enter new sea route distance (km): ");

                // Update reverse route
                var reverseRoute = _allSeaRoutes.FirstOrDefault(r => r.Origin.Equals(seaRouteToEdit.Destination) && r.Destination.Equals(seaRouteToEdit.Origin));
                if (reverseRoute != null)
                {
                    reverseRoute.Distance = seaRouteToEdit.Distance;
                    affectedCountries.Add(reverseRoute.Origin.Country);
                }
                affectedCountries.Add(seaRouteToEdit.Origin.Country);
            }

            SortAllRoutesInMemory();
            SaveAffectedFiles(affectedCountries);
            Console.WriteLine("Route edited successfully.");
        }

        public void FileValidation()
        {
            Console.WriteLine("\n--- File Validation ---");
            Console.WriteLine("Checking file integrity and route consistency...");

            var allCountries = _dataManager.GetAllCountryNames();
            bool hasErrors = false;

            // Check for missing reverse routes
            foreach (var landRoute in _allLandRoutes)
            {
                var reverseRoute = _allLandRoutes.FirstOrDefault(r => r.Origin.Equals(landRoute.Destination) && r.Destination.Equals(landRoute.Origin));
                if (reverseRoute == null)
                {
                    Console.WriteLine($"Error: Missing reverse land route for {landRoute.Destination} <-> {landRoute.Origin}");
                    hasErrors = true;
                }
            }

            foreach (var seaRoute in _allSeaRoutes)
            {
                var reverseRoute = _allSeaRoutes.FirstOrDefault(r => r.Origin.Equals(seaRoute.Destination) && r.Destination.Equals(seaRoute.Origin));
                if (reverseRoute == null)
                {
                    Console.WriteLine($"Error: Missing reverse sea route for {seaRoute.Destination} <-> {seaRoute.Origin}");
                    hasErrors = true;
                }
            }

            // You can add more validation here:
            // - Check for malformed lines (already handled by DataManager's parsing with warnings)
            // - Check for duplicate origin settlement headers within a single file
            // - Check for duplicate destination entries under the same origin

            if (!hasErrors)
            {
                Console.WriteLine("File validation complete. No major inconsistencies found.");
            }
            else
            {
                Console.WriteLine("File validation complete. Errors were found. Consider running File Repair.");
            }
        }

        public void FileRepair()
        {
            Console.WriteLine("\n--- File Repair ---");
            Console.WriteLine("Attempting to fix inconsistencies, such as missing reverse routes...");

            HashSet<string> affectedCountries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool madeChanges = false;

            // Repair missing land reverse routes
            foreach (var landRoute in _allLandRoutes.ToList()) // ToList() to avoid modification during enumeration
            {
                var reverseRoute = _allLandRoutes.FirstOrDefault(r => r.Origin.Equals(landRoute.Destination) && r.Destination.Equals(landRoute.Origin));
                if (reverseRoute == null)
                {
                    Console.WriteLine($"Repairing: Adding missing reverse land route for {landRoute.Destination} <-> {landRoute.Origin}");
                    _allLandRoutes.Add(new LandRoute(landRoute.Destination, landRoute.Origin,
                                                    new List<string>(landRoute.Biomes), // Deep copy
                                                    new List<double>(landRoute.BiomeDistances), // Deep copy
                                                    landRoute.IsMapped));
                    affectedCountries.Add(landRoute.Origin.Country);
                    affectedCountries.Add(landRoute.Destination.Country);
                    madeChanges = true;
                }
            }

            // Repair missing sea reverse routes
            foreach (var seaRoute in _allSeaRoutes.ToList())
            {
                var reverseRoute = _allSeaRoutes.FirstOrDefault(r => r.Origin.Equals(seaRoute.Destination) && r.Destination.Equals(seaRoute.Origin));
                if (reverseRoute == null)
                {
                    Console.WriteLine($"Repairing: Adding missing reverse sea route for {seaRoute.Destination} <-> {seaRoute.Origin}");
                    _allSeaRoutes.Add(new SeaRoute(seaRoute.Destination, seaRoute.Origin, seaRoute.Distance));
                    affectedCountries.Add(seaRoute.Origin.Country);
                    affectedCountries.Add(seaRoute.Destination.Country);
                    madeChanges = true;
                }
            }

            if (madeChanges)
            {
                SortAllRoutesInMemory();
                SaveAffectedFiles(affectedCountries);
                Console.WriteLine("File repair complete. Changes saved.");
            }
            else
            {
                Console.WriteLine("No repair needed. Files are consistent.");
            }
        }

        public void FilePrint()
        {
            Console.WriteLine("\n--- Print Destinations ---");
            string countryName = ConsoleInput.GetStringInput("Enter country name: ");
            string settlementName = ConsoleInput.GetStringInput("Enter settlement name: ");

            Settlement targetOrigin = new Settlement(settlementName, countryName);

            var landDestinations = _allLandRoutes
                .Where(r => r.Origin.Equals(targetOrigin))
                .ToList();

            var seaDestinations = _allSeaRoutes
                .Where(r => r.Origin.Equals(targetOrigin))
                .ToList();

            if (!landDestinations.Any() && !seaDestinations.Any())
            {
                Console.WriteLine($"No routes found originating from {settlementName} in {countryName}.");
                return;
            }

            Console.WriteLine($"\nDestinations from {settlementName} ({countryName}):");

            if (landDestinations.Any())
            {
                Console.WriteLine("\n--- Land Routes ---");
                foreach (var route in landDestinations)
                {
                    Console.WriteLine($"  - To {route.Destination.Name} ({route.Destination.Country}): Total {route.TotalDistance}km, Biomes: {string.Join(", ", route.Biomes)}, Mapped: {route.IsMapped}");
                }
            }

            if (seaDestinations.Any())
            {
                Console.WriteLine("\n--- Sea Routes ---");
                foreach (var route in seaDestinations)
                {
                    Console.WriteLine($"  - To {route.Destination.Name} ({route.Destination.Country}): {route.Distance}km by Sea");
                }
            }
        }

        public void FileCheck()
        {
            Console.WriteLine("\n--- Total Unique Settlements ---");
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
            // foreach (var s in uniqueSettlements.OrderBy(s => s.Country).ThenBy(s => s.Name))
            // {
            //     Console.WriteLine($"  - {s}");
            // }
        }

        // --- Graph Rebuilding ---
        // This method will be called after any modification to route data
        // It provides the current state of all routes to the Pathfinder
        public void RebuildGraph()
        {
            // In a more complex scenario, this would pass the data to a Pathfinder instance
            // For now, let's just confirm it's called.
            // When Pathfinder is properly implemented, this would involve calling
            // pathfinderInstance.BuildGraph(_allLandRoutes, _allSeaRoutes);
            Console.WriteLine("Graph rebuilding triggered (placeholder).");
        }
    }
}