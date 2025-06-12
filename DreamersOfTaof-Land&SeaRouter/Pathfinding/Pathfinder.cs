using System;
using System.Collections.Generic;
using System.Linq;
using YourFantasyWorldProject.Classes; // For Settlement, LandRoute, SeaRoute, Enums, BiomeModifier
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

        public Pathfinder(DataManager dataManager)
        {
            _dataManager = dataManager;
            _graph = new Dictionary<Settlement, List<Edge>>();
        }

        // Represents an edge in our graph
        private class Edge
        {
            public Settlement Destination { get; }
            public double OriginalDistance { get; } // The raw distance from file
            public double CalculatedWeight { get; } // The effective distance/cost used by Dijkstra's
            public RouteType Type { get; }
            public List<string> Biomes { get; } // Specific to LandRoute
            public bool IsMapped { get; } // Specific to LandRoute

            public Edge(Settlement destination, double originalDistance, RouteType type, List<string> biomes = null, bool isMapped = false)
            {
                Destination = destination;
                OriginalDistance = originalDistance;
                Type = type;
                Biomes = biomes ?? new List<string>();
                IsMapped = isMapped;

                // Calculate the weight based on type and modifiers at Edge creation
                CalculatedWeight = CalculateEffectiveWeight(originalDistance, type, Biomes, IsMapped);
            }

            private double CalculateEffectiveWeight(double distance, RouteType type, List<string> biomes, bool isMapped)
            {
                if (type == RouteType.Sea)
                {
                    return distance; // Sea routes typically have no biome modifiers
                }
                else // LandRoute
                {
                    double effectiveDistance = distance;

                    // Apply biome difficulty modifiers
                    foreach (var biome in biomes)
                    {
                        effectiveDistance *= BiomeModifier.GetMultiplier(biome);
                    }

                    // Apply unmapped penalty
                    if (!isMapped)
                    {
                        effectiveDistance *= 2.0; // Double distance for unmapped routes
                    }
                    return effectiveDistance;
                }
            }
        }

        // --- Graph Building Method ---
        public void BuildGraph(IReadOnlyList<LandRoute> landRoutes, IReadOnlyList<SeaRoute> seaRoutes)
        {
            _graph.Clear();

            // Add all unique settlements as nodes
            foreach (var route in landRoutes)
            {
                if (!_graph.ContainsKey(route.Origin)) _graph[route.Origin] = new List<Edge>();
                if (!_graph.ContainsKey(route.Destination)) _graph[route.Destination] = new List<Edge>();
            }
            foreach (var route in seaRoutes)
            {
                if (!_graph.ContainsKey(route.Origin)) _graph[route.Origin] = new List<Edge>();
                if (!_graph.ContainsKey(route.Destination)) _graph[route.Destination] = new List<Edge>();
            }

            // Add land edges
            foreach (var route in landRoutes)
            {
                // We add the edge using the LandRoute's properties
                _graph[route.Origin].Add(new Edge(route.Destination, route.TotalDistance, RouteType.Land, route.Biomes, route.IsMapped));
            }

            // Add sea edges
            foreach (var route in seaRoutes)
            {
                // We add the edge using the SeaRoute's properties
                _graph[route.Origin].Add(new Edge(route.Destination, route.Distance, RouteType.Sea));
            }
            Console.WriteLine($"Graph built with {_graph.Count} settlements and calculated edges (including biome/mapped modifiers).");
        }


        // --- Non-Management Methods ---

        public void FindRoute()
        {
            Console.WriteLine("\n--- Find Route ---");
            string originCountryName = ConsoleInput.GetStringInput("Enter origin country name: ");
            string originSettlementName = ConsoleInput.GetStringInput("Enter origin settlement name: ");
            string destCountryName = ConsoleInput.GetStringInput("Enter destination country name: ");
            string destSettlementName = ConsoleInput.GetStringInput("Enter destination settlement name: ");

            RoutePreference preference = ConsoleInput.GetEnumInput<RoutePreference>("Select route preference:");

            List<string> biomesToAvoid = new List<string>();
            bool avoidBiomes = ConsoleInput.GetBooleanInput("Do you want to avoid any biomes for land routes?");
            if (avoidBiomes)
            {
                Console.WriteLine("Enter biomes to avoid (type 'done' when finished):");
                string biomeName;
                do
                {
                    biomeName = ConsoleInput.GetStringInput("Biome to avoid (or 'done'): ", allowEmpty: true);
                    if (!string.IsNullOrWhiteSpace(biomeName) && biomeName.ToLowerInvariant() != "done")
                    {
                        biomesToAvoid.Add(biomeName.ToUpperInvariant()); // Store in upper invariant for consistent comparison
                    }
                } while (!string.IsNullOrWhiteSpace(biomeName) && biomeName.ToLowerInvariant() != "done");
            }


            Settlement origin = new Settlement(originSettlementName, originCountryName);
            Settlement destination = new Settlement(destSettlementName, destCountryName);

            if (!_graph.ContainsKey(origin))
            {
                Console.WriteLine($"Origin settlement {origin} not found in the graph.");
                return;
            }
            if (!_graph.ContainsKey(destination))
            {
                Console.WriteLine($"Destination settlement {destination} not found in the graph.");
                return;
            }

            // Dijkstra's Algorithm using MinHeap
            Dictionary<Settlement, double> distances = new Dictionary<Settlement, double>();
            Dictionary<Settlement, Settlement> predecessors = new Dictionary<Settlement, Settlement>();
            Dictionary<Settlement, Edge> predecessorEdges = new Dictionary<Settlement, Edge>(); // To reconstruct edge details

            MinHeap<Settlement> priorityQueue = new MinHeap<Settlement>();

            // Initialize distances
            foreach (var node in _graph.Keys)
            {
                distances[node] = double.MaxValue;
            }
            distances[origin] = 0;
            priorityQueue.Enqueue(origin, 0);

            while (priorityQueue.Count > 0)
            {
                Settlement current = priorityQueue.Dequeue();

                // If we've already found a shorter path to 'current', skip

                if (current.Equals(destination))
                {
                    // Path found, reconstruct and print
                    PrintPathAndCalculateTime(origin, destination, distances, predecessors, predecessorEdges);
                    return;
                }

                if (!_graph.ContainsKey(current)) continue; // Should not happen if graph is correctly built

                foreach (var edge in _graph[current])
                {
                    // Filter edges based on preference
                    if (preference == RoutePreference.LandOnly && edge.Type == RouteType.Sea) continue;
                    if (preference == RoutePreference.SeaOnly && edge.Type == RouteType.Land) continue;

                    // Filter land edges based on biomes to avoid
                    if (edge.Type == RouteType.Land && biomesToAvoid.Any(b => edge.Biomes.Contains(b)))
                    {
                        Console.WriteLine($"  (Skipping land route {current.Name} -> {edge.Destination.Name} due to avoided biome.)");
                        continue; // Skip this edge if it contains an avoided biome
                    }

                    double newDist = distances[current] + edge.CalculatedWeight;
                    if (newDist < distances[edge.Destination])
                    {
                        distances[edge.Destination] = newDist;
                        predecessors[edge.Destination] = current;
                        predecessorEdges[edge.Destination] = edge; // Store the actual edge taken
                        priorityQueue.UpdatePriority(edge.Destination, newDist);
                    }
                }
            }

            Console.WriteLine($"No {preference} route found from {origin} to {destination} with the given criteria.");
        }

        private void PrintPathAndCalculateTime(Settlement origin, Settlement destination,
                                               Dictionary<Settlement, double> distances,
                                               Dictionary<Settlement, Settlement> predecessors,
                                               Dictionary<Settlement, Edge> predecessorEdges)
        {
            List<Tuple<Settlement, Edge>> pathSegments = new List<Tuple<Settlement, Edge>>();
            Settlement current = destination;

            while (predecessors.ContainsKey(current))
            {
                Settlement prev = predecessors[current];
                Edge edge = predecessorEdges[current]; // This is the edge that *led to* 'current' from 'prev'
                pathSegments.Add(Tuple.Create(prev, edge));
                current = prev;
            }
            pathSegments.Reverse(); // Reconstruct path from origin to destination

            if (!pathSegments.Any() && !origin.Equals(destination)) // Case where origin == destination
            {
                Console.WriteLine("Error reconstructing path or path is just origin and destination.");
                return;
            }

            Console.WriteLine($"\n--- Shortest Route Found (Total Effective Cost: {distances[destination]:F2} km) ---");
            // Reconstruct the actual settlement path for display
            List<Settlement> actualSettlementPath = new List<Settlement> { origin };
            foreach (var segment in pathSegments)
            {
                actualSettlementPath.Add(segment.Item2.Destination);
            }
            Console.WriteLine("Path: " + string.Join(" -> ", actualSettlementPath.Select(s => s.Name)));


            double totalTravelTimeHours = 0;
            Console.WriteLine("\nTravel Segment Details:");

            foreach (var segmentTuple in pathSegments)
            {
                Settlement segmentOrigin = segmentTuple.Item1;
                Edge connectingEdge = segmentTuple.Item2;
                Settlement segmentDestination = connectingEdge.Destination;

                Console.WriteLine($"\nSegment: {segmentOrigin.Name} -> {segmentDestination.Name} ({connectingEdge.Type} route)");
                Console.WriteLine($"  Original Distance: {connectingEdge.OriginalDistance:F2} km");
                Console.WriteLine($"  Effective Cost: {connectingEdge.CalculatedWeight:F2} km (used for pathfinding)");

                double segmentTime = 0;
                if (connectingEdge.Type == RouteType.Land)
                {
                    Console.WriteLine($"  Biomes: {string.Join(", ", connectingEdge.Biomes)}");
                    Console.WriteLine($"  Mapped: {connectingEdge.IsMapped}");
                    Console.WriteLine("How would you like to travel this land segment?");
                    Console.WriteLine("  1. Walking Speed (km/h)");
                    Console.WriteLine("  2. Mount Type");
                    int travelChoice = (int)ConsoleInput.GetDoubleInput("Enter choice (1 or 2): ", 1);

                    if (travelChoice == 1)
                    {
                        double speedKmH = ConsoleInput.GetDoubleInput("Enter your walking speed (km/h): ");
                        segmentTime = CalculateTravelTime(connectingEdge.OriginalDistance, speedKmH); // Use original distance for time calc
                    }
                    else if (travelChoice == 2)
                    {
                        MountType mountType = ConsoleInput.GetEnumInput<MountType>("Select mount type:");
                        segmentTime = CalculateTravelTime(connectingEdge.OriginalDistance, mountType: mountType); // Use original distance
                    }
                    else
                    {
                        Console.WriteLine("Invalid choice, defaulting to normal walking speed.");
                        segmentTime = CalculateTravelTime(connectingEdge.OriginalDistance, mountType: MountType.None); // Use original distance
                    }
                }
                else // SeaRoute
                {
                    ShipType shipType = ConsoleInput.GetEnumInput<ShipType>("Select ship type:");
                    segmentTime = CalculateTravelTime(connectingEdge.OriginalDistance, shipType: shipType); // Use original distance
                }

                Console.WriteLine($"  Estimated time for this segment: {segmentTime:F2} hours");
                totalTravelTimeHours += segmentTime;
            }

            Console.WriteLine($"\nTotal estimated travel time: {totalTravelTimeHours:F2} hours");
        }


        public void CustomSeaRoute()
        {
            Console.WriteLine("\n--- Custom Sea Route ---");
            string originCountryName = ConsoleInput.GetStringInput("Enter origin country name: ");
            string originSettlementName = ConsoleInput.GetStringInput("Enter origin settlement name: ");
            string destCountryName = ConsoleInput.GetStringInput("Enter destination country name: ");
            string destSettlementName = ConsoleInput.GetStringInput("Enter destination settlement name: ");
            double routeDistance = ConsoleInput.GetDoubleInput("Enter custom route distance (km): ");
            ShipType shipType = ConsoleInput.GetEnumInput<ShipType>("Select ship type:");

            Settlement origin = new Settlement(originSettlementName, originCountryName);
            Settlement destination = new Settlement(destSettlementName, destCountryName);

            // Check if origin is a port (has any sea routes)
            bool isOriginPort = _graph.ContainsKey(origin) && _graph[origin].Any(e => e.Type == RouteType.Sea);
            // Check if destination is a port (has any sea routes to or from it)
            bool isDestinationPort = _graph.ContainsKey(destination) && (_graph[destination].Any(e => e.Type == RouteType.Sea) || _graph.Any(kvp => kvp.Value.Any(e => e.Destination.Equals(destination) && e.Type == RouteType.Sea)));

            if (!isOriginPort)
            {
                Console.WriteLine($"{origin.Name} in {origin.Country} is not recognized as a port settlement. Cannot create custom sea route.");
                return;
            }
            if (!isDestinationPort)
            {
                Console.WriteLine($"{destination.Name} in {destination.Country} is not recognized as a port settlement. Cannot create custom sea route.");
                return;
            }

            double travelTime = CalculateTravelTime(routeDistance, shipType: shipType);
            Console.WriteLine($"\nCustom Sea Route from {origin} to {destination} ({routeDistance}km) by {shipType}:");
            Console.WriteLine($"Estimated travel time: {travelTime:F2} hours.");

            bool saveCustomRoute = ConsoleInput.GetBooleanInput("Do you want to save this custom route?");
            if (saveCustomRoute)
            {
                // For custom routes, we only save the forward route.
                // If the user wants a reverse, they'd explicitly create it.
                // Or you could add logic to also save the reverse here.
                SeaRoute customSeaRoute = new SeaRoute(origin, destination, routeDistance);
                _dataManager.SaveCustomRoute(originCountryName, RouteType.Sea, customSeaRoute);
                Console.WriteLine($"Custom sea route saved to CustomRoutes/Sea/{originCountryName.Replace(" ", "_")}.txt");
            }
            else
            {
                Console.WriteLine("Custom route not saved.");
            }
        }

        // --- D&D 5e Speed Conversion Helper ---
        public double CalculateTravelTime(double distanceKm, double customSpeedKmH = 0, ShipType shipType = ShipType.None, MountType mountType = MountType.None)
        {
            double speedKmH = 0;

            if (customSpeedKmH > 0)
            {
                speedKmH = customSpeedKmH;
            }
            else if (mountType != MountType.None)
            {
                double speedMph;
                switch (mountType)
                {
                    case MountType.RidingHorse: speedMph = 6; break;
                    case MountType.Warhorse: speedMph = 6; break;
                    default: speedMph = NORMAL_WALK_MPH; break; // Default walking speed if mount not recognized
                }
                speedKmH = speedMph * MPH_TO_KMH_FACTOR;
            }
            else if (shipType != ShipType.None)
            {
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
            else // Default to normal walking speed if no specific speed/mount/ship provided
            {
                speedKmH = NORMAL_WALK_MPH * MPH_TO_KMH_FACTOR;
            }

            if (speedKmH <= 0)
            {
                Console.WriteLine("Warning: Speed is zero or negative. Cannot calculate travel time.");
                return double.MaxValue; // Or some other indicator of infinite time
            }

            return distanceKm / speedKmH;
        }
    }
}