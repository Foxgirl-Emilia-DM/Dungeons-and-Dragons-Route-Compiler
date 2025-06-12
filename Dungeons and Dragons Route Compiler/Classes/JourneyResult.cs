// JourneyResult.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace YourFantasyWorldProject.Classes
{
    // Encapsulates the full result of a pathfinding query
    public class JourneyResult
    {
        public Settlement Origin { get; }
        public Settlement Destination { get; }
        public double TotalDistanceKm { get; }
        public double TotalTimeHours { get; }
        public double TotalCost { get; }
        public List<JourneySegment> PathSegments { get; }
        public List<ResourceCost> EstimatedResourceCosts { get; }
        public bool PathFound { get; }

        public JourneyResult(Settlement origin, Settlement destination, List<JourneySegment> pathSegments, List<ResourceCost> estimatedResourceCosts)
        {
            Origin = origin;
            Destination = destination;
            PathSegments = pathSegments ?? new List<JourneySegment>();
            EstimatedResourceCosts = estimatedResourceCosts ?? new List<ResourceCost>();

            TotalDistanceKm = PathSegments.Sum(s => s.DistanceKm);
            TotalTimeHours = PathSegments.Sum(s => s.TimeHours);
            TotalCost = PathSegments.Sum(s => s.Cost);
            PathFound = pathSegments != null && pathSegments.Any(); // A path is found if there are any segments
        }

        public void DisplayResult()
        {
            if (!PathFound)
            {
                // MODIFIED: Use Region instead of Country for display
                Console.WriteLine($"No path found from {Origin.Name} ({Origin.Region}) to {Destination.Name} ({Destination.Region}).");
                return;
            }

            Console.WriteLine("\n--- Journey Details ---");
            Console.WriteLine($"From: {Origin.Name} ({Origin.Region})");
            Console.WriteLine($"To: {Destination.Name} ({Destination.Region})");
            Console.WriteLine($"Total Distance: {TotalDistanceKm:F2} km");
            Console.WriteLine($"Total Travel Time: {TotalTimeHours:F2} hours ({TotalTimeHours / 24:F2} days)");
            Console.WriteLine($"Total Estimated Cost: {TotalCost:F2} gold");

            Console.WriteLine("\n--- Resource Consumption ---");
            if (EstimatedResourceCosts.Any())
            {
                foreach (var cost in EstimatedResourceCosts)
                {
                    Console.WriteLine($" - {cost}");
                }
            }
            else
            {
                Console.WriteLine("No specific resource costs estimated for this journey.");
            }


            Console.WriteLine("\n--- Path Segments ---");
            for (int i = 0; i < PathSegments.Count; i++)
            {
                var segment = PathSegments[i];
                string routeType = (segment.RouteUsed is LandRoute) ? "Land Route" : "Sea Route";
                string biomesInfo = (segment.RouteUsed is LandRoute landRoute && landRoute.Biomes.Any())
                                    ? $" (Biomes: {string.Join(", ", landRoute.Biomes)})"
                                    : "";

                Console.WriteLine(
                    $"Segment {i + 1}: {segment.Start.Name} ({segment.Start.Region}) --({routeType})--> {segment.End.Name} ({segment.End.Region}){biomesInfo}\n" +
                    $"   - Distance: {segment.DistanceKm:F2} km\n" +
                    $"   - Time: {segment.TimeHours:F2} hours\n" +
                    $"   - Cost: {segment.Cost:F2} gold"
                );
            }
        }
    }

    // Helper class to represent a single segment of the journey
    public class JourneySegment
    {
        public Settlement Start { get; }
        public Settlement End { get; }
        public double DistanceKm { get; }
        public double TimeHours { get; }
        public double Cost { get; }
        public IRoute RouteUsed { get; } // Reference to the actual route object
        public List<string> BiomesTraversed { get; } // Biomes specific to LandRoute

        public JourneySegment(Settlement start, Settlement end, double distanceKm, double timeHours, double cost, IRoute routeUsed, List<string> biomesTraversed = null)
        {
            Start = start;
            End = end;
            DistanceKm = distanceKm;
            TimeHours = timeHours;
            Cost = cost;
            RouteUsed = routeUsed;
            BiomesTraversed = biomesTraversed ?? new List<string>();
        }
    }

    // Helper class for resource costs
    public class ResourceCost
    {
        public string ResourceName { get; }
        public double Quantity { get; }

        public ResourceCost(string resourceName, double quantity)
        {
            ResourceName = resourceName;
            Quantity = quantity;
        }

        public override string ToString()
        {
            return $"{ResourceName}: {Quantity:F2}";
        }
    }
}
