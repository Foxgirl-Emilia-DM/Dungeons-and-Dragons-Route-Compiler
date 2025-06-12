// Pathfinder.cs
using System;
using System.Collections.Generic;
using System.Linq;
using YourFantasyWorldProject.Classes; // For Settlement, LandRoute, SeaRoute, Enums, BiomeModifier, new classes
using YourFantasyWorldProject.Managers;
using YourFantasyWorldProject.Utils; // For ConsoleInput
using System.Globalization; // For TextInfo
using System.IO; // For Path, File
using System.Text; // For Encoding

namespace YourFantasyWorldProject.Pathfinding
{
    public class Pathfinder
    {
        private readonly DataManager _dataManager;
        private Dictionary<Settlement, List<Edge>> _graph;

        // Constants for D&D 5e speed conversions
        private const double MPH_TO_KMH_FACTOR = 1.60934;
        private const double NORMAL_WALK_MPH = 3; // Default foot travel speed

        // Resource consumption rates (per person per day)
        private const double RATIONS_PER_PERSON_PER_DAY = 1;
        private const double RATION_COST_GOLD_PER_DAY = 0.5; // 5 silver pieces = 0.5 gold

        private const double WATER_PER_PERSON_PER_DAY_LAND = 1; // Liters
        private const double WATER_PER_PERSON_PER_DAY_SEA_OR_DESERT = 2; // Liters (less access to fresh water / hot conditions)
        private const double WATER_COST_GOLD_PER_LITER = 0.01; // Example: 0.1 gold per 10 liters, so 0.01 per liter

        // Mount speeds (in MPH)
        private static readonly Dictionary<MountType, double> _mountSpeedsMph = new Dictionary<MountType, double>
        {
            { MountType.None, 0 }, // Handled by walk speeds
            { MountType.Foot, 3 }, // 3 mph is 24 miles per day at 8 hours, or 32 miles per day at 10 hours
            { MountType.DraftHorse, 3 }, // Slower due to pulling capacity
            { MountType.RidingHorse, 6 }, // Typical riding horse speed
            { MountType.Warhorse, 6 },
            { MountType.Pony, 4 },
            { MountType.Mastiff, 3 } // Can be mounted by small creatures
        };

        // Ship speeds (in MPH)
        private static readonly Dictionary<ShipType, double> _shipSpeedsMph = new Dictionary<ShipType, double>
        {
            { ShipType.None, 0 },
            { ShipType.Rowboat, 1.5 },
            { ShipType.Keelboat, 2 },
            { ShipType.Longship, 3 },
            { ShipType.Galley, 4 },
            { ShipType.SailingShip, 2 }, // Assumes average sailing conditions
            { ShipType.Warship, 5 } // A faster, dedicated warship
        };

        // Mount carrying/drawing capacities (lbs)
        private static readonly Dictionary<MountType, double> _mountDrawingCapacityLbs = new Dictionary<MountType, double>
        {
            { MountType.DraftHorse, 540 },
            { MountType.RidingHorse, 480 },
            { MountType.Warhorse, 540 },
            { MountType.Pony, 225 },
            { MountType.Mastiff, 195 } // For small riders/cargo
        };

        // Mount costs per day (gold)
        private static readonly Dictionary<MountType, double> _mountCostsPerDayGold = new Dictionary<MountType, double>
        {
            { MountType.DraftHorse, 50 },
            { MountType.RidingHorse, 75 },
            { MountType.Warhorse, 400 },
            { MountType.Pony, 30 },
            { MountType.Mastiff, 25 }
        };

        private const double ANIMAL_FEED_COST_GOLD_PER_DAY_PER_ANIMAL = 0.05; // 5 copper pieces per day per animal

        // Vehicle costs and weights
        private const double WAGON_WEIGHT_LBS = 400;
        private const double WAGON_COST_GOLD = 35;
        private const double CARRIAGE_WEIGHT_LBS = 600;
        private const double CARRIAGE_COST_GOLD = 100;

        public Pathfinder(DataManager dataManager)
        {
            _dataManager = dataManager;
            _graph = new Dictionary<Settlement, List<Edge>>();
        }

        // Represents an edge in our graph
        private class Edge
        {
            public Settlement Target { get; }
            public double Distance { get; } // Distance in KM
            public RouteType Type { get; }
            public IRoute Route { get; } // Reference to the actual route object

