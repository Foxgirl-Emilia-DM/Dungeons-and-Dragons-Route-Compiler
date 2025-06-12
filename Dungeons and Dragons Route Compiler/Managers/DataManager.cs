// DataManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YourFantasyWorldProject.Classes;
using System.Net.Http;
using System.Security.Cryptography; // Added for encryption
using System.Text; // Added for Encoding
using System.Globalization; // Added for TextInfo
using System.IO.Compression; // ADD THIS LINE for ZipFile operations
using System.Threading.Tasks; // For async operations with HttpClient

namespace YourFantasyWorldProject.Managers
{
    public class DataManager
    {
        private readonly string _basePath; // WorldData directory
        private readonly string _landPath;
        private readonly string _seaPath;
        private readonly string _customRoutesPath; // WorldData/CustomRoutes
        private readonly string _customLandPath;
        private readonly string _customSeaPath;

        // IMPORTANT: Set this URL to the *parent directory* on GitHub where WorldData.zip will reside.
        // This is the path to your 'Dungeons and Dragons Route Compiler/' folder on GitHub,
        // as WorldData.zip will be directly inside it.
        private const string GitHubBaseUrl = "https://raw.githubusercontent.com/Foxgirl-Emilia-DM/Dungeons-and-Dragons-Route-Compiler/master/Dungeons%20and%20Dragons%20Route%20Compiler/";
        private static readonly HttpClient _httpClient = new HttpClient();

        // --- ENCRYPTION CONSTANTS (FOR PLAYER DATA) ---
        // IMPORTANT: Replace these with your own unique, securely generated key and IV.
        private const string AesKeyString = "YourStrong256BitKeyForAESEncryption1234"; // 32 chars for 256-bit
        private const string AesIVString = "YourStrong16ByteIV"; // 16 chars for 128-bit block size

        private readonly byte[] _aesKey;
        private readonly byte[] _aesIV;


        public DataManager()
        {
            _basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorldData");
            _landPath = Path.Combine(_basePath, "LandRoutes");
            _seaPath = Path.Combine(_basePath, "SeaRoutes");
            _customRoutesPath = Path.Combine(_basePath, "CustomRoutes");
            _customLandPath = Path.Combine(_customRoutesPath, "Land");
            _customSeaPath = Path.Combine(_customRoutesPath, "Sea");

            // Initialize encryption key and IV
            _aesKey = Encoding.UTF8.GetBytes(AesKeyString);
            _aesIV = Encoding.UTF8.GetBytes(AesIVString);

            // Ensure initial data is present when DataManager is constructed
            EnsureInitialDataPresent().Wait(); // Block until data is ready for the rest of the app to use
        }

