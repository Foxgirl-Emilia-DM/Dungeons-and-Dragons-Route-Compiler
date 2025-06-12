// Pathfinder.cs
using System;
using System.Collections.Generic;
using System.Linq;
using YourFantasyWorldProject.Classes; // For Settlement, LandRoute, SeaRoute, Enums, BiomeModifier, new classes
using YourFantasyWorldProject.Managers;
using YourFantasyWorldProject.Utils; // For ConsoleInput

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
            public Settlement Origin { get; } // Added Origin back
            public Settlement Destination { get; }
            public IRoute RouteReference { get; } // Reference to the actual route object
            public double CalculatedWeight { get; } // The effective distance/cost used by Dijkstra's (e.g., time)

            // Properties derived from RouteReference for convenience and type safety in Edge
            public RouteType Type => RouteReference switch
            {
                LandRoute _ => RouteType.Land,
                SeaRoute _ => RouteType.Sea,
                _ => throw new InvalidOperationException("Unknown route type.")
            };
            public double OriginalDistance => RouteReference switch
            {
                LandRoute lr => lr.TotalDistance,
                SeaRoute sr => sr.Distance,
                _ => 0
            };
            public List<string> Biomes => (RouteReference as LandRoute)?.Biomes ?? new List<string>();
            public bool IsMapped => (RouteReference as LandRoute)?.IsMapped ?? false;


            public Edge(Settlement origin, Settlement destination, IRoute routeReference, double calculatedWeight)
            {
                Origin = origin;
                Destination = destination;
                RouteReference = routeReference;
                CalculatedWeight = calculatedWeight;
            }

            public override string ToString()
            {
                return $"-> {Destination.Name} ({Destination.Region}) [{Type}] (Dist: {OriginalDistance:F2}km, Weight: {CalculatedWeight:F2})";
            }
        }

        // Build the graph from loaded routes
        public void BuildGraph(IReadOnlyList<LandRoute> landRoutes, IReadOnlyList<SeaRoute> seaRoutes)
        {
            _graph.Clear(); // Clear existing graph

            // Add all unique settlements to the graph
            foreach (var route in landRoutes.Concat<IRoute>(seaRoutes))
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
            foreach (var lr in landRoutes)
            {
                // Calculate base weight (e.g., distance, time, cost) for graph edge
                // For Dijkstra, the weight typically represents what you want to minimize (e.g., time, cost).
                // For simplicity, we'll use TotalDistance as the initial calculated weight for graph building.
                // The actual travel time/cost will be calculated dynamically in FindShortestPath.
                double calculatedWeight = lr.TotalDistance; // This is a placeholder; actual weight is time/cost.

                // Add edge from origin to destination
                _graph[lr.Origin].Add(new Edge(lr.Origin, lr.Destination, lr, calculatedWeight));
                // Add reverse edge for undirected graph
                _graph[lr.Destination].Add(new Edge(lr.Destination, lr.Origin, lr, calculatedWeight)); // For bidirectional
            }

            // Add sea routes
            foreach (var sr in seaRoutes)
            {
                double calculatedWeight = sr.Distance; // Placeholder, actual weight is time/cost.

                // Add edge from origin to destination
                _graph[sr.Origin].Add(new Edge(sr.Origin, sr.Destination, sr, calculatedWeight));
                // Add reverse edge for undirected graph
                _graph[sr.Destination].Add(new Edge(sr.Destination, sr.Origin, sr, calculatedWeight)); // For bidirectional
            }

            Console.WriteLine($"Graph built with {_graph.Keys.Count} settlements and {_graph.Values.Sum(list => list.Count) / 2} routes.");
        }

        public void FindRoute()
        {
            Console.WriteLine("\n--- Find Route ---");
            string originName = ConsoleInput.GetStringInput("Enter Origin Settlement Name: ");
            string originRegion = ConsoleInput.GetStringInput("Enter Origin Settlement Region: ");
            string destinationName = ConsoleInput.GetStringInput("Enter Destination Settlement Name: ");
            string destinationRegion = ConsoleInput.GetStringInput("Enter Destination Settlement Region: ");

            Settlement origin = _dataManager.GetSettlementByNameAndRegion(originName, originRegion);
            Settlement destination = _dataManager.GetSettlementByNameAndRegion(destinationName, destinationRegion);

            if (origin == null)
            {
                Console.WriteLine($"Origin settlement '{originName}' in region '{originRegion}' not found. Please ensure it exists in your data files.");
                return;
            }
            if (destination == null)
            {
                Console.WriteLine($"Destination settlement '{destinationName}' in region '{destinationRegion}' not found. Please ensure it exists in your data files.");
                return;
            }

            if (!_graph.ContainsKey(origin))
            {
                Console.WriteLine($"Origin settlement '{origin.Name} ({origin.Region})' is not part of any defined route.");
                return;
            }
            if (!_graph.ContainsKey(destination))
            {
                Console.WriteLine($"Destination settlement '{destination.Name} ({destination.Region})' is not part of any defined route.");
                return;
            }

            RoutePreference preference = ConsoleInput.GetEnumInput<RoutePreference>("Select route preference:");

            int numTravelers = ConsoleInput.GetIntInput("Enter number of travelers: ", 1);
            MountType mountType = ConsoleInput.GetEnumInput<MountType>("Select mount type (None for walking):");
            ShipType shipType = ConsoleInput.GetEnumInput<ShipType>("Select ship type (None for no ship):");

            if (mountType != MountType.None && shipType != ShipType.None)
            {
                Console.WriteLine("Warning: You cannot use both a mount and a ship. Prioritizing ship type for sea travel.");
            }


            // Dijkstra's Algorithm implementation
            var distances = new Dictionary<Settlement, double>();
            var previousEdges = new Dictionary<Settlement, Edge>(); // Store the *edge* that led to this settlement
            var pq = new MinHeap<Settlement>();

            foreach (var settlement in _graph.Keys)
            {
                distances[settlement] = double.MaxValue;
                previousEdges[settlement] = null;
            }
            distances[origin] = 0;
            pq.Enqueue(origin, 0);

            while (pq.Count > 0)
            {
                Settlement current = pq.Dequeue();

                if (current.Equals(destination))
                {
                    break; // Path found
                }

                if (distances[current] == double.MaxValue)
                {
                    continue; // Unreachable node
                }

                foreach (var edge in _graph[current])
                {
                    // Apply route preference filtering
                    if ((preference == RoutePreference.LandOnly && edge.Type == RouteType.Sea) ||
                        (preference == RoutePreference.SeaOnly && edge.Type == RouteType.Land))
                    {
                        continue;
                    }

                    // Calculate actual travel time for this segment
                    double segmentTimeHours = CalculateTravelTime(edge.OriginalDistance, edge.Type, mountType, shipType, edge.Biomes);

                    // If segmentTimeHours is MaxValue, it means this route isn't viable with current transport
                    if (segmentTimeHours == double.MaxValue) continue;

                    double newDistance = distances[current] + segmentTimeHours; // Using time as weight for shortest time path

                    if (newDistance < distances[edge.Destination])
                    {
                        distances[edge.Destination] = newDistance;
                        previousEdges[edge.Destination] = edge; // Store the actual edge
                        pq.Enqueue(edge.Destination, newDistance);
                    }
                }
            }

            // Reconstruct path
            List<JourneySegment> finalPathSegments = new List<JourneySegment>();
            if (distances[destination] != double.MaxValue)
            {
                Settlement current = destination;
                while (!current.Equals(origin)) // Loop until we reach the origin
                {
                    if (!previousEdges.TryGetValue(current, out Edge incomingEdge) || incomingEdge == null)
                    {
                        Console.WriteLine("Error: Could not reconstruct path segments. Path broken or origin not reached.");
                        finalPathSegments.Clear();
                        break;
                    }

                    // The segment starts from incomingEdge.Origin and goes to incomingEdge.Destination
                    // The time and cost were already calculated when Dijkstra's found the shortest path to current
                    double segmentTimeHours = CalculateTravelTime(incomingEdge.OriginalDistance, incomingEdge.Type, mountType, shipType, incomingEdge.Biomes);
                    double segmentCost = CalculateCost(incomingEdge.OriginalDistance, incomingEdge.Type, incomingEdge.IsMapped);

                    // Use the actual IRoute from the edge
                    finalPathSegments.Insert(0, new JourneySegment(
                        incomingEdge.Origin,
                        incomingEdge.Destination,
                        incomingEdge.OriginalDistance,
                        segmentTimeHours,
                        segmentCost,
                        incomingEdge.RouteReference, // Directly use the stored RouteReference
                        incomingEdge.Biomes
                    ));
                    current = incomingEdge.Origin; // Move to the origin of the incoming edge
                }
            }

            // Calculate estimated resource costs for the entire journey
            List<ResourceCost> totalResourceCosts = CalculateResourceConsumption(finalPathSegments, numTravelers);

            JourneyResult result = new JourneyResult(origin, destination, finalPathSegments, totalResourceCosts);
            result.DisplayResult();
        }

        private double CalculateTravelTime(double distanceKm, RouteType routeType, MountType mountType, ShipType shipType, List<string> biomes = null)
        {
            double speedKmH;

            if (routeType == RouteType.Sea)
            {
                if (shipType == ShipType.None)
                {
                    Console.WriteLine("Cannot traverse sea route without a ship.");
                    return double.MaxValue; // Indicate impossible route
                }

                double speedMph;
                switch (shipType)
                {
                    case ShipType.Rowboat: speedMph = 1.5; break;
                    case ShipType.Keelboat: speedMph = 2; break;
                    case ShipType.Longship: speedMph = 3; break;
                    case ShipType.Galley: speedMph = 4; break;
                    case ShipType.SailingShip: speedMph = 2; break;
                    default: speedMph = 1.5; break; // Default lowest ship speed
                }
                speedKmH = speedMph * MPH_TO_KMH_FACTOR;
            }
            else // RouteType.Land
            {
                double speedMph;
                if (mountType != MountType.None)
                {
                    switch (mountType)
                    {
                        case MountType.RidingHorse: speedMph = 6; break;
                        case MountType.Warhorse: speedMph = 6; break;
                        default: speedMph = NORMAL_WALK_MPH; break; // Default walking speed if mount not recognized
                    }
                }
                else // Walking
                {
                    speedMph = NORMAL_WALK_MPH; // Base walking speed
                }
                speedKmH = speedMph * MPH_TO_KMH_FACTOR;

                // Apply biome modifiers for land routes
                if (biomes != null && biomes.Any())
                {
                    double totalBiomeMultiplier = 1.0;
                    foreach (var biome in biomes)
                    {
                        totalBiomeMultiplier *= BiomeModifier.GetMultiplier(biome);
                    }
                    // Apply the average biome difficulty to speed
                    speedKmH /= totalBiomeMultiplier;
                }
            }

            if (speedKmH <= 0)
            {
                Console.WriteLine("Warning: Speed is zero or negative. Cannot calculate travel time.");
                return double.MaxValue; // Or some other indicator of infinite time
            }

            return distanceKm / speedKmH;
        }

        private double CalculateCost(double distanceKm, RouteType routeType, bool isMapped = false)
        {
            const double GOLD_PER_KM_LAND_UNMAPPED = 0.1;
            const double GOLD_PER_KM_LAND_MAPPED = 0.05;
            const double GOLD_PER_KM_SEA = 0.2; // Sea travel often involves port fees, supplies etc.

            if (routeType == RouteType.Land)
            {
                return distanceKm * (isMapped ? GOLD_PER_KM_LAND_MAPPED : GOLD_PER_KM_LAND_UNMAPPED);
            }
            else // SeaRoute
            {
                return distanceKm * GOLD_PER_KM_SEA;
            }
        }

        private List<ResourceCost> CalculateResourceConsumption(List<JourneySegment> pathSegments, int numTravelers)
        {
            List<ResourceCost> resourceCosts = new List<ResourceCost>();

            double totalJourneyTimeHours = pathSegments.Sum(s => s.TimeHours);
            double totalJourneyTimeDays = totalJourneyTimeHours / 24.0;

            double totalRationsNeeded = totalJourneyTimeDays * RATIONS_PER_PERSON_PER_DAY * numTravelers;

            // Water consumption depends on route type
            double landTravelTimeHours = pathSegments.Where(s => s.RouteUsed is LandRoute).Sum(s => s.TimeHours);
            double seaTravelTimeHours = pathSegments.Where(s => s.RouteUsed is SeaRoute).Sum(s => s.TimeHours);

            double landTravelTimeDays = landTravelTimeHours / 24.0;
            double seaTravelTimeDays = seaTravelTimeHours / 24.0;

            double totalWaterNeededLand = landTravelTimeDays * WATER_PER_PERSON_PER_DAY_LAND * numTravelers;
            double totalWaterNeededSea = seaTravelTimeDays * WATER_PER_PERSON_PER_DAY_SEA * numTravelers;
            double totalWaterNeeded = totalWaterNeededLand + totalWaterNeededSea;


            if (totalRationsNeeded > 0)
            {
                resourceCosts.Add(new ResourceCost("Rations", Math.Ceiling(totalRationsNeeded))); // Round up to nearest whole ration
            }
            if (totalWaterNeeded > 0)
            {
                resourceCosts.Add(new ResourceCost("Water (Liters)", Math.Ceiling(totalWaterNeeded))); // Round up to nearest whole liter
            }

            return resourceCosts;
        }


        public void CustomSeaRoute()
        {
            Console.WriteLine("\n--- Add Custom Sea Route ---");
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
                Console.WriteLine("Invalid origin or destination settlement. Please ensure both exist.");
                return;
            }

            double distance = ConsoleInput.GetDoubleInput("Enter distance in kilometers: ", 0.1);

            SeaRoute customRoute = new SeaRoute(origin, destination, distance);

            // Save the custom route. Logic for custom routes might need to go into DataManager/RouteManager.
            // For now, let's assume DataManager has a method to save a single custom route.
            _dataManager.SaveCustomRoute(customRoute, RouteType.Sea, origin.Region);
            Console.WriteLine("Custom Sea Route created and saved. Remember to rebuild the graph for it to be included in pathfinding.");
            // Note: The graph is currently rebuilt on main menu actions (Add/Remove/Edit), not immediately here.
            // Consider rebuilding the graph directly after CustomSeaRoute if desired.
        }
    }
}
