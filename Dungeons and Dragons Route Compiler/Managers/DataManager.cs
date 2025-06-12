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
        // Keep them secret! This is for demonstration.
        // A real key/IV should be 32 bytes for AES-256 and 16 bytes for IV.
        // You can generate them once and keep them fixed.
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("YourSuperSecretKey12345678901234567890"); // 32 bytes for AES-256
        private static readonly byte[] IV = Encoding.UTF8.GetBytes("YourInitialization"); // 16 bytes for AES

        public DataManager(string basePath = "WorldData")
        {
            // _basePath will point to the WorldData folder which will be extracted directly into the app's base directory
            _basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, basePath);
            _landPath = Path.Combine(_basePath, "LandRoutes");
            _seaPath = Path.Combine(_basePath, "SeaRoutes");
            _customRoutesPath = Path.Combine(_basePath, "CustomRoutes");
            _customLandPath = Path.Combine(_customRoutesPath, "Land");
            _customSeaPath = Path.Combine(_customRoutesPath, "Sea");

            // Call this at DataManager instantiation to ensure data is present/updated
            EnsureInitialDataPresent();
        }

        // --- Settlement Management ---
        public Settlement GetSettlementByNameAndRegion(string name, string region)
        {
            return new Settlement(name, region);
        }

        /// <summary>
        /// Ensures that the WorldData directory is up-to-date.
        /// It backs up existing custom routes, downloads and extracts the latest WorldData.zip
        /// (overwriting default data), and then restores the custom routes.
        /// This method is designed to be called once at application startup.
        /// </summary>
        private void EnsureInitialDataPresent()
        {
            Console.WriteLine("\nChecking for latest WorldData...");

            // Step 1: Backup existing custom routes if they exist
            Dictionary<string, List<string>> backedUpCustomLandRoutes = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> backedUpCustomSeaRoutes = new Dictionary<string, List<string>>();
            bool customDataBackedUp = false;

            // Ensure custom directories exist before attempting to get files from them for backup
            Directory.CreateDirectory(_customLandPath);
            Directory.CreateDirectory(_customSeaPath);

            if (Directory.Exists(_customLandPath))
            {
                foreach (string file in Directory.GetFiles(_customLandPath, "*.txt"))
                {
                    backedUpCustomLandRoutes[Path.GetFileName(file)] = File.ReadAllLines(file).ToList();
                    customDataBackedUp = true;
                }
            }
            if (Directory.Exists(_customSeaPath))
            {
                foreach (string file in Directory.GetFiles(_customSeaPath, "*.txt"))
                {
                    backedUpCustomSeaRoutes[Path.GetFileName(file)] = File.ReadAllLines(file).ToList();
                    customDataBackedUp = true;
                }
            }

            if (customDataBackedUp)
            {
                Console.WriteLine("Backed up existing custom route data.");
            }

            // Step 2: Attempt to download and extract the latest WorldData.zip
            string zipUrl = $"{GitHubBaseUrl}WorldData.zip"; // Explicitly point to WorldData.zip within the base URL folder
            string localZipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorldData.zip"); // Save zip in app's base dir

            bool downloadedSuccessfully = false;

            try
            {
                Console.WriteLine($"Attempting to download WorldData.zip from: {zipUrl}");
                byte[] zipData = _httpClient.GetByteArrayAsync(zipUrl).Result;
                File.WriteAllBytes(localZipPath, zipData);
                Console.WriteLine($"Downloaded WorldData.zip to {localZipPath}.");
                downloadedSuccessfully = true;

                // Delete existing _basePath (WorldData folder) to ensure a clean slate for extraction.
                // This will remove old default data.
                if (Directory.Exists(_basePath))
                {
                    Console.WriteLine($"Cleaning existing {_basePath} directory before extraction.");
                    Directory.Delete(_basePath, true);
                }

                // Extract the zip file directly to the application's base directory.
                // This assumes WorldData folder is at the root of the zip file.
                ZipFile.ExtractToDirectory(localZipPath, AppDomain.CurrentDomain.BaseDirectory, true);
                Console.WriteLine($"Extracted WorldData.zip to {AppDomain.CurrentDomain.BaseDirectory}.");

                // Delete the downloaded zip file after extraction
                File.Delete(localZipPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to download or unpack WorldData.zip from GitHub: {ex.Message}");
                downloadedSuccessfully = false; // Mark failure
                // If download fails, ensure _basePath (WorldData) and its subdirectories exist
                // for the app to function with whatever local data it might have.
                if (!Directory.Exists(_basePath))
                {
                    Directory.CreateDirectory(_basePath);
                }
                // Ensure default data subdirectories exist even if zip failed
                Directory.CreateDirectory(_landPath);
                Directory.CreateDirectory(_seaPath);
            }

            // Step 3: Restore custom routes
            if (customDataBackedUp)
            {
                Console.WriteLine("Restoring custom route data...");
                // Ensure custom directories exist before restoring (might have been deleted by clean step)
                Directory.CreateDirectory(_customLandPath);
                Directory.CreateDirectory(_customSeaPath);

                foreach (var entry in backedUpCustomLandRoutes)
                {
                    File.WriteAllLines(Path.Combine(_customLandPath, entry.Key), entry.Value);
                }
                foreach (var entry in backedUpCustomSeaRoutes)
                {
                    File.WriteAllLines(Path.Combine(_customSeaPath, entry.Key), entry.Value);
                }
                Console.WriteLine("Custom route data restored.");
            }
            else
            {
                Console.WriteLine("No custom data to restore.");
                // Ensure custom directories are created even if no data was backed up
                Directory.CreateDirectory(_customLandPath);
                Directory.CreateDirectory(_customSeaPath);
            }

            Console.WriteLine("WorldData check complete.");
        }


        // --- Route Loading ---
        // LoadLandRoutesFromRegionFile and LoadSeaRoutesFromRegionFile no longer call EnsureInitialDataPresent,
        // as it's now called once at DataManager instantiation.
        public List<LandRoute> LoadLandRoutesFromRegionFile(string regionName)
        {
            List<LandRoute> routes = new List<LandRoute>();
            string defaultFilePath = Path.Combine(_landPath, $"{regionName.ToUpperInvariant()}.txt");
            string customFilePath = Path.Combine(_customLandPath, $"{regionName.ToUpperInvariant()}.txt");

            // Load from existing local files.
            if (File.Exists(defaultFilePath))
            {
                routes.AddRange(LoadRoutesFromSpecificFile<LandRoute>(defaultFilePath, regionName, RouteType.Land));
            }
            if (File.Exists(customFilePath))
            {
                routes.AddRange(LoadRoutesFromSpecificFile<LandRoute>(customFilePath, regionName, RouteType.Land));
            }
            return routes;
        }

        public List<SeaRoute> LoadSeaRoutesFromRegionFile(string regionName)
        {
            List<SeaRoute> routes = new List<SeaRoute>();
            string defaultFilePath = Path.Combine(_seaPath, $"{regionName.ToUpperInvariant()}.txt");
            string customFilePath = Path.Combine(_customSeaPath, $"{regionName.ToUpperInvariant()}.txt");

            // Load from existing local files.
            if (File.Exists(defaultFilePath))
            {
                routes.AddRange(LoadRoutesFromSpecificFile<SeaRoute>(defaultFilePath, regionName, RouteType.Sea));
            }
            if (File.Exists(customFilePath))
            {
                routes.AddRange(LoadRoutesFromSpecificFile<SeaRoute>(customFilePath, regionName, RouteType.Sea));
            }
            return routes;
        }

        /// <summary>
        /// Helper to load routes from a specific file path. Does not attempt GitHub downloads itself.
        /// </summary>
        private List<T> LoadRoutesFromSpecificFile<T>(string filePath, string regionName, RouteType routeType) where T : IRoute
        {
            List<T> routes = new List<T>();

            if (!File.Exists(filePath))
            {
                return routes;
            }

            try
            {
                string[] lines = File.ReadAllLines(filePath);
                Settlement currentOrigin = null;

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        continue;
                    }

                    if (!trimmedLine.StartsWith("\t")) // This is an origin settlement line
                    {
                        string[] originParts = trimmedLine.Split('\t');
                        string originName = originParts[0].Trim();
                        // Infer region from filename if not explicitly provided in the line or if provided region mismatches
                        string regionFromFileName = Path.GetFileNameWithoutExtension(filePath); // Filename is already uppercase
                        string effectiveRegion = regionFromFileName; // Default to filename region

                        if (originParts.Length > 1) // If region is provided in the line itself
                        {
                            string providedRegion = originParts[1].Trim();
                            if (!string.Equals(providedRegion, regionFromFileName, StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine($"Warning: Line '{line}' in file '{Path.GetFileName(filePath)}' has region '{providedRegion}' which differs from filename region '{regionFromFileName}'. Using '{regionFromFileName}'.");
                            }
                        }
                        currentOrigin = GetSettlementByNameAndRegion(originName, effectiveRegion);
                    }
                    else // This is a route line from the currentOrigin
                    {
                        if (currentOrigin == null)
                        {
                            Console.WriteLine($"Warning: Skipping route line '{line}' as no origin settlement was defined.");
                            continue;
                        }
                        try
                        {
                            if (routeType == RouteType.Land && typeof(T) == typeof(LandRoute))
                            {
                                routes.Add((T)(object)LandRoute.ParseFromFileLine(currentOrigin, line));
                            }
                            else if (routeType == RouteType.Sea && typeof(T) == typeof(SeaRoute))
                            {
                                routes.Add((T)(object)SeaRoute.ParseFromFileLine(currentOrigin, line));
                            }
                        }
                        catch (FormatException ex)
                        {
                            Console.WriteLine($"Error parsing route line '{line}': {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"An unexpected error occurred while parsing route line '{line}': {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading route file {filePath}: {ex.Message}");
            }

            return routes;
        }

        public List<string> GetAllRegions()
        {
            // Ensure data is present before enumerating directories
            // EnsureInitialDataPresent() is called in constructor, so no need to call it here.

            HashSet<string> regions = new HashSet<string>();

            // Get regions from default land route files
            if (Directory.Exists(_landPath))
            {
                foreach (string filePath in Directory.GetFiles(_landPath, "*.txt"))
                {
                    regions.Add(Path.GetFileNameWithoutExtension(filePath));
                }
            }
            // Get regions from custom land route files
            if (Directory.Exists(_customLandPath))
            {
                foreach (string filePath in Directory.GetFiles(_customLandPath, "*.txt"))
                {
                    regions.Add(Path.GetFileNameWithoutExtension(filePath));
                }
            }

            // Get regions from default sea route files
            if (Directory.Exists(_seaPath))
            {
                foreach (string filePath in Directory.GetFiles(_seaPath, "*.txt"))
                {
                    regions.Add(Path.GetFileNameWithoutExtension(filePath));
                }
            }
            // Get regions from custom sea route files
            if (Directory.Exists(_customSeaPath))
            {
                foreach (string filePath in Directory.GetFiles(_customSeaPath, "*.txt"))
                {
                    regions.Add(Path.GetFileNameWithoutExtension(filePath));
                }
            }

            return regions.ToList();
        }


        // --- Route Saving ---
        /// <summary>
        /// Saves a route to the appropriate file (default or custom).
        /// This method will append to an existing file or create a new one.
        /// It ensures the correct file format is maintained.
        /// </summary>
        /// <param name="route">The route to save.</param>
        /// <param name="routeType">The type of route (Land or Sea).</param>
        /// <param name="isCustom">True if the route should be saved to the custom routes folder, false for default.</param>
        public void SaveRoute(IRoute route, RouteType routeType, bool isCustom)
        {
            // Use the Origin's region to determine the file name
            string region = route.Origin.Region;

            string targetDir = "";
            string filePath = "";

            if (routeType == RouteType.Land)
            {
                targetDir = isCustom ? _customLandPath : _landPath;
                filePath = Path.Combine(targetDir, $"{region.ToUpperInvariant()}.txt"); // Filename is always uppercase region
            }
            else if (routeType == RouteType.Sea)
            {
                targetDir = isCustom ? _customSeaPath : _seaPath;
                filePath = Path.Combine(targetDir, $"{region.ToUpperInvariant()}.txt"); // Filename is always uppercase region
            }
            else
            {
                Console.WriteLine("Invalid route type for saving.");
                return;
            }

            // Ensure the directory exists
            Directory.CreateDirectory(targetDir);

            List<string> fileContent = new List<string>();
            bool originFound = false;

            if (File.Exists(filePath))
            {
                fileContent = File.ReadAllLines(filePath).ToList();
            }

            // Check if the origin settlement already exists as a header in the file
            // Compare using the invariant uppercase name for lookup
            string originHeaderContent = $"{route.Origin.Name}"; // Start with just the name
            // No longer adding region to origin header in file, as it's inferred from filename
            // if (!string.IsNullOrWhiteSpace(route.Origin.Region))
            // {
            //     originHeaderContent += $"\t{route.Origin.Region}";
            // }

            int originIndex = -1;
            for (int i = 0; i < fileContent.Count; i++)
            {
                string fileLineTrimmed = fileContent[i].Trim();
                // An origin header does NOT start with a tab
                if (!fileLineTrimmed.StartsWith("\t") && !string.IsNullOrWhiteSpace(fileLineTrimmed))
                {
                    string[] parts = fileLineTrimmed.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                    // Check if name matches (case-insensitive)
                    if (parts.Length >= 1 && parts[0].Trim().ToUpperInvariant().Equals(route.Origin.Name.ToUpperInvariant()))
                    {
                        // The origin header in the file should now *only* contain the name if we follow the new format
                        // Or at most, the name and *its* region, but we are moving away from that.
                        // For finding an existing header, just compare the name part.
                        originIndex = i;
                        originFound = true;
                        break;
                    }
                }
            }


            if (!originFound)
            {
                // If origin not found, add its header with original casing, just the name.
                string newOriginLine = route.Origin.Name;
                fileContent.Add(newOriginLine);
                originIndex = fileContent.Count - 1; // It's now the last line
            }

            // Append the new route line after the origin header or at the end if the origin was just added
            // Ensure no duplicate routes are added.
            string newRouteLine = routeType == RouteType.Land ? ((LandRoute)route).ToFileString() : ((SeaRoute)route).ToFileString();
            bool routeExists = false;
            // Iterate from originIndex + 1 to find existing routes from this origin
            for (int i = originIndex + 1; i < fileContent.Count; i++)
            {
                // Routes always start with a tab
                if (fileContent[i].TrimStart().StartsWith("\t") && fileContent[i].Trim().Equals(newRouteLine.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    routeExists = true;
                    Console.WriteLine("Route already exists in the file. Skipping save.");
                    break;
                }
                // Stop if we hit another origin header (a line that doesn't start with a tab)
                if (!fileContent[i].TrimStart().StartsWith("\t") && !string.IsNullOrWhiteSpace(fileContent[i].Trim()))
                {
                    break;
                }
            }

            if (!routeExists)
            {
                // Find the correct insertion point for the new route
                // It should be after the origin header, and before the next origin header (if any)
                int insertionPoint = originIndex + 1;
                while (insertionPoint < fileContent.Count && fileContent[insertionPoint].TrimStart().StartsWith("\t"))
                {
                    insertionPoint++;
                }
                fileContent.Insert(insertionPoint, newRouteLine);
                File.WriteAllLines(filePath, fileContent);
                Console.WriteLine($"Route saved to {Path.GetFileName(filePath)} in the {(isCustom ? "custom" : "default")} folder.");
            }
        }


        // --- ENCRYPTION/DECRYPTION for player data ---
        private byte[] EncryptStringToBytes_Aes(string plainText, byte[] Key, byte[] IV)
        {
            if (plainText == null || plainText.Length <= 0)
                throw new ArgumentNullException("plainText");
            if (Key == null || Key.Length <= 0)
                throw new ArgumentNullException("Key");
            if (IV == null || IV.Length <= 0)
                throw new ArgumentNullException("IV");

            byte[] encrypted;

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }
            return encrypted;
        }

        private string DecryptStringFromBytes_Aes(byte[] cipherText, byte[] Key, byte[] IV)
        {
            if (cipherText == null || cipherText.Length <= 0)
                throw new ArgumentNullException("cipherText");
            if (Key == null || Key.Length <= 0)
                throw new ArgumentNullException("Key");
            if (IV == null || IV.Length <= 0)
                throw new ArgumentNullException("IV");

            string plaintext = null;

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
            return plaintext;
        }


        // --- File Validation and Repair ---
        /// <summary>
        /// Validates and attempts to repair a route file based on its type.
        /// </summary>
        /// <param name="filePath">The path to the file to validate.</param>
        /// <param name="routeType">The type of routes expected in the file.</param>
        /// <returns>A list of issues found and/or repaired.</returns>
        public List<string> ValidateAndRepairFile(string filePath, RouteType routeType)
        {
            List<string> issuesFound = new List<string>();
            List<string> repairedLines = new List<string>();
            bool fileChanged = false;
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

            if (!File.Exists(filePath))
            {
                issuesFound.Add($"File not found: {filePath}. Cannot validate or repair.");
                return issuesFound;
            }

            try
            {
                string[] lines = File.ReadAllLines(filePath);
                Settlement currentOrigin = null;

                for (int lineNumber = 0; lineNumber < lines.Length; lineNumber++)
                {
                    string originalLine = lines[lineNumber];
                    string trimmedLine = originalLine.Trim();
                    bool lineRepaired = false;

                    if (string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        repairedLines.Add(originalLine);
                        continue;
                    }

                    if (!trimmedLine.StartsWith("\t")) // This is an origin settlement header
                    {
                        string[] originParts = trimmedLine.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                        if (originParts.Length == 0 || originParts.Length > 2) // Max 2 parts (Name, Region)
                        {
                            issuesFound.Add($"Line {lineNumber + 1}: Malformed origin header '{originalLine}'. Skipping.");
                            lineRepaired = true;
                            continue;
                        }
                        string originName = textInfo.ToTitleCase(originParts[0].Trim().ToLower()); // Convert to Title Case for storing

                        // Always infer region from filename for validation/repair consistency
                        string regionFromFileName = Path.GetFileNameWithoutExtension(filePath);
                        string originRegion = regionFromFileName; // Use filename region

                        // If the original line had a region and it doesn't match the filename, log a warning
                        if (originParts.Length == 2 && !string.Equals(originParts[1].Trim(), regionFromFileName, StringComparison.OrdinalIgnoreCase))
                        {
                            string providedRegion = originParts[1].Trim();
                            Console.WriteLine($"Warning: Line '{originalLine}' in file '{Path.GetFileName(filePath)}' has region '{providedRegion}' which differs from filename region '{regionFromFileName}'. Using '{regionFromFileName}'.");
                            issuesFound.Add($"Line {lineNumber + 1}: Region '{providedRegion}' in origin header differs from filename region '{regionFromFileName}'. Auto-correcting to filename region.");
                            lineRepaired = true;
                        }

                        currentOrigin = new Settlement(originName, originRegion);

                        // Reconstruct header with just the origin name for future compatibility
                        // This makes the file format cleaner.
                        string repairedOriginLine = originName;
                        repairedLines.Add(repairedOriginLine); // Now only name is stored in header
                        if (lineRepaired || !originalLine.Equals(repairedOriginLine)) fileChanged = true;

                    }
                    else // This is a route definition line (starts with tab)
                    {
                        if (currentOrigin == null)
                        {
                            issuesFound.Add($"Line {lineNumber + 1}: Route definition found without a preceding origin settlement. Skipping: '{originalLine}'");
                            lineRepaired = true;
                            continue;
                        }

                        if (routeType == RouteType.Land)
                        {
                            var parts = trimmedLine.Split('\t', StringSplitOptions.None);
                            if (parts.Length != 6)
                            {
                                issuesFound.Add($"Line {lineNumber + 1}: Invalid land route format. Expected 6 tab-separated parts, got {parts.Length}. Attempting repair.");
                                lineRepaired = true;
                            }

                            string destName = parts.Length > 1 ? textInfo.ToTitleCase(parts[1].Trim().ToLower()) : "Unknown_Destination";
                            string destRegion = parts.Length > 2 ? textInfo.ToTitleCase(parts[2].Trim().ToLower()) : "Unknown_Region";
                            List<string> biomes = new List<string>();
                            List<double> biomeDistances = new List<double>();
                            bool isMapped = false;

                            if (parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3]))
                            {
                                biomes = parts[3].Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                .Select(s => s.Trim())
                                                .ToList();
                            }
                            if (parts.Length > 4 && !string.IsNullOrWhiteSpace(parts[4]))
                            {
                                biomeDistances = parts[4].Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                        .Select(s => double.TryParse(s.Replace("km", "").Trim(), out double val) && val > 0 ? val : 1.0)
                                                        .ToList();
                            }
                            if (parts.Length > 5 && !string.IsNullOrWhiteSpace(parts[5]))
                            {
                                bool.TryParse(parts[5].Trim(), out isMapped);
                            }

                            if (biomes.Count != biomeDistances.Count)
                            {
                                issuesFound.Add($"Line {lineNumber + 1}: Mismatch in biome and distance count. Repairing by truncating to smaller count.");
                                lineRepaired = true;
                                int minCount = Math.Min(biomes.Count, biomeDistances.Count);
                                biomes = biomes.Take(minCount).ToList();
                                biomeDistances = biomeDistances.Take(minCount).ToList();
                            }
                            if (!biomes.Any()) { biomes.Add("Unknown"); biomeDistances.Add(1.0); lineRepaired = true; issuesFound.Add($"Line {lineNumber + 1}: No biomes found, setting to Unknown."); }

                            repairedLines.Add(
                                $"\t{destName}" +
                                $"\t{destRegion}" +
                                $"\t{string.Join(",", biomes.Select(b => textInfo.ToTitleCase(b.ToLower())))}" +
                                $"\t{string.Join(",", biomeDistances.Select(d => $"{d}km"))}" +
                                $"\t{isMapped}"
                            );

                            if (lineRepaired) fileChanged = true;
                        }
                        else if (routeType == RouteType.Sea)
                        {
                            var parts = trimmedLine.Split('\t', StringSplitOptions.None);
                            if (parts.Length != 4)
                            {
                                issuesFound.Add($"Line {lineNumber + 1}: Invalid sea route format. Expected 4 tab-separated parts, got {parts.Length}. Attempting repair.");
                                lineRepaired = true;
                            }

                            string destName = parts.Length > 1 ? textInfo.ToTitleCase(parts[1].Trim().ToLower()) : "Unknown_Destination";
                            string destRegion = parts.Length > 2 ? textInfo.ToTitleCase(parts[2].Trim().ToLower()) : "Unknown_Region";
                            double distance = (parts.Length > 3 && double.TryParse(parts[3].Replace("km", "").Trim(), out double val) && val > 0) ? val : 1.0;

                            repairedLines.Add(
                                $"\t{destName}" +
                                $"\t{destRegion}" +
                                $"\t{distance}km"
                            );

                            if (lineRepaired) fileChanged = true;
                        }
                    }
                }

                if (fileChanged)
                {
                    File.WriteAllLines(filePath, repairedLines);
                    issuesFound.Add($"File {filePath} was repaired and saved with updated formatting.");
                }
            }
            catch (Exception ex)
            {
                issuesFound.Add($"An error occurred during validation/repair of {filePath}: {ex.Message}");
            }

            return issuesFound;
        }

        // --- File Printing Helpers ---
        public void PrintFileContent(string filePath)
        {
            if (File.Exists(filePath))
            {
                Console.WriteLine($"--- Content of {Path.GetFileName(filePath)} ---");
                foreach (string line in File.ReadLines(filePath))
                {
                    Console.WriteLine(line);
                }
                Console.WriteLine($"--- End of {Path.GetFileName(filePath)} ---");
            }
            else
            {
                Console.WriteLine($"File not found: {Path.GetFileName(filePath)}");
            }
        }
    }
}
