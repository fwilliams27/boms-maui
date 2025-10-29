using System;
using System.Collections.Generic;

namespace BOMS.Data
{
    public static class MockData
    {
        // NEW: a default batch size you can use for stress tests (tweak as needed)
        public const int DefaultStressBatch = 100;

        public static List<DrinkDefinition> Drinks = new()
        {
            new DrinkDefinition("Brewed Coffee (Pike Place Roast)", 0.2),
            new DrinkDefinition("Caffè Americano", 0.3),
            new DrinkDefinition("Caffè Latte", 0.5),
            new DrinkDefinition("Cappuccino", 0.6),
            new DrinkDefinition("Flat White", 0.55),
            new DrinkDefinition("Caffè Mocha", 0.8),
            new DrinkDefinition("Vanilla Latte", 0.7),
            new DrinkDefinition("Pumpkin Spice Latte", 0.9),
            new DrinkDefinition("Iced Latte", 0.6),
            new DrinkDefinition("Iced Coffee", 0.4),
            new DrinkDefinition("Matcha Green Tea Latte", 0.7),
            new DrinkDefinition("Chai Tea Latte", 0.7),
            new DrinkDefinition("Caramel Frappuccino", 1.0),
            new DrinkDefinition("Mocha Frappuccino", 1.0),
            new DrinkDefinition("Java Chip Frappuccino", 1.1),
            new DrinkDefinition("Pink Drink", 0.6)
        };

        private static readonly Random _rng = new();

        /// <summary>
        /// Returns a single random drink definition.
        /// </summary>
        public static DrinkDefinition GetRandomDrink()
        {
            int index = _rng.Next(Drinks.Count);
            return Drinks[index];
        }

        /// <summary>
        /// NEW: Returns a batch of random drink definitions (for stress/perf seeding).
        /// </summary>
        public static List<DrinkDefinition> GetRandomDrinkBatch(int count)
        {
            var list = new List<DrinkDefinition>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(GetRandomDrink());
            }
            return list;
        }
    }

    public record DrinkDefinition(string Name, double Complexity);
}
