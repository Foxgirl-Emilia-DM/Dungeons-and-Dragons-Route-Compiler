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
                RunPlayerMenu(pathfinder);
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
                                RunDmMenu(routeManager, pathfinder);
                            }
                            else
                            {
                                Console.WriteLine("Incorrect password.");
                            }
                            break;
                        case "2": // Player Mode
                            RunPlayerMenu(pathfinder);
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
        static void RunDmMenu(RouteManager routeManager, Pathfinder pathfinder)
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
                Console.WriteLine("9. Custom Sea Route Calculation");
                Console.WriteLine("0. Back to Main Menu");

                string choice = ConsoleInput.GetStringInput("Enter your choice: ");

                switch (choice)
                {
                    case "1":
                        routeManager.AddRoute();
                        routeManager.RebuildGraph(); // Rebuild graph after any data modification
                        break;
                    case "2":
                        routeManager.RemoveRoute();
                        routeManager.RebuildGraph();
                        break;
                    case "3":
                        routeManager.EditRoute();
                        routeManager.RebuildGraph();
                        break;
                    case "4":
                        routeManager.FileValidation();
                        break;
                    case "5":
                        routeManager.FileRepair();
                        routeManager.RebuildGraph(); // Rebuild graph after repair
                        break;
                    case "6":
                        routeManager.FilePrint();
                        break;
                    case "7":
                        routeManager.FileCheck();
                        break;
                    case "8":
                        pathfinder.FindRoute();
                        break;
                    case "9":
                        // Assuming CustomSeaRoute might add a route that needs to be part of the graph
                        pathfinder.CustomSeaRoute();
                        routeManager.RebuildGraph(); // Rebuild graph after custom route creation
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
        static void RunPlayerMenu(Pathfinder pathfinder)
        {
            bool playerMenuRunning = true;
            while (playerMenuRunning)
            {
                Console.WriteLine("\n--- Player Menu ---");
                Console.WriteLine("1. Find Route (Dijkstra's)");
                Console.WriteLine("2. Custom Sea Route Calculation");
                Console.WriteLine("0. Back to Main Menu");

                string choice = ConsoleInput.GetStringInput("Enter your choice: ");

                switch (choice)
                {
                    case "1":
                        pathfinder.FindRoute();
                        break;
                    case "2":
                        pathfinder.CustomSeaRoute();
                        // Player-created custom sea routes are not automatically added to the main graph
                        // but are calculated on the fly. If you want them saved and part of the main graph,
                        // you'd need to add save logic here and then rebuild the graph in DM mode.
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
