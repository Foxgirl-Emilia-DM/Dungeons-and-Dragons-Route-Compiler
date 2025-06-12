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
                // Ensure origin and destination settlements are in the graph dictionary
                if (!_graph.ContainsKey(route.Origin)) _graph[route.Origin] = new List<Edge>();
                if (!_graph.ContainsKey(route.Destination)) _graph[route.Destination] = new List<Edge>();

                double baseTimeHours = route.TotalDistance / (NORMAL_WALK_MPH * MPH_TO_KMH_FACTOR);

                // Add forward route A -> B
                _graph[route.Origin].Add(new Edge(route.Destination, baseTimeHours, route));
                // Add reverse route B -> A (assuming land routes are bidirectional)
                _graph[route.Destination].Add(new Edge(route.Origin, baseTimeHours, route));
            }

            // Add sea routes to the graph
            foreach (var route in seaRoutes)
            {
                // Ensure origin and destination settlements are in the graph dictionary
                if (!_graph.ContainsKey(route.Origin)) _graph[route.Origin] = new List<Edge>();
                if (!_graph.ContainsKey(route.Destination)) _graph[route.Destination] = new List<Edge>();

                // Calculate base time for the sea route. Using a default ship speed for graph building.
                // The actual calculation for pathfinding will use the selected shipType.
                // For graph building, we can use a representative speed, e.g., SailingShip's speed.
                double representativeShipSpeedMph = 2; // SailingShip speed
                double baseTimeHours = route.Distance / (representativeShipSpeedMph * MPH_TO_KMH_FACTOR);

                // Add forward route X -> Y
                _graph[route.Origin].Add(new Edge(route.Destination, baseTimeHours, route));
                // Add reverse route Y -> X (assuming sea routes are also bidirectional)
                _graph[route.Destination].Add(new Edge(route.Origin, baseTimeHours, route));
            }

            Console.WriteLine($"Graph built with {_graph.Count} settlements and {_graph.Sum(kv => kv.Value.Count)} routes.");
        }


        /// <summary>
        /// Finds the shortest path between two settlements using Dijkstra's algorithm.
        /// Considers different travel speeds and biome modifiers.
        /// </summary>
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

            var distances = new Dictionary<Settlement, double>();
            var previous = new Dictionary<Settlement, IRoute>();
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

                if (current.Equals(destination))
                {
                    break;
                }

                if (!_graph.ContainsKey(current))
                {
                    continue;
                }

                foreach (var edge in _graph[current])
                {
                    // Ensure the route type matches the preference
                    if (preference == RoutePreference.LandOnly && edge.Route is SeaRoute) continue;
                    if (preference == RoutePreference.SeaOnly && edge.Route is LandRoute) continue;

                    double timeCost = 0;
                    // The 'edge.Route' here is the actual route object (LandRoute or SeaRoute)
                    // We need to ensure the origin and destination of the 'edge.Route' match
                    // the current segment being evaluated to correctly calculate time and cost.
                    // This is crucial because a 'route' object might be added as a reverse edge.
                    // When traversing from current to edge.Destination, we use the route whose Origin is 'current'
                    // and Destination is 'edge.Destination'.
                    // If the stored 'route' in the edge is the *reverse* route, we need to make sure the
                    // calculations are based on the correct direction.
                    // For simplicity, we can assume that edge.Route.Origin is always 'current' and edge.Route.Destination is 'edge.Destination'
                    // for the purpose of time/cost calculation in Dijkstra's, as the graph has symmetric edges.
                    // The critical part is that the JourneySegment correctly reflects the 'segmentStart' and 'segmentEnd'.

                    // Determine the actual segment start and end for the purpose of cost calculation
                    // This is important because 'edge.Route' might be the *reversed* route.
                    Settlement actualSegmentOrigin;
                    Settlement actualSegmentDestination;
                    IRoute actualRouteToUse;

                    // If the edge's route origin matches the current node, use it directly.
                    if (edge.Route.Origin.Equals(current) && edge.Route.Destination.Equals(edge.Destination))
                    {
                        actualRouteToUse = edge.Route;
                        actualSegmentOrigin = current;
                        actualSegmentDestination = edge.Destination;
                    }
                    // Otherwise, it's the reverse of the stored route. Create a temporary "reversed" route
                    // for accurate calculation if necessary, or just use the properties assuming symmetry.
                    // For now, we can assume the cost/time calculation functions handle directionality
                    // by just taking the route object. The distance and biome/ship info are route properties.
                    else
                    {
                        // This case handles when edge.Route is the reverse (e.g., if we added B->A but stored A->B route object)
                        // It's still valid to use the route object's properties for distance/biomes/type.
                        actualRouteToUse = edge.Route;
                        actualSegmentOrigin = current; // This edge is from 'current'
                        actualSegmentDestination = edge.Destination; // To 'edge.Destination'
                    }


                    double routeDistance = 0; // Initialize for segment calculation
                    List<string> biomesTraversed = new List<string>(); // Initialize for land routes

                    if (actualRouteToUse is LandRoute landRoute)
                    {
                        routeDistance = landRoute.TotalDistance;
                        biomesTraversed = landRoute.Biomes;
                        timeCost = CalculateLandRouteTimeAndCost(landRoute, travelSpeed, mountType, numTravelers, out _);
                    }
                    else if (actualRouteToUse is SeaRoute seaRoute)
                    {
                        routeDistance = seaRoute.Distance;
                        timeCost = CalculateSeaRouteTimeAndCost(seaRoute, shipType, numTravelers, out _);
                    }

                    double newDist = distances[current] + timeCost;

                    if (newDist < distances[edge.Destination])
                    {
                        distances[edge.Destination] = newDist;
                        previous[edge.Destination] = actualRouteToUse; // Store the route that led to this path
                        priorityQueue.Enqueue(edge.Destination, newDist);
                    }
                }
            }

            List<JourneySegment> pathSegments = new List<JourneySegment>();
            List<ResourceCost> estimatedResourceCosts = new List<ResourceCost>();
            double totalRations = 0;
            double totalWaterLand = 0;
            double totalWaterSea = 0;

            if (previous[destination] == null && !origin.Equals(destination))
            {
                return new JourneyResult(origin, destination, null, null);
            }

            // Reconstruct path: start from destination and go backward using 'previous' dictionary
            Settlement pathCurrent = destination;
            // Use a Stack to build the path in reverse and then pop to get it in correct order
            Stack<JourneySegment> segmentsStack = new Stack<JourneySegment>();

            while (pathCurrent != null && !pathCurrent.Equals(origin))
            {
                IRoute route = previous[pathCurrent];
                if (route == null) break; // Should not happen if pathFound is true

                // Determine the correct start and end for this segment
                // Because graph edges are symmetrical, the route object we retrieved (previous[pathCurrent])
                // might have its origin as the *true* origin of the route, and its destination as the *true* destination.
                // However, in the context of traversing *backwards* from 'pathCurrent', the 'segmentStart'
                // is the settlement that led to 'pathCurrent' via 'route'.
                Settlement segmentEnd = pathCurrent;
                Settlement segmentStart = route.Origin.Equals(pathCurrent) ? route.Destination : route.Origin;

                // If segmentStart is null, it means the path reconstruction failed.
                if (segmentStart == null) break;

                double segmentTimeHours;
                double segmentCost;
                double segmentDistance;
                List<string> biomes = new List<string>();


                if (route is LandRoute landRouteSegment)
                {
                    segmentDistance = landRouteSegment.TotalDistance;
                    biomes = landRouteSegment.Biomes;
                    segmentTimeHours = CalculateLandRouteTimeAndCost(landRouteSegment, travelSpeed, mountType, numTravelers, out segmentCost);
                    totalRations += (segmentTimeHours / 24) * RATIONS_PER_PERSON_PER_DAY * numTravelers;
                    totalWaterLand += (segmentTimeHours / 24) * WATER_PER_PERSON_PER_DAY_LAND * numTravelers;
                }
                else if (route is SeaRoute seaRouteSegment)
                {
                    segmentDistance = seaRouteSegment.Distance;
                    segmentTimeHours = CalculateSeaRouteTimeAndCost(seaRouteSegment, shipType, numTravelers, out segmentCost);
                    totalRations += (segmentTimeHours / 24) * RATIONS_PER_PERSON_PER_DAY * numTravelers;
                    totalWaterSea += (segmentTimeHours / 24) * WATER_PER_PERSON_PER_DAY_SEA * numTravelers;
                }
                else
                {
                    segmentTimeHours = 0;
                    segmentCost = 0;
                    segmentDistance = 0;
                }

                // Add the segment to the stack (it will be reversed later)
                segmentsStack.Push(new JourneySegment(segmentStart, segmentEnd, segmentDistance, segmentTimeHours, segmentCost, route, biomes));
                pathCurrent = segmentStart;
            }

            // Pop segments from stack to get them in correct order (Origin -> Destination)
            while (segmentsStack.Count > 0)
            {
                pathSegments.Add(segmentsStack.Pop());
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

            double mountSpeedMph = 0;
            switch (mountType)
            {
                case MountType.RidingHorse:
                case MountType.Warhorse:
                    mountSpeedMph = 6;
                    break;
                case MountType.None:
                default:
                    mountSpeedMph = 0;
                    break;
            }

            double effectiveSpeedMph = (mountType != MountType.None) ? Math.Max(baseSpeedMph, mountSpeedMph) : baseSpeedMph;
            double effectiveSpeedKmh = effectiveSpeedMph * MPH_TO_KMH_FACTOR;

            if (effectiveSpeedKmh <= 0) return double.MaxValue;

            for (int i = 0; i < route.Biomes.Count; i++)
            {
                string biome = route.Biomes[i];
                double distance = route.BiomeDistances[i];
                double difficultyMultiplier = BiomeModifier.GetMultiplier(biome);

                double effectiveDistance = distance * difficultyMultiplier;

                totalTimeHours += effectiveDistance / effectiveSpeedKmh;

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
                    shipSpeedMph = 0;
                    break;
            }

            if (shipSpeedMph <= 0)
            {
                totalCost = double.MaxValue;
                return double.MaxValue;
            }

            double shipSpeedKmh = shipSpeedMph * MPH_TO_KMH_FACTOR;
            double timeHours = route.Distance / shipSpeedKmh;

            totalCost = route.Distance * 0.05 * numTravelers;

            return timeHours;
        }


        /// <summary>
        /// Prompts the DM to create a new land route and saves it,
        /// allowing choice between default and custom folders.
        /// </summary>
        public void CreateDmLandRoute() // Renamed from CreateLandRoute
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

            if (origin == null || destination == null)
            {
                Console.WriteLine("Invalid origin or destination settlement. Please ensure both exist or can be created.");
                return;
            }

            Console.WriteLine("Enter biomes traversed (comma-separated, e.g., Grasslands,Forest):");
            List<string> biomes = ConsoleInput.GetStringInput("Biomes: ").Split(',')
                                              .Select(s => s.Trim())
                                              .Where(s => !string.IsNullOrWhiteSpace(s))
                                              .ToList();

            Console.WriteLine("Enter distance for each biome in kilometers (comma-separated, e.g., 10km,5km):");
            List<double> biomeDistances = ConsoleInput.GetStringInput("Biome Distances: ").Split(',')
                                                    .Select(s => double.TryParse(s.Replace("km", "").Trim(), out double val) && val > 0 ? val : 0)
                                                    .Where(d => d > 0)
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
        /// Prompts the DM to create a new sea route and saves it,
        /// allowing choice between default and custom folders.
        /// </summary>
        public void CreateDmSeaRoute() // Renamed from CreateSeaRoute
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

        /// <summary>
        /// Allows a player to create a new custom land route. This route will *always* be saved as custom.
        /// </summary>
        public void CreatePlayerCustomLandRoute()
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

            Console.WriteLine("Enter biomes traversed (comma-separated, e.g., Grasslands,Forest):");
            List<string> biomes = ConsoleInput.GetStringInput("Biomes: ").Split(',')
                                              .Select(s => s.Trim())
                                              .Where(s => !string.IsNullOrWhiteSpace(s))
                                              .ToList();

            Console.WriteLine("Enter distance for each biome in kilometers (comma-separated, e.g., 10km,5km):");
            List<double> biomeDistances = ConsoleInput.GetStringInput("Biome Distances: ").Split(',')
                                                    .Select(s => double.TryParse(s.Replace("km", "").Trim(), out double val) && val > 0 ? val : 0)
                                                    .Where(d => d > 0)
                                                    .ToList();

            bool isMapped = ConsoleInput.GetBooleanInput("Is this route fully mapped (yes/no)?"); // Players can still mark if mapped

            if (biomes.Count == 0 || biomeDistances.Count == 0 || biomes.Count != biomeDistances.Count)
            {
                Console.WriteLine("Invalid biome or distance input. Number of biomes must match number of distances, and both must be provided.");
                return;
            }

            LandRoute newRoute = new LandRoute(origin, destination, biomes, biomeDistances, isMapped);

            // Always save as custom for players
            _dataManager.SaveRoute(newRoute, RouteType.Land, true);
            Console.WriteLine("Custom Land Route created and saved. This route will be available in future sessions.");
        }

        /// <summary>
        /// Allows a player to create a new custom sea route. This route will *always* be saved as custom.
        /// </summary>
        public void CreatePlayerCustomSeaRoute()
        {
            Console.WriteLine("\n--- Create New Custom Sea Route (Player) ---");
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

            // Always save as custom for players
            _dataManager.SaveRoute(newRoute, RouteType.Sea, true);
            Console.WriteLine("Custom Sea Route created and saved. This route will be available in future sessions.");
        }
    }
}