            public Edge(Settlement target, double distance, RouteType type, IRoute route)
            {
                Target = target;
                Distance = distance;
                Type = type;
                Route = route;
            }
        }

        /// <summary>
        /// Clears the current graph.
        /// </summary>
        public void ClearGraph()
        {
            _graph.Clear();
        }

        /// <summary>
        /// Builds the graph from a list of land and sea routes.
        /// </summary>
        public void BuildGraph(List<LandRoute> landRoutes, List<SeaRoute> seaRoutes)
        {
            ClearGraph(); // Clear existing graph before rebuilding

            // Add all settlements to the graph to ensure they are keys, even if isolated
            foreach (var route in landRoutes.Cast<IRoute>().Concat(seaRoutes))
            {
                if (!_graph.ContainsKey(route.Origin))
                {
                    _graph[route.Origin] = new List<Edge>();
                }
                if (!_graph.ContainsKey(route.Destination))
                {
                    _graph[route.Destination] = new List<Edge>();
                }
            }

            // Add land routes
            foreach (var route in landRoutes)
            {
                // Add forward route
                _graph[route.Origin].Add(new Edge(route.Destination, route.TotalDistance, RouteType.Land, route));
            }

            // Add sea routes
            foreach (var route in seaRoutes)
            {
                // Add forward route
                _graph[route.Origin].Add(new Edge(route.Destination, route.Distance, RouteType.Sea, route));
            }
        }

