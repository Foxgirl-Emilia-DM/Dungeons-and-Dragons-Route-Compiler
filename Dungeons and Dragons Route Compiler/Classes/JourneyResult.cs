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
                // MODIFIED: Use Region instead of Country for console output
                Console.WriteLine($"No path found from {Origin.Name} ({Origin.Region}) to {Destination.Name} ({Destination.Region}).");
                return;
            }

            Console.WriteLine("\n--- Journey Summary ---");
            // MODIFIED: Use Region instead of Country for console output
            Console.WriteLine($"Route from {Origin.Name} ({Origin.Region}) to {Destination.Name} ({Destination.Region})");
            Console.WriteLine($"Total Distance: {TotalDistanceKm:F2} km");
            Console.WriteLine($"Total Estimated Time: {TotalTimeHours:F2} hours ({TotalTimeHours / 24.0:F2} days)");
            Console.WriteLine($"Total Estimated Gold Cost: {TotalCost:F2} gold");

            if (EstimatedResourceCosts.Any())
            {
                Console.WriteLine("\n--- Estimated Resource Consumption ---");
                foreach (var resource in EstimatedResourceCosts)
                {
                    Console.WriteLine($"- {resource}");
                }
            }

            Console.WriteLine("\n--- Detailed Path ---");
            for (int i = 0; i < PathSegments.Count; i++)
            {
                var segment = PathSegments[i];
                string routeType = segment.RouteUsed is LandRoute ? "Land Route" : "Sea Route";
                string biomeInfo = segment.BiomesTraversed.Any() ? $" (Biomes: {string.Join(", ", segment.BiomesTraversed)})" : "";
                string routeDetails = "";

                if (segment.RouteUsed is LandRoute lr)
                {
                    routeDetails = $" (Mapped: {lr.IsMapped})";
                }
                else if (segment.RouteUsed is SeaRoute sr)
                {
                    // No specific details for basic SeaRoute other than distance
                }

                Console.WriteLine(
                    // MODIFIED: Use Region instead of Country for console output
                    $"{i + 1}. From {segment.Start.Name} ({segment.Start.Region}) to {segment.End.Name} ({segment.End.Region}) " +
                    $"(via {routeType}){biomeInfo}{routeDetails}:" + Environment.NewLine +
                    $"   - Distance: {segment.DistanceKm:F2} km" + Environment.NewLine +
                    $"   - Time: {segment.TimeHours:F2} hours" + Environment.NewLine +
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
            return $"{ResourceName}: {Quantity}";
        }
    }
}