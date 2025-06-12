// SeaRoute.cs
using System;
using System.Globalization; // For TextInfo for title casing

namespace YourFantasyWorldProject.Classes
{
    public class SeaRoute : IRoute
    {
        public Settlement Origin { get; set; }
        public Settlement Destination { get; set; }
        public double Distance { get; set; }

        public SeaRoute(Settlement origin, Settlement destination, double distance)
        {
            Origin = origin;
            Destination = destination;
            Distance = distance;
        }

        // Method to format the route for writing to a .txt file
        public string ToFileString()
        {
            // Destination Name and Region will now retain their original casing from the Settlement object.
            return $"\t{Destination.Name}\t{Destination.Region}\t{Distance}km";
        }

        /// <summary>
        /// Parses a string line from a sea route file into a SeaRoute object.
        /// Assumes the line is formatted as: \tDestinationName\tDestinationRegion\tDistancekm
        /// </summary>
        /// <param name="origin">The origin settlement, which is typically read from a preceding line in the file.</param>
        /// <param name="line">The tab-separated line representing the route segment.</param>
        /// <returns>A new SeaRoute object.</returns>
        public static SeaRoute ParseFromFileLine(Settlement origin, string line)
        {
            var parts = line.Trim().Split('\t');
            if (parts.Length != 4)
            {
                throw new FormatException($"Invalid sea route line format. Expected 4 parts, got {parts.Length}: {line}");
            }

            // The first part is empty due to leading tab, so parts[1] is Destination.Name
            string destName = parts[1].Trim();
            string destRegion = parts[2].Trim();
            double distance = double.Parse(parts[3].Replace("km", "").Trim());

            Settlement destination = new Settlement(destName, destRegion);

            return new SeaRoute(origin, destination, distance);
        }

        public override string ToString()
        {
            return $"{Origin.Name} ({Origin.Region}) --(Sea)--> {Destination.Name} ({Destination.Region}) | Distance: {Distance}km";
        }
    }
}