        /// <summary>
        /// Finds the shortest path between two settlements using Dijkstra's algorithm,
        /// and then calculates detailed journey costs based on user inputs.
        /// </summary>
        /// <param name="startSettlement">The origin settlement.</param>
        /// <param name="endSettlement">The destination settlement.</param>
        /// <param name="routePreference">User's preference for route types (LandOnly, SeaOnly, Mixed).</param>
        /// <returns>A JourneyResult object containing path details, time, and costs.</returns>
        public JourneyResult FindShortestPath(Settlement startSettlement, Settlement endSettlement, RoutePreference routePreference)
        {
            // Input validation
            if (!_graph.ContainsKey(startSettlement) || !_graph.ContainsKey(endSettlement))
            {
                Console.WriteLine("One or both settlements not found in the loaded world data.");
                return new JourneyResult(startSettlement, endSettlement, null, null, 0, MountType.None, ShipType.None, "none", 0, 0, 0, 0, 0, false, false);
            }

            // Dijkstra's algorithm setup
            var distances = new Dictionary<Settlement, double>();
            var previous = new Dictionary<Settlement, IRoute>(); // Stores the route object used to reach this settlement
            var priorityQueue = new MinHeap<Settlement>();

            foreach (var settlement in _graph.Keys)
            {
                distances[settlement] = double.MaxValue;
                previous[settlement] = null;
            }

            distances[startSettlement] = 0;
            priorityQueue.Enqueue(startSettlement, 0);

            HashSet<Settlement> visited = new HashSet<Settlement>();

            while (priorityQueue.Count > 0)
            {
                Settlement current = priorityQueue.Dequeue();

                if (current.Equals(endSettlement))
                {
                    break; // Path found
                }

                if (visited.Contains(current))
                {
                    continue;
                }
                visited.Add(current);

                // Check neighbors
                if (_graph.TryGetValue(current, out List<Edge> edges))
                {
                    foreach (var edge in edges)
                    {
                        // Apply route preference filtering
                        if ((routePreference == RoutePreference.LandOnly && edge.Type == RouteType.Sea) ||
                            (routePreference == RoutePreference.SeaOnly && edge.Type == RouteType.Land))
                        {
                            continue; // Skip routes that don't match preference
                        }

                        double newDist = distances[current] + edge.Distance; // Initial distance, will be adjusted later for actual travel time/cost

                        if (newDist < distances[edge.Target])
                        {
                            distances[edge.Target] = newDist;
                            previous[edge.Target] = edge.Route; // Store the route itself
                            priorityQueue.Enqueue(edge.Target, newDist);
                        }
                    }
                }
            }

            // Reconstruct path
            List<JourneySegment> pathSegments = new List<JourneySegment>();
            if (previous[endSettlement] == null && !startSettlement.Equals(endSettlement))
            {
                Console.WriteLine("No path found (during pathfinding phase).");
                return new JourneyResult(startSettlement, endSettlement, null, null, 0, MountType.None, ShipType.None, "none", 0, 0, 0, 0, 0, false, false);
            }

            Settlement step = endSettlement;
            while (!step.Equals(startSettlement) && previous[step] != null)
            {
                IRoute route = previous[step];
                Settlement originOfRoute = route.Origin.Equals(step) ? route.Destination : route.Origin; // Determine actual origin of this segment in the path reconstruction

                // If the reconstructed route goes Destination -> Origin, swap for consistent segment representation
                if (!originOfRoute.Equals(startSettlement) && !originOfRoute.Equals(pathSegments.FirstOrDefault()?.Start ?? endSettlement))
                {
                    // This means the route pulled from 'previous' is the reverse of what we need for this segment.
                    // Swap origin and destination to reflect the forward movement in the path.
                    var actualOrigin = route.Destination.Equals(step) ? route.Origin : route.Destination;
                    var actualDestination = route.Origin.Equals(step) ? route.Destination : route.Origin;
                    route = (route is LandRoute tempLandRoute) ? new LandRoute(actualOrigin, actualDestination, tempLandRoute.Biomes, tempLandRoute.BiomeDistances, tempLandRoute.IsMapped) :
                            (route is SeaRoute sr) ? new SeaRoute(actualOrigin, actualDestination, sr.Distance) : route;

                    step = actualOrigin;
                }
                else
                {
                    step = originOfRoute;
                }

                double segmentDistance = (route is LandRoute currentSegmentRoute) ? currentSegmentRoute.TotalDistance : (route as SeaRoute).Distance;
                List<string> biomesTraversed = (route is LandRoute routeBiomes) ? routeBiomes.Biomes : new List<string>();

                pathSegments.Insert(0, new JourneySegment(route.Origin, route.Destination, segmentDistance, 0, 0, route, biomesTraversed)); // Time and Cost are initially 0, will be calculated later
            }


            // --- Patch 2 & 3: Detailed Journey Calculation ---

            // 1. Get number of travelers
            int numTravelers = ConsoleInput.GetIntInput("Enter the number of travelers:", 1);

            // 2. Determine transport types
            MountType chosenLandTransport = MountType.None;
            ShipType chosenSeaTransport = ShipType.None;
            string vehicleChoice = "none";
            List<double> travelerWeights = new List<double>();

            bool hasLandSegments = pathSegments.Any(s => s.RouteUsed is LandRoute);
            bool hasSeaSegments = pathSegments.Any(s => s.RouteUsed is SeaRoute);

            if (hasLandSegments)
            {
                chosenLandTransport = ConsoleInput.GetEnumInput<MountType>("Choose your land transport type:");
            }
            if (hasSeaSegments)
            {
                chosenSeaTransport = ConsoleInput.GetEnumInput<ShipType>("Choose your sea transport type:");
            }

            // 3. Ask for daily travel hours
            double travelHoursPerDay = ConsoleInput.GetDoubleInput("Enter how many hours you intend to travel per day (e.g., 8, 10, 12):", 1);
            if (travelHoursPerDay > 8)
            {
                Console.WriteLine("\n*** WARNING: Traveling more than 8 hours per day is considered a forced march! ***");
                Console.WriteLine("    This may incur penalties (e.g., exhaustion, reduced movement speed in official D&D rules) not explicitly modeled here.");
            }

            // 4. Vehicle and Traveler Weight Input (if land transport is mounted)
            double totalTravelerWeightLbs = 0;
            double vehicleWeight = 0;
            double baseVehicleCost = 0; // Cost before ownership
            bool ownsVehicle = false; // New: Patch 3

            if (chosenLandTransport != MountType.None && chosenLandTransport != MountType.Foot)
            {
                vehicleChoice = ConsoleInput.GetStringInput("Do you intend to use a wagon, carriage, or neither? (wagon/carriage/none): ").ToLowerInvariant();
                switch (vehicleChoice)
                {
                    case "wagon":
                        vehicleWeight = WAGON_WEIGHT_LBS;
                        baseVehicleCost = WAGON_COST_GOLD;
                        break;
                    case "carriage":
                        vehicleWeight = CARRIAGE_WEIGHT_LBS;
                        baseVehicleCost = CARRIAGE_COST_GOLD;
                        break;
                    default:
                        vehicleChoice = "none";
                        break;
                }

                if (!string.IsNullOrEmpty(vehicleChoice) && vehicleChoice != "none")
                {
                    ownsVehicle = ConsoleInput.GetBooleanInput($"Do you own the {vehicleChoice}?");
                }

                Console.WriteLine("\nEnter combined body weight and carry weight for each traveler (in pounds).");
                for (int i = 0; i < numTravelers; i++)
                {
                    double weight = ConsoleInput.GetDoubleInput($"Traveler {i + 1} weight (lbs): ", 0);
                    travelerWeights.Add(weight);
                    totalTravelerWeightLbs += weight;
                }
            }

            bool ownsMounts = false; // New: Patch 3
            if (chosenLandTransport != MountType.None && chosenLandTransport != MountType.Foot && hasLandSegments)
            {
                ownsMounts = ConsoleInput.GetBooleanInput($"Do you own the {chosenLandTransport}(s) used for travel?");
            }

            // Calculate initial vehicle cost based on ownership
            double actualVehicleCost = ownsVehicle ? 0 : baseVehicleCost;


            // 5. Calculate detailed time, cost, and resources for each segment
            double totalJourneyDistanceKm = 0;
            double totalJourneyTimeHours = 0;
            double totalJourneyCostGold = actualVehicleCost; // Start with actual vehicle cost

            double totalRationsAcrossJourney = 0;
            double totalWaterAcrossJourney = 0;

            int overallNumberOfMountsNeeded = 0; // To store the max mounts needed across all land segments

            List<JourneySegment> finalPathSegments = new List<JourneySegment>();

            foreach (var segment in pathSegments)
            {
                double effectiveSpeedKmh = 0;
                double biomeMultiplier = 1.0;
                List<string> biomesInSegment = new List<string>();

                // Determine base speed based on route type and chosen transport
                if (segment.RouteUsed is LandRoute landRouteSegment)
                {
                    effectiveSpeedKmh = _mountSpeedsMph[chosenLandTransport] * MPH_TO_KMH_FACTOR;
                    biomesInSegment = landRouteSegment.Biomes;

                    // Apply biome difficulty multipliers for land routes
                    double segmentBiomeFactor = 1.0;
                    foreach (var biome in landRouteSegment.Biomes)
                    {
                        segmentBiomeFactor *= BiomeModifier.GetMultiplier(biome);
                    }
                    biomeMultiplier = segmentBiomeFactor; // This will be applied to the distance or time
                }
                else if (segment.RouteUsed is SeaRoute sr)
                {
                    effectiveSpeedKmh = _shipSpeedsMph[chosenSeaTransport] * MPH_TO_KMH_FACTOR;
                }

                // If somehow speed is 0 or unchosen for a segment type, use a default walk speed
                if (effectiveSpeedKmh == 0 && segment.RouteUsed is LandRoute)
                {
                    effectiveSpeedKmh = NORMAL_WALK_MPH * MPH_TO_KMH_FACTOR; // Fallback to foot speed for land
                }
                else if (effectiveSpeedKmh == 0 && segment.RouteUsed is SeaRoute)
                {
                    Console.WriteLine($"Warning: No ship chosen for sea segment from {segment.Start.Name} to {segment.End.Name}. Assuming slowest sea speed (Rowboat).");
                    effectiveSpeedKmh = _shipSpeedsMph[ShipType.Rowboat] * MPH_TO_KMH_FACTOR; // Fallback for sea
                }
                else if (effectiveSpeedKmh == 0) // Should not happen with fallbacks, but safety
                {
                    Console.WriteLine($"Warning: Zero effective speed for segment from {segment.Start.Name} to {segment.End.Name}. Setting to a default.");
                    effectiveSpeedKmh = 1.0; // Minimal speed to prevent division by zero
                }


                // Calculate segment time (adjusted by biome multiplier)
                // If biome multiplier > 1, it means the terrain is harder, effectively increasing travel time for the same distance.
                // So, time = (distance * biomeMultiplier) / effectiveSpeedKmh
                double segmentTimeHours = (segment.DistanceKm * biomeMultiplier) / effectiveSpeedKmh;

                // Calculate segment cost (just placeholder for now, actual costs calculated below)
                double segmentCost = 0; // Segment cost will be accumulated

                // Resource Consumption per segment
                double segmentDays = segmentTimeHours / travelHoursPerDay;
                if (segmentDays < 0.001 && segment.DistanceKm > 0) segmentDays = 0.001; // Ensure at least a minimal day if distance > 0 to account for resources

                double rationsForSegment = RATIONS_PER_PERSON_PER_DAY * numTravelers * segmentDays;
                totalRationsAcrossJourney += rationsForSegment;

                double waterPerPersonForSegment = WATER_PER_PERSON_PER_DAY_LAND;
                if (segment.RouteUsed is SeaRoute || biomesInSegment.Any(b => b.ToUpperInvariant().Contains("DESERT")))
                {
                    waterPerPersonForSegment = WATER_PER_PERSON_PER_DAY_SEA_OR_DESERT;
                }
                double waterForSegment = waterPerPersonForSegment * numTravelers * segmentDays;
                totalWaterAcrossJourney += waterForSegment;

                // Mount & Vehicle Costs for land segments (apply ownership logic)
                double segmentMountCost = 0;
                double segmentFeedCost = 0;
                int currentSegmentMountsNeeded = 0;

                if (segment.RouteUsed is LandRoute && chosenLandTransport != MountType.None && chosenLandTransport != MountType.Foot)
                {
                    double mountCapacity = 0;
                    if (_mountDrawingCapacityLbs.TryGetValue(chosenLandTransport, out mountCapacity))
                    {
                        double totalLoad = totalTravelerWeightLbs + vehicleWeight;
                        if (totalLoad > 0 && mountCapacity > 0)
                        {
                            currentSegmentMountsNeeded = (int)Math.Ceiling(totalLoad / mountCapacity);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Drawing capacity not defined for {chosenLandTransport}. Assuming 1 mount per person if mounted.");
                        currentSegmentMountsNeeded = numTravelers > 0 ? numTravelers : 1; // Fallback
                    }

                    // Update overall max mounts needed
                    if (currentSegmentMountsNeeded > overallNumberOfMountsNeeded)
                    {
                        overallNumberOfMountsNeeded = currentSegmentMountsNeeded;
                    }

                    // Apply mount cost ONLY if mounts are NOT owned
                    if (!ownsMounts)
                    {
                        if (_mountCostsPerDayGold.TryGetValue(chosenLandTransport, out double mountDailyCost))
                        {
                            segmentMountCost = currentSegmentMountsNeeded * mountDailyCost * segmentDays;
                        }
                    }

                    segmentFeedCost = currentSegmentMountsNeeded * ANIMAL_FEED_COST_GOLD_PER_DAY_PER_ANIMAL * segmentDays;
                }

                segmentCost = segmentMountCost + segmentFeedCost; // Only include direct travel costs here, resources are separate

                totalJourneyDistanceKm += segment.DistanceKm;
                totalJourneyTimeHours += segmentTimeHours;
                totalJourneyCostGold += segmentCost;

                finalPathSegments.Add(new JourneySegment(segment.Start, segment.End, segment.DistanceKm, segmentTimeHours, segmentCost, segment.RouteUsed, biomesInSegment));
            }

            // Final cost calculation for resources (apply rounding)
            double totalRationCost = Math.Ceiling(totalRationsAcrossJourney) * RATION_COST_GOLD_PER_DAY;
            // Round water consumption cost up to the nearest multiple of 10.
            double roundedWaterLiters = Math.Ceiling(totalWaterAcrossJourney);
            double waterCostRoundedUpToNearestTen = Math.Ceiling(roundedWaterLiters / 10.0) * 10;
            double totalWaterCost = waterCostRoundedUpToNearestTen * WATER_COST_GOLD_PER_LITER;

            totalJourneyCostGold += totalRationCost + totalWaterCost;


            JourneyResult result = new JourneyResult(
                startSettlement, endSettlement, finalPathSegments, new List<ResourceCost>(), // EstimatedResourceCosts not used this way
                numTravelers, chosenLandTransport, chosenSeaTransport, vehicleChoice,
                overallNumberOfMountsNeeded, totalRationsAcrossJourney, totalWaterAcrossJourney, travelHoursPerDay,
                totalTravelerWeightLbs, ownsMounts, ownsVehicle // New: Patch 3 ownership flags
            );

            // Update the totals in the JourneyResult object
            result.UpdateTotals(totalJourneyDistanceKm, totalJourneyTimeHours, totalJourneyCostGold, totalRationsAcrossJourney, totalWaterAcrossJourney);

            return result;
        }

        /// <summary>
        /// Saves the detailed journey result to a local .txt file on the user's desktop.
        /// </summary>
        /// <param name="result">The JourneyResult object to save.</param>
        public void SaveJourneyResultToFile(JourneyResult result)
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string appFolderPath = Path.Combine(desktopPath, "D&D 5e: Saved Routes");

                // Ensure the directory exists
                Directory.CreateDirectory(appFolderPath);

                string fileName = $"{result.Origin.Name} to {result.Destination.Name}.txt";
                string fullFilePath = Path.Combine(appFolderPath, fileName);

                // Get the formatted result string
                string contentToSave = result.GetFormattedResult();

                File.WriteAllText(fullFilePath, contentToSave, Encoding.UTF8);

                Console.WriteLine($"\nJourney details saved successfully to:\n{fullFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError saving journey details: {ex.Message}");
                Console.WriteLine("Please ensure the application has write permissions to your Desktop.");
            }
        }