        /// <summary>
        /// Ensures that the WorldData directory exists and contains initial data.
        /// If not, it attempts to download and extract WorldData.zip from GitHub.
        /// </summary>
        public async Task EnsureInitialDataPresent()
        {
            if (!Directory.Exists(_basePath) || !Directory.EnumerateFileSystemEntries(_basePath).Any())
            {
                Console.WriteLine("WorldData directory not found or is empty. Attempting to download from GitHub...");
                try
                {
                    string zipFileName = "WorldData.zip";
                    string zipFileUrl = GitHubBaseUrl + zipFileName;
                    string zipFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, zipFileName);

                    Console.WriteLine($"Downloading {zipFileName} from {zipFileUrl}...");
                    byte[] fileBytes = await _httpClient.GetByteArrayAsync(zipFileUrl);
                    await File.WriteAllBytesAsync(zipFilePath, fileBytes);
                    Console.WriteLine("Download complete. Extracting...");

                    // Ensure the base path exists before extraction
                    Directory.CreateDirectory(_basePath);

                    ZipFile.ExtractToDirectory(zipFilePath, _basePath, true); // true for overwrite
                    Console.WriteLine("Extraction complete. WorldData is now available.");

                    // Clean up the downloaded zip file
                    File.Delete(zipFilePath);
                }
                catch (HttpRequestException httpEx)
                {
                    Console.WriteLine($"Error downloading WorldData.zip from GitHub: {httpEx.Message}");
                    Console.WriteLine("Please ensure you have an internet connection and the GitHub repository is accessible.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred during data initialization: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("WorldData directory found. Initial data check passed.");
            }
            // Ensure necessary subdirectories exist even if data was already present
            Directory.CreateDirectory(_landPath);
            Directory.CreateDirectory(_seaPath);
            Directory.CreateDirectory(_customLandPath);
            Directory.CreateDirectory(_customSeaPath);
        }

        // --- Route File Management (Patch 1 changes applied here) ---

        /// <summary>
        /// Saves all provided LandRoutes and SeaRoutes to their respective files,
        /// using the new header-based, grouped format. This method clears existing files
        /// for the regions before writing the new data to prevent duplication.
        /// </summary>
        /// <param name="landRoutes">The complete list of all land routes to save.</param>
        /// <param name="seaRoutes">The complete list of all sea routes to save.</param>
        public void SaveAllRoutes(List<LandRoute> landRoutes, List<SeaRoute> seaRoutes)
        {
            Console.WriteLine("Saving all routes to files...");

            // Get all unique regions from the routes to ensure we clear and write all relevant files
            var landRegions = landRoutes.Select(r => r.Origin.Region).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var seaRegions = seaRoutes.Select(r => r.Origin.Region).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // Clear existing land route files for relevant regions
            foreach (var region in landRegions)
            {
                ClearRouteFileContent(region, RouteType.Land);
            }
            // Clear existing sea route files for relevant regions
            foreach (var region in seaRegions)
            {
                ClearRouteFileContent(region, RouteType.Sea);
            }

            // Group land routes by origin settlement and then region for writing
            // Need to group by (OriginName, OriginRegion) because Settlement.Equals compares them
            var groupedLandRoutes = landRoutes
                .GroupBy(r => r.Origin) // Use the Settlement object as the key for grouping
                .OrderBy(g => g.Key.Name) // Order groups by origin settlement name for consistent file order
                .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Destination.Name).ToList()); // Order routes within group by destination name

            foreach (var group in groupedLandRoutes)
            {
                Settlement origin = group.Key;
                List<LandRoute> routes = group.Value;
                // Determine if this is a custom route or a default one based on the first route in the group.
                // Here, we'll save to the default regional path based on the origin's region.
                string regionFilePath = GetRouteFilePath(origin.Region, RouteType.Land, false); // Default path for land routes

                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(regionFilePath));

                // Append mode to file is no longer needed after clearing. Use true for append for each block.
                using (StreamWriter writer = new StreamWriter(regionFilePath, true))
                {
                    // Use origin.Name directly to preserve casing for headers
                    writer.WriteLine(origin.Name);
                    foreach (var route in routes)
                    {
                        writer.WriteLine(route.ToFileString());
                    }
                    writer.WriteLine(); // Add blank line after the group for readability
                }
            }

