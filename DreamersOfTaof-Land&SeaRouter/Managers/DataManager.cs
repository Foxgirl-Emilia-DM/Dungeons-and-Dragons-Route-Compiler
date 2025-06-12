using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YourFantasyWorldProject.Classes; // Make sure this is present and correct

namespace YourFantasyWorldProject.Managers
{
    public class DataManager
    {
        private readonly string _basePath;
        private readonly string _landPath;
        private readonly string _seaPath;
        private readonly string _customRoutesPath;
        private readonly string _customLandPath;
        private readonly string _customSeaPath;

        public DataManager(string baseDataDirectory = "WorldData")
        {
            _basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, baseDataDirectory);
            _landPath = Path.Combine(_basePath, "Land");
            _seaPath = Path.Combine(_basePath, "Sea");
            _customRoutesPath = Path.Combine(_basePath, "CustomRoutes");
            _customLandPath = Path.Combine(_customRoutesPath, "Land");
            _customSeaPath = Path.Combine(_customRoutesPath, "Sea");

            // Ensure all directories exist
            Directory.CreateDirectory(_landPath);
            Directory.CreateDirectory(_seaPath);
            Directory.CreateDirectory(_customRoutesPath);
            Directory.CreateDirectory(_customLandPath);
            Directory.CreateDirectory(_customSeaPath);
        }

        // --- Helper methods for file paths ---
        private string GetFilePath(string countryName, RouteType routeType)
        {
            string folderPath = routeType == RouteType.Land ? _landPath : _seaPath;
            return Path.Combine(folderPath, $"{countryName.Replace(" ", "_")}.txt");
        }

        private string GetCustomFilePath(string countryName, RouteType routeType)
        {
            string folderPath = routeType == RouteType.Land ? _customLandPath : _customSeaPath;
            return Path.Combine(folderPath, $"{countryName.Replace(" ", "_")}.txt");
        }

        // --- Public methods to Load/Save All Routes for a Country ---

        /// <summary>
        /// Loads all land and sea routes for a given country from its respective data files.
        /// </summary>
        /// <param name="countryName">The name of the country.</param>
        /// <returns>A tuple containing lists of LandRoute and SeaRoute objects.</returns>
        public (List<LandRoute> landRoutes, List<SeaRoute> seaRoutes) LoadRoutes(string countryName)
        {
            List<LandRoute> landRoutes = new List<LandRoute>();
            List<SeaRoute> seaRoutes = new List<SeaRoute>();

            string landFilePath = GetFilePath(countryName, RouteType.Land);
            string seaFilePath = GetFilePath(countryName, RouteType.Sea);

            if (File.Exists(landFilePath))
            {
                landRoutes.AddRange(ParseLandFile(landFilePath));
            }
            if (File.Exists(seaFilePath))
            {
                seaRoutes.AddRange(ParseSeaFile(seaFilePath));
            }

            return (landRoutes, seaRoutes);
        }