        // --- DM Functions for Route Management ---

        /// <summary>
        /// Prompts the DM to create a new land route.
        /// </summary>
        /// <param name="routeManager">The RouteManager instance to add the route to.</param>
        public void CreateNewLandRouteDm(RouteManager routeManager)
        {
            Console.WriteLine("\n--- Create New Land Route (DM) ---");
            Console.WriteLine("Enter Origin Settlement Name and Region:");
            string originName = ConsoleInput.GetStringInput("Origin Settlement Name: ");
            string originRegion = ConsoleInput.GetStringInput("Origin Region Name: ");
            Settlement origin = _dataManager.GetSettlementByNameAndRegion(originName, originRegion);

            Console.WriteLine("Enter Destination Settlement Name and Region:");
            string destName = ConsoleInput.GetStringInput("Destination Settlement Name: ");
            string destRegion = ConsoleInput.GetStringInput("Destination Region Name: ");
            Settlement destination = _dataManager.GetSettlementByNameAndRegion(destName, destRegion);

            List<string> biomes = new List<string>();
            List<double> biomeDistances = new List<double>();

            while (true)
            {
                string biome = ConsoleInput.GetStringInput("Enter biome name (e.g., Grasslands, Hot Desert) or type 'done' to finish: ", true);
                if (biome.ToLowerInvariant() == "done") break;
                biomes.Add(biome);
                double distance = ConsoleInput.GetDoubleInput($"Enter distance in km for {biome}: ", 0.1);
                biomeDistances.Add(distance);
            }

            if (!biomes.Any() || biomes.Count != biomeDistances.Count)
            {
                Console.WriteLine("Error: Must enter at least one biome and its corresponding distance.");
                return;
            }

            bool isMapped = ConsoleInput.GetBooleanInput("Is this route mapped (True/False)?");

            LandRoute newRoute = new LandRoute(origin, destination, biomes, biomeDistances, isMapped);

            routeManager.AddLandRoute(newRoute); // Add to in-memory, will trigger save and graph rebuild
            Console.WriteLine("Land Route created and saved.");
        }

