using System;

namespace YourFantasyWorldProject.Classes
{
    public class Settlement
    {
        public string Name { get; set; }
        public string Country { get; set; }

        public Settlement(string name, string country)
        {
            // Store names in an invariant culture uppercase for consistent comparison
            Name = name.ToUpperInvariant();
            Country = country.ToUpperInvariant();
        }

        // Override Equals for proper comparison in collections (e.g., HashSet, List.Contains)
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            Settlement other = (Settlement)obj;
            // Settlements are equal if their names and countries match (case-insensitive)
            return Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase) &&
                   Country.Equals(other.Country, StringComparison.OrdinalIgnoreCase);
        }

        // Override GetHashCode for efficient use in hash-based collections (like Dictionary or HashSet)
        public override int GetHashCode()
        {
            // Combine hash codes of Name and Country for a unique hash
            // Using a simple XOR combination
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Name) ^
                   StringComparer.OrdinalIgnoreCase.GetHashCode(Country);
        }

        public override string ToString()
        {
            return $"{Name} ({Country})";
        }
    }
}