        /// <summary>
        /// Saves all land or sea routes for a given country to its respective data file.
        /// </summary>
        /// <param name="countryName">The name of the country.</param>
        /// <param name="routeType">The type of routes (Land or Sea) to save.</param>
        /// <param name="landRoutes">List of land routes (only used if routeType is Land).</param>
        /// <param name="seaRoutes">List of sea routes (only used if routeType is Sea).</param>
        public void SaveRoutes(string countryName, RouteType routeType, List<LandRoute> landRoutes, List<SeaRoute> seaRoutes)
        {
            string filePath = GetFilePath(countryName, routeType);

            try
            {
                // Clear the file and write new data
                using (StreamWriter writer = new StreamWriter(filePath, append: false)) // Overwrite file
                {
                    Dictionary<string, List<IRoute>> routesByOrigin = new Dictionary<string, List<IRoute>>();

                    if (routeType == RouteType.Land && landRoutes != null)
                    {
                        foreach (var route in landRoutes.Where(r => r.Origin.Country.Equals(countryName, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (!routesByOrigin.ContainsKey(route.Origin.Name.ToUpperInvariant()))
                            {
                                routesByOrigin[route.Origin.Name.ToUpperInvariant()] = new List<IRoute>();
                            }
                            routesByOrigin[route.Origin.Name.ToUpperInvariant()].Add(route);
                        }
                    }
                    else if (routeType == RouteType.Sea && seaRoutes != null)
                    {
                        foreach (var route in seaRoutes.Where(r => r.Origin.Country.Equals(countryName, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (!routesByOrigin.ContainsKey(route.Origin.Name.ToUpperInvariant()))
                            {
                                routesByOrigin[route.Origin.Name.ToUpperInvariant()] = new List<IRoute>();
                            }
                            routesByOrigin[route.Origin.Name.ToUpperInvariant()].Add(route);
                        }
                    }

                    foreach (var originEntry in routesByOrigin.OrderBy(kvp => kvp.Key))
                    {
                        // Write the origin settlement header
                        writer.WriteLine($"{originEntry.Key} {originEntry.Value.First().Origin.Country}"); // Include country name for clarity

                        foreach (var route in originEntry.Value.OrderBy(r => r.Destination.Name))
                        {
                            if (route is LandRoute lr)
                            {
                                writer.WriteLine(lr.ToFileString());
                            }
                            else if (route is SeaRoute sr)
                            {
                                writer.WriteLine(sr.ToFileString());
                            }
                        }
                        writer.WriteLine(); // Add an empty line for separation
                    }
                }
                Console.WriteLine($"Routes for {countryName} ({routeType}) saved to {Path.GetFileName(filePath)}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving routes to {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        // --- Private Helper Methods for Parsing Files ---

        private List<LandRoute> ParseLandFile(string filePath)
        {
            List<LandRoute> routes = new List<LandRoute>();
            Settlement currentOrigin = null;

            try
            {
                string[] lines = File.ReadAllLines(filePath);

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();

                    if (string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        currentOrigin = null; // Reset for next block
                        continue;
                    }

                    if (!trimmedLine.StartsWith("\t")) // It's an origin settlement header
                    {
                        string[] parts = trimmedLine.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 1) // At least settlement name
                        {
                            string settlementName = parts[0];
                            string countryName = parts.Length > 1 ? parts[1].Trim() : Path.GetFileNameWithoutExtension(filePath).Replace("_", " "); // Infer country from filename if not explicitly provided
                            currentOrigin = new Settlement(settlementName, countryName);
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Malformed origin header '{trimmedLine}' in {Path.GetFileName(filePath)}. Skipping block.");
                            currentOrigin = null; // Skip this malformed block
                        }
                    }
                    else if (currentOrigin != null) // It's a destination route
                    {
                        string routeData = trimmedLine.TrimStart('\t');
                        string[] parts = routeData.Split('\t');

                        if (parts.Length == 5) // Expected for LandRoute: DestName, DestCountry, Biomes, BiomeDistances, IsMapped
                        {
                            try
                            {
                                string destName = parts[0].Trim();
                                string destCountry = parts[1].Trim();
                                List<string> biomes = parts[2].Split(',').Select(s => s.Trim()).ToList();
                                List<double> biomeDistances = parts[3].Split(',').Select(s => double.TryParse(s.Trim().Replace("km", ""), out double d) ? d : 0).ToList();
                                bool isMapped = bool.TryParse(parts[4].Trim(), out bool mapped) && mapped;

                                routes.Add(new LandRoute(currentOrigin, new Settlement(destName, destCountry), biomes, biomeDistances, isMapped));
                            }
                            catch (Exception ex) // Catch parsing errors for a specific route line
                            {
                                Console.WriteLine($"Warning: Error parsing land route data '{routeData}' for origin '{currentOrigin.Name}' in {Path.GetFileName(filePath)}: {ex.Message}. Skipping line.");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Malformed land route data '{routeData}' for origin '{currentOrigin.Name}' in {Path.GetFileName(filePath)}. Expected 5 parts. Skipping line.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading land file {Path.GetFileName(filePath)}: {ex.Message}");
            }
            return routes;
        }

        private List<SeaRoute> ParseSeaFile(string filePath)
        {
            List<SeaRoute> routes = new List<SeaRoute>();
            Settlement currentOrigin = null;

            try
            {
                string[] lines = File.ReadAllLines(filePath);

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();

                    if (string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        currentOrigin = null; // Reset for next block
                        continue;
                    }

                    if (!trimmedLine.StartsWith("\t")) // It's an origin settlement header
                    {
                        string[] parts = trimmedLine.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 1) // At least settlement name
                        {
                            string settlementName = parts[0];
                            string countryName = parts.Length > 1 ? parts[1].Trim() : Path.GetFileNameWithoutExtension(filePath).Replace("_", " "); // Infer country from filename if not explicitly provided
                            currentOrigin = new Settlement(settlementName, countryName);
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Malformed origin header '{trimmedLine}' in {Path.GetFileName(filePath)}. Skipping block.");
                            currentOrigin = null; // Skip this malformed block
                        }
                    }
                    else if (currentOrigin != null) // It's a destination route
                    {
                        string routeData = trimmedLine.TrimStart('\t');
                        string[] parts = routeData.Split('\t');

                        if (parts.Length == 3) // Expected for SeaRoute: DestName, DestCountry, Distance
                        {
                            try
                            {
                                string destName = parts[0].Trim();
                                string destCountry = parts[1].Trim();
                                double distance = double.TryParse(parts[2].Trim().Replace("km", ""), out double d) ? d : 0;

                                routes.Add(new SeaRoute(currentOrigin, new Settlement(destName, destCountry), distance));
                            }
                            catch (Exception ex) // Catch parsing errors for a specific route line
                            {
                                Console.WriteLine($"Warning: Error parsing sea route data '{routeData}' for origin '{currentOrigin.Name}' in {Path.GetFileName(filePath)}: {ex.Message}. Skipping line.");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Malformed sea route data '{routeData}' for origin '{currentOrigin.Name}' in {Path.GetFileName(filePath)}. Expected 3 parts. Skipping line.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading sea file {Path.GetFileName(filePath)}: {ex.Message}");
            }
            return routes;
        }

        /// <summary>
        /// Retrieves a list of all unique country names found in the data files.
        /// </summary>
        /// <returns>An ordered list of unique country names.</returns>
        public List<string> GetAllCountryNames()
        {
            HashSet<string> countryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Check Land directories
            if (Directory.Exists(_landPath))
            {
                foreach (string filePath in Directory.GetFiles(_landPath, "*.txt"))
                {
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
                    countryNames.Add(fileNameWithoutExtension.Replace("_", " ")); // Convert back to readable name
                }
            }

            // Check Sea directories
            if (Directory.Exists(_seaPath))
            {
                foreach (string filePath in Directory.GetFiles(_seaPath, "*.txt"))
                {
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
                    countryNames.Add(fileNameWithoutExtension.Replace("_", " ")); // Convert back to readable name
                }
            }

            // Optionally, include custom routes directories as well if they can define new countries
            if (Directory.Exists(_customLandPath))
            {
                foreach (string filePath in Directory.GetFiles(_customLandPath, "*.txt"))
                {
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
                    countryNames.Add(fileNameWithoutExtension.Replace("_", " "));
                }
            }
            if (Directory.Exists(_customSeaPath))
            {
                foreach (string filePath in Directory.GetFiles(_customSeaPath, "*.txt"))
                {
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
                    countryNames.Add(fileNameWithoutExtension.Replace("_", " "));
                }
            }

            return countryNames.OrderBy(c => c).ToList();
        }

        // --- New: Save Custom Route ---

        /// <summary>
        /// Saves a custom route (either Land or Sea) to its respective custom data file.
        /// </summary>
        /// <param name="originCountryName">The country name of the route's origin.</param>
        /// <param name="routeType">The type of route (Land or Sea).</param>
        /// <param name="route">The IRoute object to save.</param>
        public void SaveCustomRoute(string originCountryName, RouteType routeType, IRoute route)
        {
            string filePath = GetCustomFilePath(originCountryName, routeType);

            try
            {
                // Ensure the directory exists before writing
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                // Append the custom route to the file
                using (StreamWriter writer = new StreamWriter(filePath, append: true)) // append: true to add to existing
                {
                    // Write origin settlement header if it's a new block or first entry for this origin
                    // For simplicity, we'll append the origin header and the route.
                    // A more robust system would load the custom file, add the route in memory, then save.
                    // This simple append might lead to duplicate origin headers if not managed externally.
                    writer.WriteLine($"{route.Origin.Name.ToUpperInvariant()} {route.Origin.Country}"); // Write origin settlement header
                    if (route is LandRoute landRoute)
                    {
                        writer.WriteLine(landRoute.ToFileString());
                    }
                    else if (route is SeaRoute seaRoute)
                    {
                        writer.WriteLine(seaRoute.ToFileString());
                    }
                    writer.WriteLine(); // Add an empty line for separation
                }
                Console.WriteLine($"Custom {routeType} route from {route.Origin.Name} to {route.Destination.Name} saved to {Path.GetFileName(filePath)}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving custom {routeType} route to {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        // --- Optional: Load Custom Routes (for future use or internal use) ---
        /// <summary>
        /// Loads custom sea routes for a given country from its custom data file.
        /// </summary>
        /// <param name="countryName">The name of the country.</param>
        /// <returns>A list of SeaRoute objects.</returns>
        private List<SeaRoute> LoadCustomSeaRoutes(string countryName) // Made private as it's not externally called in Pathfinder directly
        {
            List<SeaRoute> customRoutes = new List<SeaRoute>();
            string filePath = GetCustomFilePath(countryName, RouteType.Sea);
            if (File.Exists(filePath))
            {
                customRoutes.AddRange(ParseSeaFile(filePath)); // Assuming custom sea format is same as regular sea format
            }
            return customRoutes;
        }
    }
}