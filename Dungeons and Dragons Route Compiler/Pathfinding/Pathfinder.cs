// Pathfinder.cs
using System;
using System.Collections.Generic;
using System.Linq;
using YourFantasyWorldProject.Classes; // For Settlement, LandRoute, SeaRoute, Enums, BiomeModifier, new classes
using YourFantasyWorldProject.Managers;
using YourFantasyWorldProject.Utils; // For ConsoleInput
using System.Globalization; // For TextInfo

namespace YourFantasyWorldProject.Pathfinding
{
    public class Pathfinder
    {
        private readonly DataManager _dataManager;
        private Dictionary<Settlement, List<Edge>> _graph;

        // Constants for D&D 5e speed conversions
        private const double MPH_TO_KMH_FACTOR = 1.60934;
        private const double NORMAL_WALK_MPH = 3;
        private const double FAST_WALK_MPH = 4;
        private const double SLOW_WALK_MPH = 2;

        // Resource consumption rates (per person per day)
        private const double RATIONS_PER_PERSON_PER_DAY = 1;
        private const double WATER_PER_PERSON_PER_DAY_LAND = 1; // Liters
        private const double WATER_PER_PERSON_PER_DAY_SEA = 2; // Liters (less access to fresh water)


        public Pathfinder(DataManager dataManager)
        {
            _dataManager = dataManager;
            _graph = new Dictionary<Settlement, List<Edge>>();
        }

        // Represents an edge in our graph
        private class Edge
        {
            public Settlement Destination { get; }
            public double Weight { get; } // Represents time or cost, depending on the algorithm's goal
            public IRoute Route { get; } // Reference to the actual route object

            public Edge(Settlement destination, double weight, IRoute route)
            {
                Destination = destination;
                Weight = weight;
                Route = route;
            }
        }

        /// <summary>
        /// Builds the graph from the provided land and sea routes.
        /// This method should be called whenever route data changes.
        /// </summary>
        public void BuildGraph(List<LandRoute> landRoutes, List<SeaRoute> seaRoutes)
        {
            _graph.Clear(); // Clear existing graph before rebuilding

            // Add all settlements to the graph first to ensure all nodes exist
            // This is important for settlements that might only be destinations but not origins
            HashSet<Settlement> allSettlements = new HashSet<Settlement>();
            foreach (var route in landRoutes)
            {
                allSettlements.Add(route.Origin);
                allSettlements.Add(route.Destination);
            }
            foreach (var route in seaRoutes)
            {
                allSettlements.Add(route.Origin);
                allSettlements.Add(route.Destination);
            }

            foreach (var settlement in allSettlements)
            {
                if (!_graph.ContainsKey(settlement))
                {
                    _graph[settlement] = new List<Edge>();
                }
            }


            // Add land routes to the graph
            foreach (var route in landRoutes)
            {
                // Ensure origin and destination exist in the graph
                if (!_graph.ContainsKey(route.Origin)) _graph[route.Origin] = new List<Edge>();
                if (!_graph.ContainsKey(route.Destination)) _graph[route.Destination] = new List<Edge>();

                // Calculate base time for the land route (distance / speed)
                // We'll use total distance and an average speed for simplicity here.
                // More complex calculations involving biomes will be done in the pathfinding algorithm.
                double baseTimeHours = route.TotalDistance / (NORMAL_WALK_MPH * MPH_TO_KMH_FACTOR); // Example: normal walk speed

                _graph[route.Origin].Add(new Edge(route.Destination, baseTimeHours, route));
            }

            // Add sea routes to the graph
            foreach (var route in seaRoutes)
            {
                // Ensure origin and destination exist in the graph
                if (!_graph.ContainsKey(route.Origin)) _graph[route.Origin] = new List<Edge>();
                if (!_graph.ContainsKey(route.Destination)) _graph[route.Destination] = new List<Edge>();

                // Calculate base time for the sea route (distance / speed)
                double baseTimeHours = route.Distance / (ShipType.SailingShip.ToString().Equals("SailingShip") ? 2 * MPH_TO_KMH_FACTOR : 1.5 * MPH_TO_KMH_FACTOR); // Example: SailingShip speed

                _graph[route.Origin].Add(new Edge(route.Destination, baseTimeHours, route));
            }

            Console.WriteLine($"Graph built with {_graph.Count} settlements and {_graph.Sum(kv => kv.Value.Count)} routes.");
        }


