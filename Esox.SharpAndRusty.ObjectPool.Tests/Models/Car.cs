using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esox.SharpAndRusty.ObjectPool.Tests.Models;

public class Car(string make, string model)
{
    public string Make { get; set; } = make;
    public string Model { get; set; } = model;

    public static List<Car> GetInitialCars()
    {
        return
        [
            new Car("Ford", "Focus"),
            new Car("Ford", "Fiesta"),
            new Car("Ford", "Mondeo"),
            new Car("Ford", "Mustang"),
            new Car("Citroen", "DS"),
            new Car("Citroen", "C1"),
            new Car("Citroen", "C2")
        ];
    }
}
