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
        /// following the format: DestinationName\tDestinationRegion\tBiome1,Biome2\tDistance1km,Distance2km\tIsMapped
        /// This method represents the route *from* an implied origin.
        /// </summary>
        /// <returns>A formatted string for file storage.</returns>
        public string ToFileString()
        {
            // Destination Name and Region will now retain their original casing from the Settlement object.
            // NO LONGER adding a leading tab here.
            return $"{Destination.Name}\t{Destination.Region}\t{string.Join(",", Biomes)}\t{string.Join(",", BiomeDistances.Select(d => $"{d}km"))}\t{IsMapped}";
        }


        /// <summary>
        /// Parses a string line from a land route file into a LandRoute object.
        /// Assumes the line is formatted as: DestinationName\tDestinationRegion\tBiome1,Biome2\tDistance1km,Distance2km\tIsMapped
        /// </summary>
        /// <param name="origin">The origin settlement, which is typically read from a preceding line in the file.</param>
        /// <param name="line">The tab-separated line representing the route segment.</param>
        /// <returns>A new LandRoute object.</returns>
        public static LandRoute ParseFromFileLine(Settlement origin, string line)
        {
            // Trim leading/trailing whitespace, but do NOT assume a leading tab.
            var parts = line.Trim().Split('\t');
            if (parts.Length != 5) // Now expect 5 parts (DestinationName, DestinationRegion, Biomes, BiomeDistances, IsMapped)
            {
                throw new FormatException($"Invalid land route line format. Expected 5 parts, got {parts.Length}: {line}");
            }

            string destName = parts[0].Trim(); // Now at index 0
            string destRegion = parts[1].Trim(); // Now at index 1
            List<string> biomes = parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(); // Now at index 2
            List<double> biomeDistances = parts[3].Split(',', StringSplitOptions.RemoveEmptyEntries) // Now at index 3
                                                 .Select(s => double.Parse(s.Replace("km", "").Trim()))
                                                 .ToList();
            bool isMapped = bool.Parse(parts[4].Trim()); // Now at index 4

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
