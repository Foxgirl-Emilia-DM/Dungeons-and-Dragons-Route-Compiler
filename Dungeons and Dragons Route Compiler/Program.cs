// Program.cs
using System;
using System.IO;
using System.Linq;
using YourFantasyWorldProject.Classes;
using YourFantasyWorldProject.Managers;
using YourFantasyWorldProject.Pathfinding;
using YourFantasyWorldProject.Utils;

namespace YourFantasyWorldProject
{
    class Program
    {
        private const string DmPasswordFilePath = "dm_password.txt";

        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to the Fantasy World Route Manager!");

            DataManager dataManager = new DataManager(); // EnsureInitialDataPresent is called here
            Pathfinder pathfinder = new Pathfinder(dataManager);
            RouteManager routeManager = new RouteManager(dataManager, pathfinder);

            // Initial graph build after all data is loaded by DataManager's constructor
            routeManager.RebuildGraph(); // Still rebuild graph to ensure the in-memory graph is fresh

            string dmPassword = LoadDmPassword();

            if (dmPassword == null)
            {
                Console.WriteLine("\nDM password file not found or empty. Starting in Player Mode automatically.");
                RunPlayerMenu(pathfinder, dataManager, routeManager); // Pass routeManager to player menu
            }
            else
            {
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
                            string enteredPassword = ConsoleInput.GetStringInput("");
                            if (enteredPassword == dmPassword)
                            {
                                Console.WriteLine("DM access granted.");
                                RunDmMenu(routeManager, pathfinder, dataManager);
                            }
                            else
                            {
                                Console.WriteLine("Incorrect password.");
                            }
                            break;
                        case "2": // Player Mode
                            RunPlayerMenu(pathfinder, dataManager, routeManager); // Pass routeManager to player menu
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

        static void RunDmMenu(RouteManager routeManager, Pathfinder pathfinder, DataManager dataManager)
        {
            bool dmMenuRunning = true;
            while (dmMenuRunning)
            {
                Console.WriteLine("\n--- DM Menu ---");
                Console.WriteLine("1. Add Route (DM controlled: Choose Custom/Default)"); // Clarified DM route creation
                Console.WriteLine("2. Remove Route");
                Console.WriteLine("3. Edit Route");
                Console.WriteLine("4. Validate Files");
                Console.WriteLine("5. Repair Files");
                Console.WriteLine("6. Print Routes for a Region");
                Console.WriteLine("7. Check Total Unique Settlements");
                Console.WriteLine("8. Find Route (Dijkstra's)");
                Console.WriteLine("0. Back to Main Menu");

                string choice = ConsoleInput.GetStringInput("Enter your choice: ");

                switch (choice)
                {
                    case "1":
                        routeManager.AddRoute(); // This now internally uses CreateDmLandRoute/CreateDmSeaRoute
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

        static void RunPlayerMenu(Pathfinder pathfinder, DataManager dataManager, RouteManager routeManager) // Added routeManager parameter
        {
            bool playerMenuRunning = true;
            while (playerMenuRunning)
            {
                Console.WriteLine("\n--- Player Menu ---");
                Console.WriteLine("1. Find Route (Dijkstra's)");
                Console.WriteLine("2. Create Custom Land Route (Player only)"); // Player can only create custom
                Console.WriteLine("3. Create Custom Sea Route (Player only)"); // Player can only create custom
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
                        pathfinder.CreatePlayerCustomLandRoute(); // New method for player-only custom land route
                        routeManager.RebuildGraph(); // Rebuild graph to include new player custom route
                        break;
                    case "3":
                        pathfinder.CreatePlayerCustomSeaRoute(); // New method for player-only custom sea route
                        routeManager.RebuildGraph(); // Rebuild graph to include new player custom route
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

        static string LoadDmPassword()
        {
            try
            {
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DmPasswordFilePath);

                if (File.Exists(fullPath))
                {
                    string password = File.ReadAllText(fullPath).Trim();
                    if (!string.IsNullOrEmpty(password))
                    {
                        Console.WriteLine($"DM password loaded from {fullPath}");
                        return password;
                    }
                    else
                    {
                        Console.WriteLine($"DM password file '{fullPath}' is empty.");
                        return null;
                    }
                }
                else
                {
                    Console.WriteLine($"DM password file '{fullPath}' not found.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading DM password from file: {ex.Message}.");
                return null;
            }
        }
    }
}
