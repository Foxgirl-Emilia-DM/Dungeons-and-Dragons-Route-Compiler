// Program.cs
using System;
using System.IO; // Added for File operations
using System.Linq;
using YourFantasyWorldProject.Classes;
using YourFantasyWorldProject.Managers;
using YourFantasyWorldProject.Pathfinding;
using YourFantasyWorldProject.Utils;

namespace YourFantasyWorldProject
{
    class Program
    {
        // Path to the file storing the DM password
        private const string DmPasswordFilePath = "dm_password.txt";

        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to the Fantasy World Route Manager!");

            // Initialize DataManager, which ensures folder structure exists
            DataManager dataManager = new DataManager();
            // Initialize Pathfinder
            Pathfinder pathfinder = new Pathfinder(dataManager);
            // Initialize RouteManager, which loads all data at startup and depends on Pathfinder
            RouteManager routeManager = new RouteManager(dataManager, pathfinder); // Pass pathfinder here

            // Initial graph build after all data is loaded
            routeManager.RebuildGraph();

            // Attempt to load the DM password. This method will return null if the file isn't found or is empty.
            string dmPassword = LoadDmPassword();

            if (dmPassword == null)
            {
                // If DM password file is not found or empty, skip straight to Player Mode
                Console.WriteLine("\nDM password file not found or empty. Starting in Player Mode automatically.");
                RunPlayerMenu(pathfinder, dataManager); // Pass dataManager to player menu
            }
            else
            {
                // If a DM password was successfully loaded, offer the main menu with DM access.
                bool appRunning = true;
                while (appRunning)
                {
                    Console.WriteLine("\n--- Main Menu ---");
                    Console.WriteLine("1. DM Mode");
                    Console.WriteLine("2. Player Mode");
                    Console.WriteLine("0. Exit");

                    string mainMenuChoice = ConsoleInput.GetStringInput("Enter your choice: ");

                    switch (mainMenuChoice)
                    {
                        case "1": // DM Mode
                            Console.Write("Enter DM password: ");
                            string enteredPassword = ConsoleInput.GetStringInput(""); // Read password without prompt
                            if (enteredPassword == dmPassword)
                            {
                                Console.WriteLine("DM access granted.");
                                RunDmMenu(routeManager, pathfinder, dataManager); // Pass dataManager to DM menu
                            }
                            else
                            {
                                Console.WriteLine("Incorrect password.");
                            }
                            break;
                        case "2": // Player Mode
                            RunPlayerMenu(pathfinder, dataManager); // Pass dataManager to player menu
                            break;
                        case "0":
                            appRunning = false;
                            Console.WriteLine("Exiting application. Goodbye!");
                            break;
                        default:
                            Console.WriteLine("Invalid choice. Please try again.");
                            break;
                    }
                }
            }
        }

        // --- DM Mode Menu ---
        static void RunDmMenu(RouteManager routeManager, Pathfinder pathfinder, DataManager dataManager)
        {
            bool dmMenuRunning = true;
            while (dmMenuRunning)
            {
                Console.WriteLine("\n--- DM Menu ---");
                Console.WriteLine("1. Add Route");
                Console.WriteLine("2. Remove Route");
                Console.WriteLine("3. Edit Route");
                Console.WriteLine("4. Validate Files");
                Console.WriteLine("5. Repair Files");
                Console.WriteLine("6. Print Routes for a Region");
                Console.WriteLine("7. Check Total Unique Settlements");
                Console.WriteLine("8. Find Route (Dijkstra's)");
                Console.WriteLine("9. Create Land Route (Choose Custom/Default)"); // Clarified menu text
                Console.WriteLine("10. Create Sea Route (Choose Custom/Default)"); // New menu option for sea route
                Console.WriteLine("0. Back to Main Menu");

                string choice = ConsoleInput.GetStringInput("Enter your choice: ");

                switch (choice)
                {
                    case "1":
                        routeManager.AddRoute(); // This method now handles custom/default choice internally
                        break;
                    case "2":
                        routeManager.RemoveRoute();
                        break;
                    case "3":
                        routeManager.EditRoute();
                        break;
                    case "4":
                        routeManager.FileValidation();
                        break;
                    case "5":
                        routeManager.FileRepair();
                        break;
                    case "6":
                        routeManager.FilePrint();
                        break;
                    case "7":
                        routeManager.FileCheck();
                        break;
                    case "8":
                        string originNameFind = ConsoleInput.GetStringInput("Origin Settlement Name: ");
                        string originRegionFind = ConsoleInput.GetStringInput("Origin Region Name: ");
                        Settlement originFind = dataManager.GetSettlementByNameAndRegion(originNameFind, originRegionFind);

                        string destinationNameFind = ConsoleInput.GetStringInput("Destination Settlement Name: ");
                        string destinationRegionFind = ConsoleInput.GetStringInput("Destination Region Name: ");
                        Settlement destinationFind = dataManager.GetSettlementByNameAndRegion(destinationNameFind, destinationRegionFind);

                        if (originFind != null && destinationFind != null)
                        {
                            pathfinder.FindPath(
                                origin: originFind,
                                destination: destinationFind,
                                numTravelers: ConsoleInput.GetIntInput("Number of travelers: ", 1),
                                travelSpeed: ConsoleInput.GetStringInput("Travel Speed (Normal, Fast, Slow): "),
                                shipType: ConsoleInput.GetEnumInput<ShipType>("Select Ship Type:"),
                                mountType: ConsoleInput.GetEnumInput<MountType>("Select Mount Type:"),
                                preference: ConsoleInput.GetEnumInput<RoutePreference>("Select Route Preference:")
                            ).DisplayResult();
                        }
                        else
                        {
                            Console.WriteLine("Invalid origin or destination settlement entered for pathfinding.");
                        }
                        break;
                    case "9":
                        pathfinder.CreateLandRoute(); // Calls the renamed method
                        break;
                    case "10": // New case for creating sea routes explicitly
                        pathfinder.CreateSeaRoute(); // Calls the renamed method
                        break;
                    case "0":
                        dmMenuRunning = false;
                        Console.WriteLine("Exiting DM Menu.");
                        break;
                    default:
                        Console.WriteLine("Invalid choice. Please try again.");
                        break;
                }
            }
        }

