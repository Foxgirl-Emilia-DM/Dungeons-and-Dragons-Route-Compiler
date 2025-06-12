using System;
using System.Collections.Generic;
using System.Linq;
using YourFantasyWorldProject.Classes;
using YourFantasyWorldProject.Managers;
using YourFantasyWorldProject.Pathfinding;
using YourFantasyWorldProject.Utils;

namespace YourFantasyWorldProject
{ 
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to the Fantasy World Route Manager!");

            // Initialize DataManager, which ensures folder structure exists
            DataManager dataManager = new DataManager();
            // Initialize RouteManager, which loads all data at startup
            RouteManager routeManager = new RouteManager(dataManager);
            // Initialize Pathfinder, passing the data manager (or lists of routes directly if graph is not built on init)
            Pathfinding.Pathfinder pathfinder = new Pathfinder(dataManager);

            // Important: Build the graph after initial data load
            pathfinder.BuildGraph(routeManager.AllLandRoutes, routeManager.AllSeaRoutes);

            bool running = true;
            while (running)
            {
                Console.WriteLine("\n--- Main Menu ---");
                Console.WriteLine("1. Add Route");
                Console.WriteLine("2. Remove Route");
                Console.WriteLine("3. Edit Route");
                Console.WriteLine("4. Validate Files");
                Console.WriteLine("5. Repair Files");
                Console.WriteLine("6. Print Routes for a Settlement");
                Console.WriteLine("7. Check Total Unique Settlements");
                Console.WriteLine("8. Find Route (Dijkstra's)");
                Console.WriteLine("9. Custom Sea Route Calculation");
                Console.WriteLine("0. Exit");

                string choice = ConsoleInput.GetStringInput("Enter your choice: ");

                switch (choice)
                {
                    case "1":
                        routeManager.AddRoute();
                        // Rebuild graph after any data modification
                        pathfinder.BuildGraph(routeManager.AllLandRoutes, routeManager.AllSeaRoutes);
                        break;
                    case "2":
                        routeManager.RemoveRoute();
                        pathfinder.BuildGraph(routeManager.AllLandRoutes, routeManager.AllSeaRoutes);
                        break;
                    case "3":
                        routeManager.EditRoute();
                        pathfinder.BuildGraph(routeManager.AllLandRoutes, routeManager.AllSeaRoutes);
                        break;
                    case "4":
                        routeManager.FileValidation();
                        break;
                    case "5":
                        routeManager.FileRepair();
                        pathfinder.BuildGraph(routeManager.AllLandRoutes, routeManager.AllSeaRoutes); // Rebuild graph after repair
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
                        pathfinder.CustomSeaRoute();
                        break;
                    case "0":
                        running = false;
                        Console.WriteLine("Exiting application. Goodbye!");
                        break;
                    default:
                        Console.WriteLine("Invalid choice. Please try again.");
                        break;
                }
            }
        }
    }
}