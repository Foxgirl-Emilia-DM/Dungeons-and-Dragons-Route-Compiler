using System.Collections.Generic;

namespace YourFantasyWorldProject.Classes
{
    public static class BiomeModifier
    {
        // Stores biome names and their difficulty multipliers.
        // A multiplier of 1 means no change. A multiplier of 2 means twice the distance.
        private static readonly Dictionary<string, double> _biomeDifficulties = new Dictionary<string, double>
        {
            // Standard Terrains
            { "GRASSLANDS", 1.0 },
            { "WETLANDS", 2.0 },        // Difficult terrain
            { "HOT DESERT", 2.0 },
            { "COLD DESERT", 2.0 },
            { "TUNDRA", 2.0 },      // Difficult terrain
            { "TAIGA", 2.0 },       // Difficult terrain
            { "GLACIER", 2.0 },     // Very difficult terrain
            { "TEMPERATE RAINFOREST", 1.0 }, // Can be dense
            { "TROPICAL RAINFOREST", 2.0 },      // Very difficult terrain
            { "TROPICAL SEASONAL FOREST", 2.0 },
            { "TEMPERATE DECIDUOUS FOREST", 1.0 },
            { "SAVANNA", 1.0 },
            // Add more biomes as needed
            // Example: "CITY", 1.0
            // Example: "ROAD", 0.8 (if you want to model easier travel on roads)
        };

        // Get the multiplier for a given biome. Defaults to 1.0 if not found.
        public static double GetMultiplier(string biomeName)
        {
            if (_biomeDifficulties.TryGetValue(biomeName.ToUpperInvariant(), out double multiplier))
            {
                return multiplier;
            }
            // If a biome isn't explicitly defined, assume no extra difficulty
            return 1.0;
        }
    }
}