        /// <summary>
        /// Finds the shortest path between two settlements using Dijkstra's algorithm.
        /// Considers different travel speeds and biome modifiers.
        /// </summary>
        /// <param name="origin">The starting settlement.</param>
        /// <param name="destination">The destination settlement.</param>
        /// <param name="numTravelers">Number of travelers in the party.</param>
        /// <param name="travelSpeed">The desired travel speed (Normal, Fast, Slow).</param>
        /// <param name="shipType">The type of ship to use for sea travel.</param>
        /// <param name="mountType">The type of mount to use for land travel.</param>
        /// <param name="preference">User preference for route types (LandOnly, SeaOnly, Mixed).</param>
        /// <returns>A JourneyResult object containing path details, or an empty result if no path is found.</returns>
        public JourneyResult FindPath(
            Settlement origin, Settlement destination,
            int numTravelers,
            string travelSpeed, ShipType shipType, MountType mountType,
            RoutePreference preference)
        {
            if (!_graph.ContainsKey(origin) || !_graph.ContainsKey(destination))
            {
                Console.WriteLine("Origin or destination settlement not found in the graph.");
                return new JourneyResult(origin, destination, null, null);
            }

            // Dijkstra's algorithm implementation
            var distances = new Dictionary<Settlement, double>();
            var previous = new Dictionary<Settlement, IRoute>(); // Stores the route taken to reach this settlement
            var priorityQueue = new MinHeap<Settlement>();

            foreach (var settlement in _graph.Keys)
            {
                distances[settlement] = double.MaxValue;
                previous[settlement] = null;
            }

            distances[origin] = 0;
            priorityQueue.Enqueue(origin, 0);

            while (priorityQueue.Count > 0)
            {
                Settlement current = priorityQueue.Dequeue();

                // If we reached the destination, we can stop
                if (current.Equals(destination))
                {
                    break;
                }

                // If current has not been added to graph, continue
                if (!_graph.ContainsKey(current))
                {
                    continue;
                }

                foreach (var edge in _graph[current])
                {
                    // Filter routes based on preference
                    if (preference == RoutePreference.LandOnly && edge.Route is SeaRoute) continue;
                    if (preference == RoutePreference.SeaOnly && edge.Route is LandRoute) continue;

                    double timeCost = 0;
                    double routeDistance = 0;
                    List<string> biomesTraversed = new List<string>();

                    if (edge.Route is LandRoute landRoute)
                    {
                        routeDistance = landRoute.TotalDistance;
                        biomesTraversed = landRoute.Biomes; // Get biomes for this segment
                        timeCost = CalculateLandRouteTimeAndCost(landRoute, travelSpeed, mountType, numTravelers, out _); // Cost not used here, only time
                    }
                    else if (edge.Route is SeaRoute seaRoute)
                    {
                        routeDistance = seaRoute.Distance;
                        timeCost = CalculateSeaRouteTimeAndCost(seaRoute, shipType, numTravelers, out _); // Cost not used here, only time
                    }

                    double newDist = distances[current] + timeCost;

                    if (newDist < distances[edge.Destination])
                    {
                        distances[edge.Destination] = newDist;
                        previous[edge.Destination] = edge.Route; // Store the route itself
                        priorityQueue.Enqueue(edge.Destination, newDist);
                    }
                }
            }

            // Reconstruct path
            List<JourneySegment> pathSegments = new List<JourneySegment>();
            List<ResourceCost> estimatedResourceCosts = new List<ResourceCost>();
            double totalRations = 0;
            double totalWaterLand = 0;
            double totalWaterSea = 0;

            if (previous[destination] == null && !origin.Equals(destination)) // No path found
            {
                return new JourneyResult(origin, destination, null, null);
            }

            Settlement pathCurrent = destination;
            while (pathCurrent != null && !pathCurrent.Equals(origin))
            {
                IRoute route = previous[pathCurrent];
                if (route == null) break; // Should not happen if path found, but as a safeguard

                Settlement segmentStart = route.Origin;
                Settlement segmentEnd = route.Destination;

                double segmentTimeHours;
                double segmentCost;
                double segmentDistance;
                List<string> biomes = new List<string>();


                if (route is LandRoute landRouteSegment)
                {
                    segmentTimeHours = CalculateLandRouteTimeAndCost(landRouteSegment, travelSpeed, mountType, numTravelers, out segmentCost);
                    segmentDistance = landRouteSegment.TotalDistance;
                    biomes = landRouteSegment.Biomes;
                    totalRations += (segmentTimeHours / 24) * RATIONS_PER_PERSON_PER_DAY * numTravelers;
                    totalWaterLand += (segmentTimeHours / 24) * WATER_PER_PERSON_PER_DAY_LAND * numTravelers;
                }
                else if (route is SeaRoute seaRouteSegment)
                {
                    segmentTimeHours = CalculateSeaRouteTimeAndCost(seaRouteSegment, shipType, numTravelers, out segmentCost);
                    segmentDistance = seaRouteSegment.Distance;
                    totalRations += (segmentTimeHours / 24) * RATIONS_PER_PERSON_PER_DAY * numTravelers;
                    totalWaterSea += (segmentTimeHours / 24) * WATER_PER_PERSON_PER_DAY_SEA * numTravelers;
                }
                else
                {
                    segmentTimeHours = 0;
                    segmentCost = 0;
                    segmentDistance = 0;
                }

                pathSegments.Insert(0, new JourneySegment(segmentStart, segmentEnd, segmentDistance, segmentTimeHours, segmentCost, route, biomes));
                pathCurrent = segmentStart; // Move to the origin of the current segment
            }

            if (totalRations > 0) estimatedResourceCosts.Add(new ResourceCost("Rations", totalRations));
            if (totalWaterLand > 0) estimatedResourceCosts.Add(new ResourceCost("Water (Land)", totalWaterLand));
            if (totalWaterSea > 0) estimatedResourceCosts.Add(new ResourceCost("Water (Sea)", totalWaterSea));


            return new JourneyResult(origin, destination, pathSegments, estimatedResourceCosts);
        }

