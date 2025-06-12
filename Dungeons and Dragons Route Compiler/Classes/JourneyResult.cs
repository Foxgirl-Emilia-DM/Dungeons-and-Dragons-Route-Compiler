// JourneyResult.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization; // For TextInfo for title casing
using System.Text; // For StringBuilder

namespace YourFantasyWorldProject.Classes
{
    // Encapsulates the full result of a pathfinding query
    public class JourneyResult
    {
        public Settlement Origin { get; }
        public Settlement Destination { get; }
        public double TotalDistanceKm { get; private set; }
        public double TotalTimeHours { get; private set; }
        public double TotalCost { get; private set; }
        public List<JourneySegment> PathSegments { get; }
        public List<ResourceCost> EstimatedResourceCosts { get; } // Renamed for clarity, though current use is limited
        public bool PathFound { get; }

        // New properties for detailed journey calculations (Patch 2)
        public int NumberOfTravelers { get; private set; }
        public MountType ChosenLandTransport { get; private set; }
        public ShipType ChosenSeaTransport { get; private set; }
        public string VehicleChoice { get; private set; } // "wagon", "carriage", "none"
        public int NumberOfMountsNeeded { get; private set; }
        public double TotalRationsConsumed { get; private set; }
        public double TotalWaterConsumed { get; private set; }
        public double TravelHoursPerDay { get; private set; }
        public double TotalTravelerWeightLbs { get; private set; } // Added for clarity
        public bool OwnsMounts { get; private set; } // New: Patch 3
        public bool OwnsVehicle { get; private set; } // New: Patch 3


        // Constructor for the JourneyResult.
        // It now takes additional parameters to capture the detailed calculation results.
        public JourneyResult(
            Settlement origin, Settlement destination, List<JourneySegment> pathSegments, List<ResourceCost> estimatedResourceCosts,
            int numberOfTravelers, MountType chosenLandTransport, ShipType chosenSeaTransport, string vehicleChoice,
            int numberOfMountsNeeded, double totalRationsConsumed, double totalWaterConsumed, double travelHoursPerDay,
            double totalTravelerWeightLbs, bool ownsMounts, bool ownsVehicle
        )
        {
            Origin = origin;
            Destination = destination;
            PathSegments = pathSegments ?? new List<JourneySegment>();
            EstimatedResourceCosts = estimatedResourceCosts ?? new List<ResourceCost>();

            // Calculate totals from segments. These will be recalculated and updated by Pathfinder
            // after initial pathfinding, during the detailed cost/time calculation phase.
            TotalDistanceKm = PathSegments.Sum(s => s.DistanceKm);
            TotalTimeHours = PathSegments.Sum(s => s.TimeHours);
            TotalCost = PathSegments.Sum(s => s.Cost); // This will be the initial, raw cost.

            PathFound = pathSegments != null && pathSegments.Any();

            // Initialize new properties from Patch 2 and Patch 3
            NumberOfTravelers = numberOfTravelers;
            ChosenLandTransport = chosenLandTransport;
            ChosenSeaTransport = chosenSeaTransport;
            VehicleChoice = vehicleChoice;
            NumberOfMountsNeeded = numberOfMountsNeeded;
            TotalRationsConsumed = totalRationsConsumed;
            TotalWaterConsumed = totalWaterConsumed;
            TravelHoursPerDay = travelHoursPerDay;
            TotalTravelerWeightLbs = totalTravelerWeightLbs;
            OwnsMounts = ownsMounts; // New: Patch 3
            OwnsVehicle = ownsVehicle; // New: Patch 3
        }

        // Method to update the journey totals after detailed calculations are performed
        // This is necessary because initial segment costs/times might be based on default
        // speeds, and then adjusted based on user inputs (mounts, daily travel hours).
        public void UpdateTotals(double totalDistanceKm, double totalTimeHours, double totalCost,
                                 double totalRationsConsumed, double totalWaterConsumed)
        {
            TotalDistanceKm = totalDistanceKm;
            TotalTimeHours = totalTimeHours;
            TotalCost = totalCost;
            TotalRationsConsumed = totalRationsConsumed;
            TotalWaterConsumed = totalWaterConsumed;
        }

