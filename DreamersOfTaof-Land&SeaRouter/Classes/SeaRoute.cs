using System;

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
            return $"\t{Destination.Name}\t{Destination.Country}\t{Distance}km";
        }

        public override string ToString()
        {
            return $"{Origin.Name} ({Origin.Country}) --(Sea)--> {Destination.Name} ({Destination.Country}) | Distance: {Distance}km";
        }
    }
}