        // --- Player Mode Menu ---
        static void RunPlayerMenu(Pathfinder pathfinder, DataManager dataManager)
        {
            bool playerMenuRunning = true;
            while (playerMenuRunning)
            {
                Console.WriteLine("\n--- Player Menu ---");
                Console.WriteLine("1. Find Route (Dijkstra's)");
                Console.WriteLine("2. Create Custom Sea Route (for saving)"); // Clarified Player Mode option
                Console.WriteLine("0. Back to Main Menu");

                string choice = ConsoleInput.GetStringInput("Enter your choice: ");

                switch (choice)
                {
                    case "1":
                        string originNamePlayer = ConsoleInput.GetStringInput("Origin Settlement Name: ");
                        string originRegionPlayer = ConsoleInput.GetStringInput("Origin Region Name: ");
                        Settlement originPlayer = dataManager.GetSettlementByNameAndRegion(originNamePlayer, originRegionPlayer);

                        string destinationNamePlayer = ConsoleInput.GetStringInput("Destination Settlement Name: ");
                        string destinationRegionPlayer = ConsoleInput.GetStringInput("Destination Region Name: ");
                        Settlement destinationPlayer = dataManager.GetSettlementByNameAndRegion(destinationNamePlayer, destinationRegionPlayer);

                        if (originPlayer != null && destinationPlayer != null)
                        {
                            pathfinder.FindPath(
                                origin: originPlayer,
                                destination: destinationPlayer,
                                numTravelers: ConsoleInput.GetIntInput("Number of travelers: ", 1),
                                travelSpeed: ConsoleInput.GetStringInput("Travel Speed (Normal, Fast, Slow): "),
                                shipType: ConsoleInput.GetEnumInput<ShipType>("Select Ship Type:"),
                                mountType: ConsoleInput.GetEnumInput<MountType>("Select Mount Type:"),
                                preference: ConsoleInput.GetEnumInput<RoutePreference>("Select Route Preference:")
                            ).DisplayResult();
                        }
                        else
                        {
                            Console.WriteLine("Invalid origin or destination settlement entered for pathfinding.");
                        }
                        break;
                    case "2":
                        // In Player Mode, this will still call the CreateSeaRoute method, which now includes the custom/default choice.
                        // The note emphasizes that this will save the route.
                        Console.WriteLine("Note: This option will create and save a new sea route to your local data.");
                        pathfinder.CreateSeaRoute(); // Calls the renamed method
                        break;
                    case "0":
                        playerMenuRunning = false;
                        Console.WriteLine("Exiting Player Menu.");
                        break;
                    default:
                        Console.WriteLine("Invalid choice. Please try again.");
                        break;
                }
            }
        }

        // --- DM Password Flexibility ---
        /// <summary>
        /// Loads the DM password from a local file.
        /// Returns the password string if found and not empty, otherwise returns null.
        /// </summary>
        static string LoadDmPassword()
        {
            try
            {
                // The full path will be where the .exe is located
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DmPasswordFilePath);

                if (File.Exists(fullPath)) // Check if the file exists at the full path
                {
                    string password = File.ReadAllText(fullPath).Trim(); // Read from the full path
                    if (!string.IsNullOrEmpty(password))
                    {
                        Console.WriteLine($"DM password loaded from {fullPath}");
                        return password;
                    }
                    else
                    {
                        // File exists but is empty
                        Console.WriteLine($"DM password file '{fullPath}' is empty.");
                        return null; // Signal that no valid password was found
                    }
                }
                else
                {
                    // File does not exist
                    Console.WriteLine($"DM password file '{fullPath}' not found.");
                    return null; // Signal that no valid password was found
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading DM password from file: {ex.Message}.");
                return null; // Signal an error occurred
            }
        }
    }
}
