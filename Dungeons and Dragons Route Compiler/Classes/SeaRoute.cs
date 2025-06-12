// SeaRoute.cs
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
            // MODIFIED: Changed Destination.Country to Destination.Region
            return $"\t{Destination.Name}\t{Destination.Region}\t{Distance}km";
        }

        public override string ToString()
        {
            // MODIFIED: Changed Origin.Country and Destination.Country to Origin.Region and Destination.Region
            return $"{Origin.Name} ({Origin.Region}) --(Sea)--> {Destination.Name} ({Destination.Region}) | Distance: {Distance}km";
        }
    }
}