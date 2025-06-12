// DataManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YourFantasyWorldProject.Classes;
using System.Net.Http;
using System.Security.Cryptography; // Added for encryption

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
        private static readonly byte[] EncryptionKey = { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20 }; // 32 bytes for AES-256
        private static readonly byte[] EncryptionIV = { 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2E, 0x2F, 0x30 }; // 16 bytes for IV

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
        private string GetFilePath(string regionName, RouteType routeType)
        {
            string folder = routeType == RouteType.Land ? _landPath : _seaPath;
            return Path.Combine(folder, $"{regionName}.txt");
        }

        private string GetCustomFilePath(string regionName, RouteType routeType)
        {
            string folder = routeType == RouteType.Land ? _customLandPath : _customSeaPath;
            return Path.Combine(folder, $"Custom_{regionName}.txt");
        }

        public IEnumerable<string> GetAllRegionNames()
        {
            var landFiles = Directory.GetFiles(_landPath, "*.txt").Select(f => Path.GetFileNameWithoutExtension(f));
            var seaFiles = Directory.GetFiles(_seaPath, "*.txt").Select(f => Path.GetFileNameWithoutExtension(f));
            var customLandFiles = Directory.GetFiles(_customLandPath, "Custom_*.txt")
                                            .Select(f => Path.GetFileNameWithoutExtension(f).Replace("Custom_", ""));
            var customSeaFiles = Directory.GetFiles(_customSeaPath, "Custom_*.txt")
                                            .Select(f => Path.GetFileNameWithoutExtension(f).Replace("Custom_", ""));

            HashSet<string> allRegionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in landFiles.Concat(seaFiles).Concat(customLandFiles).Concat(customSeaFiles))
            {
                allRegionNames.Add(file);
            }
            return allRegionNames;
        }

        // --- New: Encryption/Decryption Methods ---
        private byte[] EncryptData(byte[] data, byte[] key, byte[] iv)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = key;
                aesAlg.IV = iv;
                aesAlg.Mode = CipherMode.CBC; // CBC mode is common
                aesAlg.Padding = PaddingMode.PKCS7; // Standard padding

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        csEncrypt.Write(data, 0, data.Length);
                        csEncrypt.FlushFinalBlock();
                    }
                    return msEncrypt.ToArray();
                }
            }
        }

        private byte[] DecryptData(byte[] data, byte[] key, byte[] iv)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = key;
                aesAlg.IV = iv;
                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.PKCS7;

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msDecrypt = new MemoryStream(data))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (MemoryStream msPlain = new MemoryStream())
                        {
                            csDecrypt.CopyTo(msPlain);
                            return msPlain.ToArray();
                        }
                    }
                }
            }
        }

        // --- Modified: Download file from GitHub (now downloads encrypted bytes and decrypts) ---
        private string DownloadFileFromGitHub(string githubRelativePath)
        {
            string url = GitHubBaseUrl + githubRelativePath;
            Console.WriteLine($"Attempting to download encrypted data from: {url}");
            try
            {
                // Download raw bytes (encrypted content)
                byte[] encryptedBytes = _httpClient.GetByteArrayAsync(url).Result;

                // Decrypt the bytes
                byte[] decryptedBytes = DecryptData(encryptedBytes, EncryptionKey, EncryptionIV);

                // Convert decrypted bytes to string (assuming UTF-8 encoding)
                string fileContent = System.Text.Encoding.UTF8.GetString(decryptedBytes);
                Console.WriteLine($"Successfully downloaded and decrypted: {Path.GetFileName(githubRelativePath)}");
                return fileContent;
            }
            catch (HttpRequestException httpEx)
            {
                if (httpEx.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"File not found on GitHub: {Path.GetFileName(githubRelativePath)} ({url}) - This might be normal if a region has no data for this type.");
                }
                else
                {
                    Console.WriteLine($"HTTP Error downloading {Path.GetFileName(githubRelativePath)} from GitHub: {httpEx.Message}");
                }
                return null;
            }
            catch (CryptographicException cryptoEx)
            {
                Console.WriteLine($"Encryption/Decryption Error for {Path.GetFileName(githubRelativePath)}: {cryptoEx.Message}. This might indicate a wrong key/IV or corrupted encrypted data.");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading/decrypting {Path.GetFileName(githubRelativePath)} from GitHub: {ex.Message}");
                return null;
            }
        }


        // --- Core Loading Methods (Modified to use the updated DownloadFileFromGitHub) ---
        public (List<LandRoute> landRoutes, List<SeaRoute> seaRoutes) LoadRoutes(string regionName)
        {
            List<LandRoute> landRoutes = new List<LandRoute>();
            List<SeaRoute> seaRoutes = new List<SeaRoute>();

            // --- Load Regular Land Routes ---
            string landFilePath = GetFilePath(regionName, RouteType.Land);
            string landGitHubRelativePath = $"Land/{regionName}.txt";

            if (File.Exists(landFilePath))
            {
                // If local file exists (for DM's machine), load it as plaintext
                landRoutes.AddRange(ParseLandFile(landFilePath));
            }
            else
            {
                // If local file doesn't exist (for players or new regions), attempt to download and decrypt from GitHub
                string downloadedContent = DownloadFileFromGitHub(landGitHubRelativePath);
                if (downloadedContent != null)
                {
                    // Parse from the decrypted string content
                    landRoutes.AddRange(ParseLandContent(downloadedContent, landGitHubRelativePath)); // Use new ParseLandContent
                }
            }

            // --- Load Regular Sea Routes ---
            string seaFilePath = GetFilePath(regionName, RouteType.Sea);
            string seaGitHubRelativePath = $"Sea/{regionName}.txt";
            if (File.Exists(seaFilePath))
            {
                // If local file exists, load it
                seaRoutes.AddRange(ParseSeaFile(seaFilePath));
            }
            else
            {
                // Attempt to download and decrypt from GitHub
                string downloadedContent = DownloadFileFromGitHub(seaGitHubRelativePath);
                if (downloadedContent != null)
                {
                    seaRoutes.AddRange(ParseSeaContent(downloadedContent, seaGitHubRelativePath)); // Use new ParseSeaContent
                }
            }

            // --- Load Custom Land Routes (always prioritize local if present) ---
            string customLandFilePath = GetCustomFilePath(regionName, RouteType.Land);
            if (File.Exists(customLandFilePath))
            {
                landRoutes.AddRange(ParseLandFile(customLandFilePath));
            }

            // --- Load Custom Sea Routes (always prioritize local if present) ---
            string customSeaFilePath = GetCustomFilePath(regionName, RouteType.Sea);
            if (File.Exists(customSeaFilePath))
            {
                seaRoutes.AddRange(ParseSeaFile(customSeaFilePath));
            }

            return (landRoutes, seaRoutes);
        }

        // --- Parsing Methods (Now also with versions for content strings) ---
        private List<LandRoute> ParseLandFile(string filePath)
        {
            try
            {
                return ParseLandContent(File.ReadAllText(filePath), filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading land route file {Path.GetFileName(filePath)}: {ex.Message}");
                return new List<LandRoute>();
            }
        }

        private List<LandRoute> ParseLandContent(string content, string sourceIdentifier) // New: Parses from string content
        {
            List<LandRoute> routes = new List<LandRoute>();
            string currentOriginName = null;
            string currentOriginRegion = null;

            using (StringReader reader = new StringReader(content))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        continue;
                    }

                    if (!trimmedLine.StartsWith("\t"))
                    {
                        var parts = trimmedLine.Split(',');
                        if (parts.Length == 2)
                        {
                            currentOriginName = parts[0].Trim();
                            currentOriginRegion = parts[1].Trim();
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Malformed origin line in {sourceIdentifier}: {line}");
                            currentOriginName = null;
                            currentOriginRegion = null;
                        }
                    }
                    else if (currentOriginName != null && currentOriginRegion != null)
                    {
                        string[] parts = trimmedLine.Substring(1).Split('\t');

                        if (parts.Length >= 5)
                        {
                            string destName = parts[0].Trim();
                            string destRegion = parts[1].Trim();
                            List<string> biomes = parts[2].Split(',').Select(b => b.Trim()).ToList();
                            List<double> biomeDistances = parts[3].Split(',')
                                                                  .Select(d => double.TryParse(d.Replace("km", "").Trim(), out double val) ? val : 0)
                                                                  .ToList();
                            bool isMapped = bool.TryParse(parts[4].Trim(), out bool mapped) && mapped;

                            Settlement origin = new Settlement(currentOriginName, currentOriginRegion);
                            Settlement destination = new Settlement(destName, destRegion);

                            routes.Add(new LandRoute(origin, destination, biomes, biomeDistances, isMapped));
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Malformed land route line in {sourceIdentifier} for origin {currentOriginName}: {line}");
                        }
                    }
                }
            }
            return routes;
        }

        private List<SeaRoute> ParseSeaFile(string filePath)
        {
            try
            {
                return ParseSeaContent(File.ReadAllText(filePath), filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading sea route file {Path.GetFileName(filePath)}: {ex.Message}");
                return new List<SeaRoute>();
            }
        }

        private List<SeaRoute> ParseSeaContent(string content, string sourceIdentifier) // New: Parses from string content
        {
            List<SeaRoute> routes = new List<SeaRoute>();
            string currentOriginName = null;
            string currentOriginRegion = null;

            using (StringReader reader = new StringReader(content))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        continue;
                    }

                    if (!trimmedLine.StartsWith("\t"))
                    {
                        var parts = trimmedLine.Split(',');
                        if (parts.Length == 2)
                        {
                            currentOriginName = parts[0].Trim();
                            currentOriginRegion = parts[1].Trim();
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Malformed origin line in {sourceIdentifier}: {line}");
                            currentOriginName = null;
                            currentOriginRegion = null;
                        }
                    }
                    else if (currentOriginName != null && currentOriginRegion != null)
                    {
                        string[] parts = trimmedLine.Substring(1).Split('\t');

                        if (parts.Length >= 3)
                        {
                            string destName = parts[0].Trim();
                            string destRegion = parts[1].Trim();
                            double distance = double.TryParse(parts[2].Replace("km", "").Trim(), out double val) ? val : 0;

                            Settlement origin = new Settlement(currentOriginName, currentOriginRegion);
                            Settlement destination = new Settlement(destName, destRegion);

                            routes.Add(new SeaRoute(origin, destination, distance));
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Malformed sea route line in {sourceIdentifier} for origin {currentOriginName}: {line}");
                        }
                    }
                }
            }
            return routes;
        }

        // --- Helper: Get Settlement by Name and Region ---
        public Settlement GetSettlementByNameAndRegion(string name, string region)
        {
            return new Settlement(name, region);
        }

        // --- New: FindLandRoute (placeholder) ---
        public LandRoute FindLandRoute(string originName, string originRegion, string destName, string destRegion, List<LandRoute> allLandRoutes)
        {
            Console.WriteLine($"DataManager.FindLandRoute called for {originName} ({originRegion}) to {destName} ({destRegion}). Implementation needed.");
            return null;
        }

        // --- New: FindSeaRoute (placeholder) ---
        public SeaRoute FindSeaRoute(string originName, string originRegion, string destName, string destRegion, List<SeaRoute> allSeaRoutes)
        {
            Console.WriteLine($"DataManager.FindSeaRoute called for {originName} ({originRegion}) to {destName} ({destRegion}). Implementation needed.");
            return null;
        }


        // --- Saving Methods ---

        public void SaveRoutes<T>(List<T> routes, RouteType routeType) where T : IRoute
        {
            string filePath = null;
            string customFilePath = null;

            if (routes == null || !routes.Any())
            {
                string regionName = routes.FirstOrDefault()?.Origin.Region;
                if (string.IsNullOrEmpty(regionName)) return;

                filePath = GetFilePath(regionName, routeType);
                customFilePath = GetCustomFilePath(regionName, routeType);

                if (File.Exists(filePath)) File.WriteAllText(filePath, string.Empty);
                if (File.Exists(customFilePath)) File.WriteAllText(customFilePath, string.Empty);

                Console.WriteLine($"All {routeType} routes for region {regionName} cleared from files.");
                return;
            }

            string targetRegion = routes.First().Origin.Region;
            filePath = GetFilePath(targetRegion, routeType);
            customFilePath = GetCustomFilePath(targetRegion, routeType);

            var groupedRoutes = routes.GroupBy(r => new { r.Origin.Name, r.Origin.Region });

            try
            {
                string tempFilePath = filePath + ".tmp";
                using (StreamWriter writer = new StreamWriter(tempFilePath))
                {
                    foreach (var group in groupedRoutes)
                    {
                        writer.WriteLine($"{group.Key.Name},{group.Key.Region}");

                        foreach (var route in group)
                        {
                            if (routeType == RouteType.Land && route is LandRoute landRoute)
                            {
                                writer.WriteLine(landRoute.ToFileString());
                            }
                            else if (routeType == RouteType.Sea && route is SeaRoute seaRoute)
                            {
                                writer.WriteLine(seaRoute.ToFileString());
                            }
                        }
                        writer.WriteLine();
                    }
                }

                File.Delete(filePath);
                File.Move(tempFilePath, filePath);
                Console.WriteLine($"Successfully saved {routes.Count} {routeType} routes to {Path.GetFileName(filePath)}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving {routeType} routes to {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        public void SaveCustomRoute(IRoute route, RouteType routeType, string regionName)
        {
            string filePath = GetCustomFilePath(regionName, routeType);

            try
            {
                bool originHeaderExists = false;
                if (File.Exists(filePath))
                {
                    foreach (string line in File.ReadLines(filePath))
                    {
                        string trimmedLine = line.Trim();
                        if (!trimmedLine.StartsWith("\t") && !string.IsNullOrWhiteSpace(trimmedLine))
                        {
                            var parts = trimmedLine.Split(',');
                            if (parts.Length == 2 && parts[0].Trim().Equals(route.Origin.Name, StringComparison.OrdinalIgnoreCase) && parts[1].Trim().Equals(route.Origin.Region, StringComparison.OrdinalIgnoreCase))
                            {
                                originHeaderExists = true;
                                break;
                            }
                        }
                    }
                }

                using (StreamWriter writer = new StreamWriter(filePath, append: true))
                {
                    if (!originHeaderExists)
                    {
                        writer.WriteLine($"{route.Origin.Name},{route.Origin.Region}");
                    }

                    if (routeType == RouteType.Land && route is LandRoute landRoute)
                    {
                        writer.WriteLine(landRoute.ToFileString());
                    }
                    else if (routeType == RouteType.Sea && route is SeaRoute seaRoute)
                    {
                        writer.WriteLine(seaRoute.ToFileString());
                    }
                    writer.WriteLine();
                }
                Console.WriteLine($"Custom {routeType} route from {route.Origin.Name} to {route.Destination.Name} saved to {Path.GetFileName(filePath)}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving custom {routeType} route to {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        private List<SeaRoute> LoadCustomSeaRoutes(string regionName)
        {
            List<SeaRoute> customRoutes = new List<SeaRoute>();
            string filePath = GetCustomFilePath(regionName, RouteType.Sea);
            if (File.Exists(filePath))
            {
                customRoutes.AddRange(ParseSeaFile(filePath));
            }
            return customRoutes;
        }

        // --- File Validation Helpers ---
        public bool IsFileFormatValid(string filePath, RouteType type)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"File not found: {filePath}");
                return false;
            }

            bool isValid = true;
            string currentOriginName = null;
            string currentOriginRegion = null;
            int lineNumber = 0;

            foreach (string line in File.ReadLines(filePath))
            {
                lineNumber++;
                string trimmedLine = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    continue;
                }

                if (!trimmedLine.StartsWith("\t"))
                {
                    var parts = trimmedLine.Split(',');
                    if (parts.Length == 2)
                    {
                        currentOriginName = parts[0].Trim();
                        currentOriginRegion = parts[1].Trim();
                        if (string.IsNullOrWhiteSpace(currentOriginName) || string.IsNullOrWhiteSpace(currentOriginRegion))
                        {
                            Console.WriteLine($"Validation Error (Line {lineNumber}): Origin name or region is empty. Line: '{line}'");
                            isValid = false;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Validation Error (Line {lineNumber}): Malformed origin line. Expected 'Name,Region'. Line: '{line}'");
                        isValid = false;
                        currentOriginName = null;
                        currentOriginRegion = null;
                    }
                }
                else
                {
                    if (currentOriginName == null || currentOriginRegion == null)
                    {
                        Console.WriteLine($"Validation Error (Line {lineNumber}): Route detail found without a valid origin settlement. Line: '{line}'");
                        isValid = false;
                    }

                    string[] parts = trimmedLine.Substring(1).Split('\t');

                    if (type == RouteType.Land)
                    {
                        if (parts.Length >= 5)
                        {
                            string destName = parts[0].Trim();
                            string destRegion = parts[1].Trim();
                            List<string> biomes = parts[2].Split(',').Select(b => b.Trim()).ToList();
                            List<double> biomeDistances = parts[3].Split(',')
                                                                  .Select(d => double.TryParse(d.Replace("km", "").Trim(), out double val) ? val : 0)
                                                                  .ToList();
                            bool isMapped;

                            if (string.IsNullOrWhiteSpace(destName) || string.IsNullOrWhiteSpace(destRegion))
                            {
                                Console.WriteLine($"Validation Error (Line {lineNumber}): Land route destination name or region is empty. Line: '{line}'");
                                isValid = false;
                            }
                            if (biomes.Any(b => string.IsNullOrWhiteSpace(b)))
                            {
                                Console.WriteLine($"Validation Error (Line {lineNumber}): Land route biome name is empty. Line: '{line}'");
                                isValid = false;
                            }
                            if (biomeDistances.Any(d => d <= 0))
                            {
                                Console.WriteLine($"Validation Error (Line {lineNumber}): Land route biome distance is invalid or non-positive. Line: '{line}'");
                                isValid = false;
                            }
                            if (biomes.Count != biomeDistances.Count)
                            {
                                Console.WriteLine($"Validation Error (Line {lineNumber}): Land route biome count and distance count mismatch. Line: '{line}'");
                                isValid = false;
                            }
                            if (!bool.TryParse(parts[4].Trim(), out isMapped))
                            {
                                Console.WriteLine($"Validation Error (Line {lineNumber}): Land route 'IsMapped' value is invalid. Line: '{line}'");
                                isValid = false;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Validation Error (Line {lineNumber}): Malformed land route detail line. Expected 5 tab-separated parts. Line: '{line}'");
                            isValid = false;
                        }
                    }
                    else if (type == RouteType.Sea)
                    {
                        if (parts.Length >= 3)
                        {
                            string destName = parts[0].Trim();
                            string destRegion = parts[1].Trim();
                            double distance;

                            if (string.IsNullOrWhiteSpace(destName) || string.IsNullOrWhiteSpace(destRegion))
                            {
                                Console.WriteLine($"Validation Error (Line {lineNumber}): Sea route destination name or region is empty. Line: '{line}'");
                                isValid = false;
                            }
                            if (!double.TryParse(parts[2].Replace("km", "").Trim(), out distance) || distance <= 0)
                            {
                                Console.WriteLine($"Validation Error (Line {lineNumber}): Sea route distance is invalid or non-positive. Line: '{line}'");
                                isValid = false;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Validation Error (Line {lineNumber}): Malformed sea route detail line. Expected 3 tab-separated parts. Line: '{line}'");
                            isValid = false;
                        }
                    }
                }
            }
            return isValid;
        }

        // --- Repair Methods ---
        public List<string> RepairFile(string filePath, RouteType type)
        {
            List<string> repairedLines = new List<string>();
            List<string> issuesFound = new List<string>();
            string currentOriginName = null;
            string currentOriginRegion = null;
            int lineNumber = 0;

            foreach (string line in File.ReadLines(filePath))
            {
                lineNumber++;
                string trimmedLine = line.Trim();
                bool lineRepaired = false;

                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    repairedLines.Add(line);
                    continue;
                }

                if (!trimmedLine.StartsWith("\t"))
                {
                    var parts = trimmedLine.Split(',');
                    if (parts.Length == 2)
                    {
                        string name = parts[0].Trim();
                        string region = parts[1].Trim();
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            issuesFound.Add($"Line {lineNumber}: Origin name is empty. Setting to 'UNKNOWN_SETTLEMENT'.");
                            name = "UNKNOWN_SETTLEMENT";
                            lineRepaired = true;
                        }
                        if (string.IsNullOrWhiteSpace(region))
                        {
                            issuesFound.Add($"Line {lineNumber}: Origin region is empty. Setting to 'UNKNOWN_REGION'.");
                            region = "UNKNOWN_REGION";
                            lineRepaired = true;
                        }
                        currentOriginName = name;
                        currentOriginRegion = region;
                        repairedLines.Add($"{currentOriginName},{currentOriginRegion}");
                    }
                    else
                    {
                        issuesFound.Add($"Line {lineNumber}: Malformed origin line. Expected 'Name,Region'. Skipping line.");
                        currentOriginName = null;
                        currentOriginRegion = null;
                    }
                }
                else
                {
                    if (currentOriginName == null || currentOriginRegion == null)
                    {
                        issuesFound.Add($"Line {lineNumber}: Route detail found without a valid origin settlement. Skipping line.");
                        continue;
                    }

                    string[] parts = trimmedLine.Substring(1).Split('\t');

                    if (type == RouteType.Land)
                    {
                        if (parts.Length < 5)
                        {
                            issuesFound.Add($"Line {lineNumber}: Malformed land route line. Expected 5 tab-separated parts. Attempting repair with defaults.");
                            Array.Resize(ref parts, 5);
                            for (int i = 0; i < parts.Length; i++)
                            {
                                if (parts[i] == null) parts[i] = "";
                            }
                            lineRepaired = true;
                        }

                        string destName = string.IsNullOrWhiteSpace(parts[0].Trim()) ? "UNKNOWN_DEST" : parts[0].Trim();
                        string destRegion = string.IsNullOrWhiteSpace(parts[1].Trim()) ? "UNKNOWN_REGION" : parts[1].Trim();
                        List<string> biomes = parts[2].Split(',')
                                                    .Select(b => string.IsNullOrWhiteSpace(b.Trim()) ? "UNKNOWN_BIOME" : b.Trim())
                                                    .ToList();
                        List<double> biomeDistances = parts[3].Split(',')
                                                              .Select(d => double.TryParse(d.Replace("km", "").Trim(), out double val) && val > 0 ? val : 1.0)
                                                              .ToList();
                        bool isMapped = bool.TryParse(parts[4].Trim(), out bool mapped) && mapped;

                        if (biomes.Count != biomeDistances.Count)
                        {
                            issuesFound.Add($"Line {lineNumber}: Biome count mismatch. Repairing by truncating/padding.");
                            int maxCount = Math.Max(biomes.Count, biomeDistances.Count);
                            while (biomes.Count < maxCount) biomes.Add("UNKNOWN_BIOME");
                            while (biomeDistances.Count < maxCount) biomeDistances.Add(1.0);
                            biomes = biomes.Take(maxCount).ToList();
                            biomeDistances = biomeDistances.Take(maxCount).ToList();
                            lineRepaired = true;
                        }

                        repairedLines.Add($"\t{destName}\t{destRegion}\t{string.Join(", ", biomes)}\t{string.Join(", ", biomeDistances.Select(d => $"{d}km"))}\t{isMapped}");
                    }
                    else if (type == RouteType.Sea)
                    {
                        if (parts.Length < 3)
                        {
                            issuesFound.Add($"Line {lineNumber}: Malformed sea route line. Expected 3 tab-separated parts. Attempting repair with defaults.");
                            Array.Resize(ref parts, 3);
                            for (int i = 0; i < parts.Length; i++)
                            {
                                if (parts[i] == null) parts[i] = "";
                            }
                            lineRepaired = true;
                        }

                        string destName = string.IsNullOrWhiteSpace(parts[0].Trim()) ? "UNKNOWN_DEST" : parts[0].Trim();
                        string destRegion = string.IsNullOrWhiteSpace(parts[1].Trim()) ? "UNKNOWN_REGION" : parts[1].Trim();
                        double distance = double.TryParse(parts[2].Replace("km", "").Trim(), out double val) && val > 0 ? val : 1.0;

                        repairedLines.Add($"\t{destName}\t{destRegion}\t{distance}km");
                    }

                    if (lineRepaired)
                    {
                        issuesFound.Add($"Line {lineNumber}: Repaired '{trimmedLine}'.");
                    }
                }
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