        /// <summary>
        /// Prompts the DM to create a new sea route.
        /// </summary>
        /// <param name="routeManager">The RouteManager instance to add the route to.</param>
        public void CreateNewSeaRouteDm(RouteManager routeManager)
        {
            Console.WriteLine("\n--- Create New Sea Route (DM) ---");
            Console.WriteLine("Enter Origin Settlement Name and Region:");
            string originName = ConsoleInput.GetStringInput("Origin Settlement Name: ");
            string originRegion = ConsoleInput.GetStringInput("Origin Region Name: ");
            Settlement origin = _dataManager.GetSettlementByNameAndRegion(originName, originRegion);

            Console.WriteLine("Enter Destination Settlement Name and Region:");
            string destName = ConsoleInput.GetStringInput("Destination Settlement Name: ");
            string destRegion = ConsoleInput.GetStringInput("Destination Region Name: ");
            Settlement destination = _dataManager.GetSettlementByNameAndRegion(destName, destRegion);

            double distance = ConsoleInput.GetDoubleInput("Enter distance in kilometers: ", 0.1);

            SeaRoute newRoute = new SeaRoute(origin, destination, distance);

            routeManager.AddSeaRoute(newRoute); // Add to in-memory, will trigger save and graph rebuild
            Console.WriteLine("Sea Route created and saved.");
        }