        /// <summary>
        /// Calculates the time and cost for a land route segment, considering biomes, speed, and mounts.
        /// </summary>
        private double CalculateLandRouteTimeAndCost(LandRoute route, string travelSpeed, MountType mountType, int numTravelers, out double totalCost)
        {
            double totalTimeHours = 0;
            totalCost = 0;
            double baseSpeedMph;

            switch (travelSpeed.ToLowerInvariant())
            {
                case "fast":
                    baseSpeedMph = FAST_WALK_MPH;
                    break;
                case "slow":
                    baseSpeedMph = SLOW_WALK_MPH;
                    break;
                case "normal":
                default:
                    baseSpeedMph = NORMAL_WALK_MPH;
                    break;
            }

            // Apply mount speed if applicable
            double mountSpeedMph = 0;
            switch (mountType)
            {
                case MountType.RidingHorse:
                case MountType.Warhorse:
                    mountSpeedMph = 6; // Example: 6 mph for horses
                    break;
                case MountType.None:
                default:
                    mountSpeedMph = 0;
                    break;
            }

            // Use the faster of walking or mounted speed, if a mount is present
            double effectiveSpeedMph = (mountType != MountType.None) ? Math.Max(baseSpeedMph, mountSpeedMph) : baseSpeedMph;
            double effectiveSpeedKmh = effectiveSpeedMph * MPH_TO_KMH_FACTOR;

            if (effectiveSpeedKmh <= 0) return double.MaxValue; // Avoid division by zero or negative speed

            for (int i = 0; i < route.Biomes.Count; i++)
            {
                string biome = route.Biomes[i];
                double distance = route.BiomeDistances[i];
                double difficultyMultiplier = BiomeModifier.GetMultiplier(biome);

                // Adjusted distance due to biome difficulty
                double effectiveDistance = distance * difficultyMultiplier;

                // Time for this biome segment
                totalTimeHours += effectiveDistance / effectiveSpeedKmh;

                // Cost for this biome segment (example: 0.1 gold per km per person, adjusted by difficulty)
                // This is a placeholder; actual costs might depend on specific items, tolls, etc.
                totalCost += (effectiveDistance * 0.1 * numTravelers);
            }

            return totalTimeHours;
        }

