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
            // NO LONGER adding a leading tab here.
            return $"{Destination.Name}\t{Destination.Region}\t{Distance}km";
        }

        /// <summary>
        /// Parses a string line from a sea route file into a SeaRoute object.
        /// Assumes the line is formatted as: DestinationName\tDestinationRegion\tDistancekm
        /// </summary>
        /// <param name="origin">The origin settlement, which is typically read from a preceding line in the file.</param>
        /// <param name="line">The tab-separated line representing the route segment.</param>
        /// <returns>A new SeaRoute object.</returns>
        public static SeaRoute ParseFromFileLine(Settlement origin, string line)
        {
            // Trim leading/trailing whitespace, but do NOT assume a leading tab.
            var parts = line.Trim().Split('\t');
            if (parts.Length != 3) // Now expect 3 parts (DestinationName, DestinationRegion, Distancekm)
            {
                throw new FormatException($"Invalid sea route line format. Expected 3 parts, got {parts.Length}: {line}");
            }

            string destName = parts[0].Trim(); // Now at index 0
            string destRegion = parts[1].Trim(); // Now at index 1
            double distance = double.Parse(parts[2].Replace("km", "").Trim()); // Now at index 2

            Settlement destination = new Settlement(destName, destRegion);

            return new SeaRoute(origin, destination, distance);
        }

        public override string ToString()
        {
            return $"{Origin.Name} ({Origin.Region}) --(Sea)--> {Destination.Name} ({Destination.Region}) | Distance: {Distance}km";
        }
    }
}
