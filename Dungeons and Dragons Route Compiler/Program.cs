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

        static async System.Threading.Tasks.Task Main(string[] args)
        {
            Console.WriteLine("Welcome to the Fantasy World Route Manager!");

            DataManager dataManager = new DataManager(); // EnsureInitialDataPresent is called here
            Pathfinder pathfinder = new Pathfinder(dataManager);
            RouteManager routeManager = new RouteManager(dataManager, pathfinder);

            // Initial graph build after all data is loaded by DataManager's constructor
            // RouteManager's constructor already calls LoadAllRoutes, which in turn calls RebuildGraph
            // So, no explicit call to routeManager.RebuildGraph() is needed here after construction.

            string dmPassword = LoadDmPassword();

            if (dmPassword == null)
            {
                Console.WriteLine("\nDM password file not found or empty. Starting in Player Mode automatically.");
                RunPlayerMenu(pathfinder, routeManager); // Pass routeManager to player menu
            }
            else
            {
                bool appRunning = true;
                while (appRunning)
                {
                    Console.WriteLine("\n--- Main Menu ---");
                    Console.WriteLine("1. Player Menu");
                    Console.WriteLine("2. DM Menu");
                    Console.WriteLine("3. Exit");

                    string choice = ConsoleInput.GetStringInput("Enter your choice: ");

                    switch (choice)
                    {
                        case "1":
                            RunPlayerMenu(pathfinder, routeManager);
                            break;
                        case "2":
                            Console.Write("Enter DM password: ");
                            string enteredPassword = Console.ReadLine();
                            if (enteredPassword == dmPassword)
                            {
                                Console.WriteLine("DM password accepted.");
                                RunDmMenu(pathfinder, routeManager);
                            }
                            else
                            {
                                Console.WriteLine("Incorrect password.");
                            }
                            break;
                        case "3":
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

        static void RunPlayerMenu(Pathfinder pathfinder, RouteManager routeManager)
        {
            bool playerMenuRunning = true;
            while (playerMenuRunning)
            {
                Console.WriteLine("\n--- Player Menu ---");
                Console.WriteLine("1. Find Route");
                Console.WriteLine("2. Create Custom Land Route");
                Console.WriteLine("3. Create Custom Sea Route");
                Console.WriteLine("4. View All Settlements");
                Console.WriteLine("5. Return to Main Menu");

                string choice = ConsoleInput.GetStringInput("Enter your choice: ");

                switch (choice)
                {
                    case "1":
                        Console.WriteLine("\n--- Find Route ---");
                        string startName = ConsoleInput.GetStringInput("Enter origin settlement name: ");
                        string startRegion = ConsoleInput.GetStringInput("Enter origin region name: ");
                        Settlement start = routeManager.GetSettlementByNameAndRegion(startName, startRegion);

                        string endName = ConsoleInput.GetStringInput("Enter destination settlement name: ");
                        string endRegion = ConsoleInput.GetStringInput("Enter destination region name: ");
                        Settlement end = routeManager.GetSettlementByNameAndRegion(endName, endRegion);

                        if (start == null)
                        {
                            Console.WriteLine($"Origin settlement '{startName} ({startRegion})' not found.");
                            break;
                        }
                        if (end == null)
                        {
                            Console.WriteLine($"Destination settlement '{endName} ({endRegion})' not found.");
                            break;
                        }

                        RoutePreference preference = ConsoleInput.GetEnumInput<RoutePreference>("Choose route preference:");

                        // Call FindShortestPath which now handles all detailed prompts and calculations
                        JourneyResult result = pathfinder.FindShortestPath(start, end, preference);
                        result.DisplayResult(); // Display the result to the console

                        // New: Ask to save the route to file (Patch 3)
                        if (result.PathFound && ConsoleInput.GetBooleanInput("Would you like to save this route to a local file?"))
                        {
                            pathfinder.SaveJourneyResultToFile(result);
                        }
                        break;
                    case "2":
                        pathfinder.CreateNewCustomLandRoutePlayer(routeManager);
                        break;
                    case "3":
                        pathfinder.CreateNewCustomSeaRoutePlayer(routeManager);
                        break;
                    case "4":
                        routeManager.DisplayLoadedData();
                        break;
                    case "5":
                        playerMenuRunning = false;
                        Console.WriteLine("Exiting Player Menu.");
                        break;
                    default:
                        Console.WriteLine("Invalid choice. Please try again.");
                        break;
                }
            }
        }

        static void RunDmMenu(Pathfinder pathfinder, RouteManager routeManager)
        {
            bool dmMenuRunning = true;
            while (dmMenuRunning)
            {
                Console.WriteLine("\n--- DM Menu ---");
                Console.WriteLine("1. Find Route (Full Options)");
                Console.WriteLine("2. Create New Land Route (Default)");
                Console.WriteLine("3. Create New Sea Route (Default)");
                Console.WriteLine("4. View All Settlements & Routes (File Check)");
                Console.WriteLine("5. Rebuild Graph (if underlying files changed manually)");
                Console.WriteLine("6. Return to Main Menu");

                string choice = ConsoleInput.GetStringInput("Enter your choice: ");

                switch (choice)
                {
                    case "1":
                        Console.WriteLine("\n--- Find Route (DM Full Options) ---");
                        string startName = ConsoleInput.GetStringInput("Enter origin settlement name: ");
                        string startRegion = ConsoleInput.GetStringInput("Enter origin region name: ");
                        Settlement start = routeManager.GetSettlementByNameAndRegion(startName, startRegion);

                        string endName = ConsoleInput.GetStringInput("Enter destination settlement name: ");
                        string endRegion = ConsoleInput.GetStringInput("Enter destination region name: ");
                        Settlement end = routeManager.GetSettlementByNameAndRegion(endName, endRegion);

                        if (start == null)
                        {
                            Console.WriteLine($"Origin settlement '{startName} ({startRegion})' not found.");
                            break;
                        }
                        if (end == null)
                        {
                            Console.WriteLine($"Destination settlement '{endName} ({endRegion})' not found.");
                            break;
                        }

                        RoutePreference preference = ConsoleInput.GetEnumInput<RoutePreference>("Choose route preference:");

                        // Call FindShortestPath which now handles all detailed prompts and calculations
                        JourneyResult result = pathfinder.FindShortestPath(start, end, preference);
                        result.DisplayResult(); // Display the result to the console

                        // New: Ask to save the route to file (Patch 3)
                        if (result.PathFound && ConsoleInput.GetBooleanInput("Would you like to save this route to a local file?"))
                        {
                            pathfinder.SaveJourneyResultToFile(result);
                        }
                        break;
                    case "2":
                        pathfinder.CreateNewLandRouteDm(routeManager);
                        break;
                    case "3":
                        pathfinder.CreateNewSeaRouteDm(routeManager);
                        break;
                    case "4":
                        routeManager.FileCheck(); // Displays loaded data summary
                        break;
                    case "5":
                        routeManager.RebuildGraph(); // Manually trigger graph rebuild from current files
                        break;
                    case "6":
                        dmMenuRunning = false;
                        Console.WriteLine("Exiting DM Menu.");
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
                Console.WriteLine($"Error loading DM password from file: {ex.Message}.\n" +
                                  $"Please ensure '{DmPasswordFilePath}' is accessible in the application's root directory.");
                return null;
            }
        }
    }
}
