// ConsoleInput.cs
using System;
using System.Linq; // Added for Any() for future-proofing, though not directly used in this snippet

namespace YourFantasyWorldProject.Utils
{
    public static class ConsoleInput
    {
        /// <summary>
        /// Prompts the user for string input and returns it.
        /// </summary>
        /// <param name="prompt">The message to display to the user.</param>
        /// <param name="allowEmpty">If true, allows an empty string as valid input.</param>
        /// <returns>The string entered by the user.</returns>
        public static string GetStringInput(string prompt, bool allowEmpty = false)
        {
            string input;
            do
            {
                Console.Write(prompt);
                input = Console.ReadLine()?.Trim();
                if (!allowEmpty && string.IsNullOrWhiteSpace(input))
                {
                    Console.WriteLine("Input cannot be empty. Please try again.");
                }
            } while (!allowEmpty && string.IsNullOrWhiteSpace(input));
            return input;
        }

        /// <summary>
        /// Prompts the user for a double input and validates it against a minimum value.
        /// </summary>
        /// <param name="prompt">The message to display to the user.</param>
        /// <param name="minValue">The minimum allowed value (default is 0).</param>
        /// <returns>The double entered by the user.</returns>
        public static double GetDoubleInput(string prompt, double minValue = 0)
        {
            double value;
            while (true)
            {
                Console.Write(prompt);
                string input = Console.ReadLine();
                if (double.TryParse(input, out value) && value >= minValue)
                {
                    return value;
                }
                Console.WriteLine($"Invalid input. Please enter a valid number greater than or equal to {minValue:F2}.");
            }
        }

        /// <summary>
        /// Prompts the user for an integer input and validates it against a min and max value.
        /// </summary>
        /// <param name="prompt">The message to display to the user.</param>
        /// <param name="minValue">The minimum allowed integer value.</param>
        /// <param name="maxValue">The maximum allowed integer value.</param>
        /// <returns>The integer entered by the user.</returns>
        public static int GetIntInput(string prompt, int minValue = int.MinValue, int maxValue = int.MaxValue)
        {
            int value;
            while (true)
            {
                Console.Write(prompt);
                string input = Console.ReadLine();
                if (int.TryParse(input, out value) && value >= minValue && value <= maxValue)
                {
                    return value;
                }
                Console.WriteLine($"Invalid input. Please enter a valid integer between {minValue} and {maxValue}.");
            }
        }

        /// <summary>
        /// Prompts the user for a boolean (yes/no) input.
        /// </summary>
        /// <param name="prompt">The message to display to the user.</param>
        /// <returns>True if 'yes', false if 'no'.</returns>
        public static bool GetBooleanInput(string prompt)
        {
            while (true)
            {
                string input = GetStringInput($"{prompt} (yes/no): ").ToLowerInvariant();
                if (input == "yes" || input == "y")
                {
                    return true;
                }
                else if (input == "no" || input == "n")
                {
                    return false;
                }
                else
                {
                    Console.WriteLine("Invalid input. Please enter 'yes' or 'no'.");
                }
            }
        }

        /// <summary>
        /// Prompts the user to select an enum value from a displayed list.
        /// </summary>
        /// <typeparam name="T">The enum type.</typeparam>
        /// <param name="prompt">The message to display to the user.</param>
        /// <returns>The selected enum value.</returns>
        public static T GetEnumInput<T>(string prompt) where T : Enum
        {
            while (true)
            {
                Console.WriteLine(prompt);
                // Display available options
                var names = Enum.GetNames(typeof(T));
                for (int i = 0; i < names.Length; i++)
                {
                    Console.WriteLine($"  {i + 1}. {names[i]}");
                }

                string input = GetStringInput("Enter the number corresponding to your choice: ");
                if (int.TryParse(input, out int choice) && choice > 0 && choice <= names.Length)
                {
                    return (T)Enum.Parse(typeof(T), names[choice - 1]);
                }
                Console.WriteLine("Invalid choice. Please enter a valid number from the list.");
            }
        }
    }
}