        /// <summary>
        /// Generates a formatted string summary of the journey result.
        /// </summary>
        /// <returns>A string containing the detailed journey summary.</returns>
        public string GetFormattedResult()
        {
            StringBuilder sb = new StringBuilder();

            if (!PathFound)
            {
                sb.AppendLine($"\nNo path found from {Origin.Name} ({Origin.Region}) to {Destination.Name} ({Destination.Region}).");
                return sb.ToString();
            }

            sb.AppendLine($"\n--- Journey from {Origin.Name} ({Origin.Region}) to {Destination.Name} ({Destination.Region}) ---");
            sb.AppendLine($"Number of Travelers: {NumberOfTravelers}");
            sb.AppendLine($"Travel Hours Per Day: {TravelHoursPerDay:F1}");

            if (OwnsMounts && ChosenLandTransport != MountType.None && ChosenLandTransport != MountType.Foot)
            {
                sb.AppendLine($"Mounts owned: Yes");
            }
            if (OwnsVehicle && !string.IsNullOrEmpty(VehicleChoice) && VehicleChoice != "none")
            {
                sb.AppendLine($"Vehicle owned: Yes");
            }


            sb.AppendLine("\n--- Path Segments ---");
            for (int i = 0; i < PathSegments.Count; i++)
            {
                var segment = PathSegments[i];
                string routeDescription = "";
                if (segment.RouteUsed is LandRoute lr)
                {
                    // Format biome names to Title Case for display
                    TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
                    string formattedBiomes = string.Join(", ", lr.Biomes.Select(b => textInfo.ToTitleCase(b.ToLower())));
                    routeDescription = $"--(Land Route)--> {segment.End.Name} ({segment.End.Region}) (Biomes: {formattedBiomes})";
                }
                else if (segment.RouteUsed is SeaRoute sr)
                {
                    routeDescription = $"--(Sea Route)--> {segment.End.Name} ({segment.End.Region})";
                }

                sb.AppendLine($"Segment {i + 1}: {segment.Start.Name} ({segment.Start.Region}) {routeDescription}");
                sb.AppendLine($"   - Distance: {segment.DistanceKm:F2} km");
                sb.AppendLine($"   - Time: {segment.TimeHours:F2} hours");
                sb.AppendLine($"   - Cost: {segment.Cost:F2} gold");
                if (segment.BiomesTraversed.Any())
                {
                    sb.AppendLine($"   - Biomes Traversed: {string.Join(", ", segment.BiomesTraversed)}");
                }
            }

            sb.AppendLine("\n--- Path Total ---");
            sb.AppendLine($"Distance: {TotalDistanceKm:F2} km");
            double totalDays = TotalTimeHours / TravelHoursPerDay;
            // Ensure total days is at least 1 if there is any travel time
            totalDays = TotalTimeHours > 0 && TravelHoursPerDay > 0 ? Math.Max(1, Math.Ceiling(totalDays)) : 0;

            sb.AppendLine($"Time: {TotalTimeHours:F2} hrs / {totalDays:F0} days"); // Round days up
            sb.AppendLine($"Cost: {TotalCost:F2} gold");

            if (ChosenLandTransport != MountType.None && ChosenLandTransport != MountType.Foot)
            {
                sb.AppendLine($"Number of mounts: {NumberOfMountsNeeded} {ChosenLandTransport}");
                sb.AppendLine($"Using cart, carriage or none: {VehicleChoice}");
                sb.AppendLine("Note: Price for all mounts per day in addition to price for vehicle added to cost.");
                sb.AppendLine($"Total Weight Carried (Lbs): {TotalTravelerWeightLbs:F2}");
            }

            sb.AppendLine("\n--- Resource Consumption ---");
            sb.AppendLine($"- Rations: {Math.Ceiling(TotalRationsConsumed):F0} (rounded up to the next integer)");
            sb.AppendLine($"- Water: {Math.Ceiling(TotalWaterConsumed):F0} (rounded up to the next liter -- double if it is over sea or through a desert)");
            sb.AppendLine("Note: Rations included in cost.");

            return sb.ToString();
        }

        /// <summary>
        /// Displays the formatted result string to the console.
        /// </summary>
        public void DisplayResult()
        {
            Console.WriteLine(GetFormattedResult());
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

    // Helper class for resource costs (currently less used directly as costs are summed into TotalCost)
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