        /// <summary>
        /// Calculates the time and cost for a sea route segment.
        /// </summary>
        private double CalculateSeaRouteTimeAndCost(SeaRoute route, ShipType shipType, int numTravelers, out double totalCost)
        {
            double shipSpeedMph;
            switch (shipType)
            {
                case ShipType.Rowboat:
                    shipSpeedMph = 1.5;
                    break;
                case ShipType.Keelboat:
                    shipSpeedMph = 2;
                    break;
                case ShipType.Longship:
                    shipSpeedMph = 3;
                    break;
                case ShipType.Galley:
                    shipSpeedMph = 4;
                    break;
                case ShipType.SailingShip:
                    shipSpeedMph = 2;
                    break;
                case ShipType.None:
                default:
                    shipSpeedMph = 0; // Should not happen if a ship is selected
                    break;
            }

            if (shipSpeedMph <= 0)
            {
                totalCost = double.MaxValue; // Cannot travel by sea without a ship/speed
                return double.MaxValue;
            }

            double shipSpeedKmh = shipSpeedMph * MPH_TO_KMH_FACTOR;
            double timeHours = route.Distance / shipSpeedKmh;

            // Example cost for sea travel: 0.05 gold per km per person
            totalCost = route.Distance * 0.05 * numTravelers;

            return timeHours;
        }


        /// <summary>
        /// Prompts the user to create a new land route and saves it,
        /// allowing choice between default and custom folders.
        /// </summary>
        public void CreateLandRoute() // Renamed from CreateCustomLandRoute
        {
            Console.WriteLine("\n--- Create New Land Route ---");
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

            Console.WriteLine("Enter biomes traversed (comma-separated, e.g., Grasslands,Forest):");
            List<string> biomes = ConsoleInput.GetStringInput("Biomes: ").Split(',')
                                              .Select(s => s.Trim()) // Do not force ToUpperInvariant here, store as read
                                              .Where(s => !string.IsNullOrWhiteSpace(s))
                                              .ToList();

            Console.WriteLine("Enter distance for each biome in kilometers (comma-separated, e.g., 10km,5km):");
            List<double> biomeDistances = ConsoleInput.GetStringInput("Biome Distances: ").Split(',')
                                                    .Select(s => double.TryParse(s.Replace("km", "").Trim(), out double val) && val > 0 ? val : 0)
                                                    .Where(d => d > 0) // Filter out invalid/zero distances
                                                    .ToList();

            bool isMapped = ConsoleInput.GetBooleanInput("Is this route fully mapped (yes/no)?");

            if (biomes.Count == 0 || biomeDistances.Count == 0 || biomes.Count != biomeDistances.Count)
            {
                Console.WriteLine("Invalid biome or distance input. Number of biomes must match number of distances, and both must be provided.");
                return;
            }

            bool saveAsCustom = ConsoleInput.GetBooleanInput("Save as a CUSTOM route (yes/no)? (No will save to default routes)");

            LandRoute newRoute = new LandRoute(origin, destination, biomes, biomeDistances, isMapped);

            _dataManager.SaveRoute(newRoute, RouteType.Land, saveAsCustom);
            Console.WriteLine("Land Route created and saved. Remember to rebuild the graph for it to be included in pathfinding.");
        }


        /// <summary>
        /// Prompts the user to create a new sea route and saves it,
        /// allowing choice between default and custom folders.
        /// </summary>
        public void CreateSeaRoute() // Renamed from CreateCustomSeaRoute
        {
            Console.WriteLine("\n--- Create New Sea Route ---");
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

            bool saveAsCustom = ConsoleInput.GetBooleanInput("Save as a CUSTOM route (yes/no)? (No will save to default routes)");

            SeaRoute newRoute = new SeaRoute(origin, destination, distance);

            _dataManager.SaveRoute(newRoute, RouteType.Sea, saveAsCustom);
            Console.WriteLine("Sea Route created and saved. Remember to rebuild the graph for it to be included in pathfinding.");
        }
    }
}
