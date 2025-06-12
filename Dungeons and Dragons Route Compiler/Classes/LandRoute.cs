// LandRoute.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization; // For TextInfo for title casing

namespace YourFantasyWorldProject.Classes
{
    public class LandRoute : IRoute
    {
        public Settlement Origin { get; set; }
        public Settlement Destination { get; set; }
        public List<string> Biomes { get; set; }
        public List<double> BiomeDistances { get; set; }
        public bool IsMapped { get; set; }
        public double TotalDistance => BiomeDistances?.Sum() ?? 0;

        public LandRoute(Settlement origin, Settlement destination, List<string> biomes, List<double> biomeDistances, bool isMapped)
        {
            Origin = origin;
            Destination = destination;
            Biomes = biomes ?? new List<string>();
            BiomeDistances = biomeDistances ?? new List<double>();
            IsMapped = isMapped;

            if (Biomes.Count != BiomeDistances.Count)
            {
                throw new ArgumentException("Number of biomes must match number of biome distances.");
            }
        }

        /// <summary>
        /// Formats the LandRoute into a string suitable for file storage,
        /// following the format: \tDestinationName\tDestinationRegion\tBiome1,Biome2\tDistance1km,Distance2km\tIsMapped
        /// This method represents the route *from* an implied origin.
        /// </summary>
        public string ToFileString()
        {
            // Format biome names to Title Case for output
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            string biomeListStr = string.Join(",", Biomes.Select(b => textInfo.ToTitleCase(b.ToLower()))); // Convert to lowercase first, then title case
            string biomeDistanceListStr = string.Join(",", BiomeDistances.Select(d => $"{d}km"));

            // Destination Name and Region will now retain their original casing from the Settlement object.
            return $"\t{Destination.Name}\t{Destination.Region}\t{biomeListStr}\t{biomeDistanceListStr}\t{IsMapped}";
        }

        /// <summary>
        /// Parses a string line from a land route file into a LandRoute object.
        /// Assumes the line is formatted as: \tDestinationName\tDestinationRegion\tBiome1,Biome2\tDistance1km,Distance2km\tIsMapped
        /// </summary>
        /// <param name="origin">The origin settlement, which is typically read from a preceding line in the file.</param>
        /// <param name="line">The tab-separated line representing the route segment.</param>
        /// <returns>A new LandRoute object.</returns>
        public static LandRoute ParseFromFileLine(Settlement origin, string line)
        {
            var parts = line.Trim().Split('\t');
            if (parts.Length != 6)
            {
                throw new FormatException($"Invalid land route line format. Expected 6 parts, got {parts.Length}: {line}");
            }

            // The first part is empty due to leading tab, so parts[1] is Destination.Name
            string destName = parts[1].Trim();
            string destRegion = parts[2].Trim();
            // Store biomes as they are read, ToTitleCase will be applied on output
            List<string> biomes = parts[3].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
            List<double> biomeDistances = parts[4].Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                 .Select(s => double.Parse(s.Replace("km", "").Trim()))
                                                 .ToList();
            bool isMapped = bool.Parse(parts[5].Trim());

            Settlement destination = new Settlement(destName, destRegion);

            return new LandRoute(origin, destination, biomes, biomeDistances, isMapped);
        }

        public override string ToString()
        {
            // Format biome names to Title Case for display
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            string formattedBiomes = string.Join(", ", Biomes.Select(b => textInfo.ToTitleCase(b.ToLower())));

            return $"{Origin.Name} ({Origin.Region}) --(Land)--> {Destination.Name} ({Destination.Region}) | Biomes: {formattedBiomes} | Distances: {string.Join(", ", BiomeDistances.Select(d => $"{d}km"))} | Mapped: {IsMapped}";
        }
    }
}