            // Group sea routes by origin settlement and then region for writing
            var groupedSeaRoutes = seaRoutes
                .GroupBy(r => r.Origin)
                .OrderBy(g => g.Key.Name)
                .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Destination.Name).ToList());

            foreach (var group in groupedSeaRoutes)
            {
                Settlement origin = group.Key;
                List<SeaRoute> routes = group.Value;
                string regionFilePath = GetRouteFilePath(origin.Region, RouteType.Sea, false); // Default path for sea routes

                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(regionFilePath));

                using (StreamWriter writer = new StreamWriter(regionFilePath, true))
                {
                    writer.WriteLine(origin.Name); // Write header
                    foreach (var route in routes)
                    {
                        writer.WriteLine(route.ToFileString());
                    }
                    writer.WriteLine(); // Add blank line
                }
            }

            // After saving, re-zip the WorldData folder
            CreateWorldDataZip();
            Console.WriteLine("All routes saved successfully.");
        }


        /// <summary>
        /// Clears the content of a specific route file.
        /// Used before writing new, organized data to prevent duplication.
        /// </summary>
        /// <param name="regionName">The region name of the file to clear.</param>
        /// <param name="routeType">The type of route (Land or Sea).</param>
        public void ClearRouteFileContent(string regionName, RouteType routeType)
        {
            string filePath = GetRouteFilePath(regionName, routeType, false); // Get default path
            if (File.Exists(filePath))
            {
                File.WriteAllText(filePath, string.Empty); // Clear content by writing an empty string
                Console.WriteLine($"Cleared content of {filePath}");
            }
        }


        /// <summary>
        /// Loads routes from a specific file, parsing the new header-based format.
        /// </summary>
        /// <param name="filePath">The path to the route file.</param>
        /// <param name="routeType">The type of routes expected in the file (Land or Sea).</param>
        /// <returns>A list of IRoute objects parsed from the file.</returns>
        public List<IRoute> LoadRoutesFromFile(string filePath, RouteType routeType)
        {
            List<IRoute> routes = new List<IRoute>();
            if (!File.Exists(filePath))
            {
                return routes;
            }

            Settlement currentOrigin = null;
            // Extract region from file path for the settlement header
            string regionFromFileName = Path.GetFileNameWithoutExtension(filePath);

            foreach (string line in File.ReadLines(filePath))
            {
                string trimmedLine = line.Trim();

                // Skip empty lines
                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    continue;
                }

                // Check if it's a header (no tab character indicates a settlement name)
                if (!trimmedLine.Contains('\t'))
                {
                    currentOrigin = new Settlement(trimmedLine, regionFromFileName);
                }
                else if (currentOrigin != null)
                {
                    try
                    {
                        if (routeType == RouteType.Land)
                        {
                            LandRoute landRoute = LandRoute.ParseFromFileLine(currentOrigin, trimmedLine);
                            routes.Add(landRoute);
                        }
                        else if (routeType == RouteType.Sea)
                        {
                            SeaRoute seaRoute = SeaRoute.ParseFromFileLine(currentOrigin, trimmedLine);
                            routes.Add(seaRoute);
                        }
                    }
                    catch (FormatException ex)
                    {
                        Console.WriteLine($"Error parsing route line '{trimmedLine}' from '{filePath}': {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"An unexpected error occurred parsing route line '{trimmedLine}' from '{filePath}': {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"Warning: Found route line '{trimmedLine}' in '{filePath}' without a preceding origin settlement header. Skipping.");
                }
            }
            return routes;
        }

        /// <summary>
        /// Saves a single custom route. This method is used for immediate custom route creation
        /// and appends directly to the custom route file. The full file rewrite for consistency
        /// will happen when SaveAllRoutes is called (e.g., when the RouteManager saves its full state).
        /// </summary>
        public void SaveSingleCustomRoute(IRoute route, RouteType routeType, bool isCustom)
        {
            // For custom routes, always save to the CustomRoutes directory
            string basePath = isCustom ? _customRoutesPath : _basePath;
            string routeSpecificPath = "";
            if (routeType == RouteType.Land)
            {
                // MODIFIED: Use ToTitleCaseExceptOf for filename
                routeSpecificPath = Path.Combine(basePath, "Land", $"{ToTitleCaseExceptOf(route.Origin.Region)}.txt");
            }
            else if (routeType == RouteType.Sea)
            {
                // MODIFIED: Use ToTitleCaseExceptOf for filename
                routeSpecificPath = Path.Combine(basePath, "Sea", $"{ToTitleCaseExceptOf(route.Origin.Region)}.txt");
            }

            if (string.IsNullOrEmpty(routeSpecificPath))
            {
                Console.WriteLine("Error: Invalid route type or path generation failed.");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(routeSpecificPath));

            // For saving a single custom route, we append it.
            // The full header-based formatting will happen when SaveAllRoutes is called for all routes.
            using (StreamWriter writer = new StreamWriter(routeSpecificPath, true)) // Append mode
            {
                // Write origin header and route line.
                // The current implementation writes the origin name, then the route, then a newline.
                // This will result in individual custom routes appearing like mini-blocks.
                // The SaveAllRoutes method will consolidate these into proper blocks.
                if (route is LandRoute lr)
                {
                    writer.WriteLine($"{lr.Origin.Name}\n{lr.ToFileString()}\n");
                }
                else if (route is SeaRoute sr)
                {
                    writer.WriteLine($"{sr.Origin.Name}\n{sr.ToFileString()}\n");
                }
                Console.WriteLine($"Single custom route saved to {routeSpecificPath}");
            }
            // Do NOT call CreateWorldDataZip here, it will be called by SaveAllRoutes.
        }

        /// <summary>
        /// Helper to get the correct file path for a given region and route type.
        /// It formats the region name to Title Case for the filename, excluding "of".
        /// </summary>
        private string GetRouteFilePath(string regionName, RouteType routeType, bool isCustom)
        {
            string baseDir = isCustom ? _customRoutesPath : _basePath;
            string subDir = "";

            // Format the region name for the filename to Title Case, excluding "of"
            string formattedRegionFileName = ToTitleCaseExceptOf(regionName);

            if (routeType == RouteType.Land)
            {
                subDir = "LandRoutes";
                if (isCustom) subDir = Path.Combine("Custom", "Land");
            }
            else if (routeType == RouteType.Sea)
            {
                subDir = "SeaRoutes";
                if (isCustom) subDir = Path.Combine("Custom", "Sea");
            }
            return Path.Combine(baseDir, subDir, $"{formattedRegionFileName}.txt");
        }

        /// <summary>
        /// Converts a string to Title Case, specifically handling "of" to remain lowercase.
        /// </summary>
        private string ToTitleCaseExceptOf(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            string[] words = input.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].ToLowerInvariant() == "of" && i != 0) // Keep "of" lowercase unless it's the first word
                {
                    words[i] = "of";
                }
                else
                {
                    words[i] = textInfo.ToTitleCase(words[i].ToLower());
                }
            }
            return string.Join(" ", words);
        }

        // --- Player Data Encryption/Decryption ---

        // Encrypts player data before saving
        public string EncryptPlayerData(string plainText)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = _aesKey;
                aesAlg.IV = _aesIV;

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }
                        return Convert.ToBase64String(msEncrypt.ToArray());
                    }
                }
            }
        }

        // Decrypts player data after loading
        public string DecryptPlayerData(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return string.Empty;

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = _aesKey;
                aesAlg.IV = _aesIV;

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText)))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            return srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Retrieves a Settlement object by its name and region (case-insensitive).
        /// This method should ideally query the in-memory graph managed by RouteManager.
        /// For now, it creates a new Settlement. This might need refactoring
        /// if a global settlement registry is implemented.
        /// </summary>
        /// <param name="name">The name of the settlement.</param>
        /// <param name="region">The region of the settlement.</param>
        /// <returns>A new Settlement object.</returns>
        public Settlement GetSettlementByNameAndRegion(string name, string region)
        {
            // In a real application, this would query a master list of all settlements
            // to return a consistent object. For now, it simply creates one.
            // Pathfinder's graph building ensures consistent settlement objects are used there.
            return new Settlement(name, region);
        }

        /// <summary>
        /// Creates a .zip file of the WorldData directory at the application's root folder.
        /// This zip file can be published to GitHub for players to download.
        /// </summary>
        private void CreateWorldDataZip()
        {
            string zipFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorldData.zip");
            string sourceDirectory = _basePath;

            try
            {
                // Delete existing zip file if it exists to allow overwriting
                if (File.Exists(zipFilePath))
                {
                    File.Delete(zipFilePath);
                    Console.WriteLine($"Deleted existing WorldData.zip at {zipFilePath}.");
                }

                // Ensure the source directory (WorldData) exists before zipping
                if (Directory.Exists(sourceDirectory))
                {
                    // Create the zip file from the WorldData directory
                    ZipFile.CreateFromDirectory(sourceDirectory, zipFilePath, CompressionLevel.Fastest, false);
                    Console.WriteLine($"Successfully created WorldData.zip at {zipFilePath}.");
                }
                else
                {
                    Console.WriteLine($"Warning: WorldData directory ({sourceDirectory}) not found. Cannot create WorldData.zip.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating WorldData.zip: {ex.Message}");
            }
        }
    }
}
