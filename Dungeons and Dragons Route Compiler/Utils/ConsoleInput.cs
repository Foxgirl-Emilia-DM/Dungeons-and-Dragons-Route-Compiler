// ConsoleInput.cs
using System;
using System.Linq;

namespace YourFantasyWorldProject.Utils
{
    public static class ConsoleInput
    {
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
                Console.WriteLine($"Invalid input. Please enter a valid number greater than or equal to {minValue}.");
            }
        }

        // NEW METHOD: GetIntInput
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
                if (minValue == int.MinValue && maxValue == int.MaxValue)
                {
                    Console.WriteLine("Invalid input. Please enter a valid integer.");
                }
                else if (minValue != int.MinValue && maxValue != int.MaxValue)
                {
                    Console.WriteLine($"Invalid input. Please enter an integer between {minValue} and {maxValue}.");
                }
                else if (minValue != int.MinValue)
                {
                    Console.WriteLine($"Invalid input. Please enter an integer greater than or equal to {minValue}.");
                }
                else // maxValue is not int.MaxValue
                {
                    Console.WriteLine($"Invalid input. Please enter an integer less than or equal to {maxValue}.");
                }
            }
        }

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