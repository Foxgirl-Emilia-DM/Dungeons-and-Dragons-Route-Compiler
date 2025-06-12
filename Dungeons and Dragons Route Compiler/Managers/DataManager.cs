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

        // IMPORTANT: Replace this with the raw content URL of your public GitHub repository's WorldData folder
        // This repo will now store ENCRYPTED versions of your .txt files for player downloads.
        // Example: https://raw.githubusercontent.com/YourGitHubUser/YourRepoName/main/WorldData/
        private const string GitHubBaseUrl = "https://raw.githubusercontent.com/YOUR_USERNAME/YOUR_REPO_NAME/main/WorldData/";
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
            _basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, basePath);
            _landPath = Path.Combine(_basePath, "LandRoutes");
            _seaPath = Path.Combine(_basePath, "SeaRoutes");
            _customRoutesPath = Path.Combine(_basePath, "CustomRoutes");
            _customLandPath = Path.Combine(_customRoutesPath, "Land");
            _customSeaPath = Path.Combine(_customRoutesPath, "Sea");

            // Ensure directories exist
            Directory.CreateDirectory(_landPath);
            Directory.CreateDirectory(_seaPath);
            Directory.CreateDirectory(_customLandPath);
            Directory.CreateDirectory(_customSeaPath);
        }

        // --- Settlement Management ---
        public Settlement GetSettlementByNameAndRegion(string name, string region)
        {
            // Creates a new settlement. Settlement constructor now stores original casing.
            return new Settlement(name, region);
        }

        // --- Route Loading ---
        public List<LandRoute> LoadLandRoutesFromRegionFile(string regionName)
        {
            List<LandRoute> routes = new List<LandRoute>();
            string defaultFilePath = Path.Combine(_landPath, $"{regionName.ToUpperInvariant()}.txt");
            string customFilePath = Path.Combine(_customLandPath, $"{regionName.ToUpperInvariant()}.txt");

            // Load from default and custom files
            routes.AddRange(LoadRoutesFromFile<LandRoute>(defaultFilePath, regionName, RouteType.Land));
            routes.AddRange(LoadRoutesFromFile<LandRoute>(customFilePath, regionName, RouteType.Land));

            return routes;
        }

        public List<SeaRoute> LoadSeaRoutesFromRegionFile(string regionName)
        {
            List<SeaRoute> routes = new List<SeaRoute>();
            string defaultFilePath = Path.Combine(_seaPath, $"{regionName.ToUpperInvariant()}.txt");
            string customFilePath = Path.Combine(_customSeaPath, $"{regionName.ToUpperInvariant()}.txt");

            // Load from default and custom files
            routes.AddRange(LoadRoutesFromFile<SeaRoute>(defaultFilePath, regionName, RouteType.Sea));
            routes.AddRange(LoadRoutesFromFile<SeaRoute>(customFilePath, regionName, RouteType.Sea));

            return routes;
        }

        /// <summary>
        /// Generic helper to load routes from a specified file path.
        /// </summary>
        private List<T> LoadRoutesFromFile<T>(string filePath, string regionName, RouteType routeType) where T : IRoute
        {
            List<T> routes = new List<T>();

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Route file not found: {Path.GetFileName(filePath)}. Attempting to download from GitHub...");
                // Attempt to download and decrypt from GitHub
                string githubUrlSegment = (routeType == RouteType.Land) ? "LandRoutes" : "SeaRoutes";
                string githubUrl = $"{GitHubBaseUrl}{githubUrlSegment}/{regionName.ToUpperInvariant()}.enc";
                try
                {
                    byte[] encryptedData = _httpClient.GetByteArrayAsync(githubUrl).Result;
                    string decryptedContent = DecryptStringFromBytes_Aes(encryptedData, Key, IV);
                    File.WriteAllText(filePath, decryptedContent); // Save decrypted content to local default path
                    Console.WriteLine($"Downloaded and decrypted route data for {regionName} from GitHub.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to download or decrypt route data for {regionName} from GitHub: {ex.Message}");
                    return routes; // Return empty list if download fails
                }
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
                        continue; // Skip empty lines
                    }

                    if (!trimmedLine.StartsWith("\t")) // This is an origin settlement line
                    {
                        string[] originParts = trimmedLine.Split('\t');
                        string originName = originParts[0].Trim();
                        // For origin region, prioritize what's in the file, otherwise use the filename's region
                        string originRegion = originParts.Length > 1 ? originParts[1].Trim() : Path.GetFileNameWithoutExtension(filePath);
                        currentOrigin = GetSettlementByNameAndRegion(originName, originRegion);
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
            HashSet<string> regions = new HashSet<string>();

            // Get regions from default land route files
            foreach (string filePath in Directory.GetFiles(_landPath, "*.txt"))
            {
                regions.Add(Path.GetFileNameWithoutExtension(filePath));
            }
            // Get regions from custom land route files
            foreach (string filePath in Directory.GetFiles(_customLandPath, "*.txt"))
            {
                regions.Add(Path.GetFileNameWithoutExtension(filePath));
            }

            // Get regions from default sea route files
            foreach (string filePath in Directory.GetFiles(_seaPath, "*.txt"))
            {
                regions.Add(Path.GetFileNameWithoutExtension(filePath));
            }
            // Get regions from custom sea route files
            foreach (string filePath in Directory.GetFiles(_customSeaPath, "*.txt"))
            {
                regions.Add(Path.GetFileNameWithoutExtension(filePath));
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
            string originHeaderContent = $"{route.Origin.Name}\t{route.Origin.Region}"; // Use original casing for header content
            int originIndex = -1;
            for (int i = 0; i < fileContent.Count; i++)
            {
                // To find an existing header, we need to compare its content using invariant casing.
                // Assuming headers are "NAME\tREGION"
                string fileLineTrimmed = fileContent[i].Trim();
                if (!fileLineTrimmed.StartsWith("\t") && !string.IsNullOrWhiteSpace(fileLineTrimmed)) // This is a potential origin header
                {
                    string[] parts = fileLineTrimmed.Split('\t');
                    if (parts.Length >= 1 && parts[0].Trim().ToUpperInvariant().Equals(route.Origin.Name.ToUpperInvariant()))
                    {
                        if (parts.Length == 2 && parts[1].Trim().ToUpperInvariant().Equals(route.Origin.Region.ToUpperInvariant()))
                        {
                            originIndex = i;
                            originFound = true;
                            break;
                        }
                        else if (parts.Length == 1 && string.IsNullOrWhiteSpace(route.Origin.Region)) // Case for 'TINVERKE' without region
                        {
                            originIndex = i;
                            originFound = true;
                            break;
                        }
                    }
                }
            }


            if (!originFound)
            {
                // If origin not found, add its header with original casing.
                // The example shows "TINVERKE" on its own line if it's the only info.
                // If region is also present, it should be "TINVERKE\tKingdom of Faalskarth".
                string newOriginLine = $"{route.Origin.Name}";
                if (!string.IsNullOrWhiteSpace(route.Origin.Region))
                {
                    newOriginLine += $"\t{route.Origin.Region}";
                }
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
                        if (originParts.Length == 0 || originParts.Length > 2)
                        {
                            issuesFound.Add($"Line {lineNumber + 1}: Malformed origin header '{originalLine}'. Skipping.");
                            lineRepaired = true;
                            continue;
                        }
                        string originName = textInfo.ToTitleCase(originParts[0].Trim().ToLower()); // Convert to Title Case for storing
                        string originRegion = originParts.Length == 2 ? textInfo.ToTitleCase(originParts[1].Trim().ToLower()) : Path.GetFileNameWithoutExtension(filePath); // Convert to Title Case
                        currentOrigin = new Settlement(originName, originRegion);

                        // Reconstruct header with original (now title-cased) input
                        string repairedOriginLine = originName;
                        if (!string.IsNullOrWhiteSpace(originRegion) && !originRegion.Equals(Path.GetFileNameWithoutExtension(filePath), StringComparison.OrdinalIgnoreCase))
                        {
                            repairedOriginLine += $"\t{originRegion}";
                        }
                        repairedLines.Add(repairedOriginLine);
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
                                                .Select(s => s.Trim()) // Store as read for now
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

                            // Reconstruct the line with canonical formatting and proper casing for biomes
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
