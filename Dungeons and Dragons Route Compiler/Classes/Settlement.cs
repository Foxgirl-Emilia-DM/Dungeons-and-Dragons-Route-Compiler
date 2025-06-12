// Settlement.cs
using System;
using System.Collections.Generic;

namespace YourFantasyWorldProject.Classes
{
    public class Settlement
    {
        // Store original casing for display, but use invariant uppercase for internal comparisons.
        public string Name { get; private set; }
        public string Region { get; private set; }

        // Internal properties for case-insensitive comparisons
        private string _nameInvariant;
        private string _regionInvariant;

        public Settlement(string name, string region)
        {
            // Store original casing
            Name = name ?? "";
            Region = region ?? "";

            // Store invariant uppercase for efficient comparison
            _nameInvariant = Name.ToUpperInvariant();
            _regionInvariant = Region.ToUpperInvariant();
        }

        // Override Equals for proper comparison in collections (e.g., HashSet, List.Contains)
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            Settlement other = (Settlement)obj;
            // Settlements are equal if their names and regions match (case-insensitive)
            return _nameInvariant.Equals(other._nameInvariant) &&
                   _regionInvariant.Equals(other._regionInvariant);
        }

        // Override GetHashCode for efficient use in hash-based collections (like Dictionary or HashSet)
        public override int GetHashCode()
        {
            // Combine hash codes of invariant Name and Region for a unique hash
            return StringComparer.OrdinalIgnoreCase.GetHashCode(_nameInvariant) ^
                   StringComparer.OrdinalIgnoreCase.GetHashCode(_regionInvariant);
        }

        public override string ToString()
        {
            return $"{Name} ({Region})";
        }
    }
}
