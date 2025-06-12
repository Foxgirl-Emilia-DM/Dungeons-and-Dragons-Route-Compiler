using System;
using System.Collections.Generic;
using System.Linq;

namespace YourFantasyWorldProject.Classes
{
    public class LandRoute : IRoute // MODIFIED LINE
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

        public string ToFileString()
        {
            string biomeListStr = string.Join(", ", Biomes);
            string biomeDistanceListStr = string.Join(", ", BiomeDistances.Select(d => $"{d}km"));
            return $"\t{Destination.Name}\t{Destination.Country}\t{biomeListStr}\t{biomeDistanceListStr}\t{IsMapped}";
        }

        public override string ToString()
        {
            return $"{Origin.Name} ({Origin.Country}) --(Land)--> {Destination.Name} ({Destination.Country}) | Biomes: {string.Join(", ", Biomes)} | Distances: {string.Join(", ", BiomeDistances.Select(d => $"{d}km"))} | Total: {TotalDistance}km | Mapped: {IsMapped}";
        }
    }
}