        // --- Player Functions for Route Management ---

        /// <summary>
        /// Allows a player to create a custom land route. This will be saved to the custom routes directory.
        /// </summary>
        /// <param name="routeManager">The RouteManager instance to add the route to.</param>
        public void CreateNewCustomLandRoutePlayer(RouteManager routeManager)
        {
            Console.WriteLine("\n--- Create New Custom Land Route (Player) ---");
            Console.WriteLine("Enter Origin Settlement Name and Region:");
            string originName = ConsoleInput.GetStringInput("Origin Settlement Name: ");
            string originRegion = ConsoleInput.GetStringInput("Origin Region Name: ");
            Settlement origin = _dataManager.GetSettlementByNameAndRegion(originName, originRegion);

            Console.WriteLine("Enter Destination Settlement Name and Region:");
            string destName = ConsoleInput.GetStringInput("Destination Settlement Name: ");
            string destRegion = ConsoleInput.GetStringInput("Destination Region Name: ");
            Settlement destination = _dataManager.GetSettlementByNameAndRegion(destName, destRegion);

            if (origin == null || destination == null)
            {
                Console.WriteLine("Invalid origin or destination settlement. Please ensure both exist or can be created.");
                return;
            }

            List<string> biomes = new List<string>();
            List<double> biomeDistances = new List<double>();

            Console.WriteLine("Enter biomes and their distances for this route (e.g., Grasslands, 100km). Type 'done' when finished.");
            while (true)
            {
                string biomeInput = ConsoleInput.GetStringInput("Biome name (or 'done'): ", true);
                if (biomeInput.ToLowerInvariant() == "done") break;

                double distance = ConsoleInput.GetDoubleInput($"Distance in km for {biomeInput}: ", 0.1);

                biomes.Add(biomeInput);
                biomeDistances.Add(distance);
            }

            if (!biomes.Any() || biomes.Count != biomeDistances.Count)
            {
                Console.WriteLine("Error: At least one biome and its distance must be entered for a land route.");
                return;
            }

            bool isMapped = true; // Player custom routes are always considered mapped

            LandRoute newRoute = new LandRoute(origin, destination, biomes, biomeDistances, isMapped);

            // Save the custom route. It will be added to in-memory routes and saved properly by SaveAllRoutes.
            // For now, _dataManager.SaveSingleCustomRoute is used for immediate file writing.
            _dataManager.SaveSingleCustomRoute(newRoute, RouteType.Land, true);
            routeManager.AddLandRoute(newRoute); // Add to RouteManager's in-memory list and trigger full save
            Console.WriteLine("Custom Land Route created and saved. This route will be available in future sessions.");
        }

        /// <summary>
        /// Allows a player to create a custom sea route. This will be saved to the custom routes directory.
        /// </summary>
        /// <param name="routeManager">The RouteManager instance to add the route to.</param>
        public void CreateNewCustomSeaRoutePlayer(RouteManager routeManager)
        {
            Console.WriteLine("\n--- Create New Custom Sea Route (Player) ---\n");
            Console.WriteLine("Enter Origin Settlement Name and Region:");
            string originName = ConsoleInput.GetStringInput("Origin Settlement Name: ");
            string originRegion = ConsoleInput.GetStringInput("Origin Region Name: ");
            Settlement origin = _dataManager.GetSettlementByNameAndRegion(originName, originRegion);

            Console.WriteLine("Enter Destination Settlement Name and Region:");
            string destName = ConsoleInput.GetStringInput("Destination Settlement Name: ");
            string destRegion = ConsoleInput.GetStringInput("Destination Region Name: ");
            Settlement destination = _dataManager.GetSettlementByNameAndRegion(destName, destRegion);

            if (origin == null || destination == null)
            {
                Console.WriteLine("Invalid origin or destination settlement. Please ensure both exist or can be created.");
                return;
            }

            double distance = ConsoleInput.GetDoubleInput("Enter distance in kilometers: ", 0.1);

            SeaRoute newRoute = new SeaRoute(origin, destination, distance);

            // Save the custom route. It will be added to in-memory routes and saved properly by SaveAllRoutes.
            _dataManager.SaveSingleCustomRoute(newRoute, RouteType.Sea, true);
            routeManager.AddSeaRoute(newRoute); // Add to RouteManager's in-memory list and trigger full save
            Console.WriteLine("Custom Sea Route created and saved. This route will be available in future sessions.");
        }
    }
}
