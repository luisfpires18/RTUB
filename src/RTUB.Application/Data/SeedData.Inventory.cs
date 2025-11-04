using Microsoft.EntityFrameworkCore;
using RTUB.Core.Entities;

namespace RTUB.Application.Data;

/// <summary>
/// Seeds inventory data: instruments and products
/// </summary>
public static partial class SeedData
{
    public static async Task SeedInventoryAsync(ApplicationDbContext dbContext)
    {
        // Seed Sample Instruments
        if (!await dbContext.Instruments.AnyAsync())
        {
            var instruments = new[]
            {
                CreateInstrument("Guitarra", "Guitarra #1", "Fender", "SN001", Core.Enums.InstrumentCondition.Excellent, "Sala de Ensaio"),
                CreateInstrument("Guitarra", "Guitarra #2", "Ibanez", "SN002", Core.Enums.InstrumentCondition.Good, "Sala de Ensaio"),
                CreateInstrument("Guitarra", "Guitarra #3", "Yamaha", null, Core.Enums.InstrumentCondition.Worn, "Armazém"),
                CreateInstrument("Bandolim", "Bandolim #1", "Artisan", "SN101", Core.Enums.InstrumentCondition.Excellent, "Sala de Ensaio"),
                CreateInstrument("Bandolim", "Bandolim #2", null, null, Core.Enums.InstrumentCondition.Good, "Sala de Ensaio"),
                CreateInstrument("Percussão", "Cajón #1", "Meinl", null, Core.Enums.InstrumentCondition.Good, "Sala de Ensaio"),
                CreateInstrument("Percussão", "Pandeiro #1", "RMV", null, Core.Enums.InstrumentCondition.Excellent, "Sala de Ensaio"),
            };

            dbContext.Instruments.AddRange(instruments);
            await dbContext.SaveChangesAsync();
        }

        // Seed Sample Products
        if (!await dbContext.Products.AnyAsync())
        {
            var products = new[]
            {
                CreateProduct("Álbum RTUB 2023", "Album", 10.00m, 50, "Álbum de músicas da RTUB 2023", isPublic: true),
                CreateProduct("Álbum RTUB 2022", "Album", 10.00m, 30, "Álbum de músicas da RTUB 2022", isPublic: true),
                CreateProduct("Álbum RTUB 2021", "Album", 8.00m, 20, "Álbum de músicas da RTUB 2021", isPublic: true),
                CreateProduct("Álbum RTUB 2020", "Album", 8.00m, 15, "Álbum de músicas da RTUB 2020", isPublic: true),
                CreateProduct("Pin RTUB Logo", "Pin", 3.00m, 100, "Pin com logotipo da RTUB", isPublic: true),
                CreateProduct("Pin Guitarra", "Pin", 3.00m, 80, "Pin em forma de guitarra", isPublic: true),
                CreateProduct("T-Shirt RTUB", "Tshirt", 15.00m, 40, "T-Shirt oficial da RTUB", isPublic: false),
                CreateProduct("Hoodie RTUB", "Hoodie", 30.00m, 20, "Hoodie oficial da RTUB - Membros apenas", isPublic: false),
            };

            dbContext.Products.AddRange(products);
            await dbContext.SaveChangesAsync();
        }
    }

    private static Instrument CreateInstrument(string category, string name, string? brand, string? serialNumber, 
                                               Core.Enums.InstrumentCondition condition, string? location)
    {
        var instrument = Instrument.Create(category, name, condition);
        instrument.Update(name, condition, serialNumber, brand, location);
        if (brand != null)
        {
            instrument.SetPurchaseInfo(DateTime.UtcNow.AddYears(-2), 500.00m);
        }
        return instrument;
    }

    private static Product CreateProduct(string name, string type, decimal price, int stock, string? description, bool isPublic = true)
    {
        var product = Product.Create(name, type, price, stock);
        product.Update(name, type, price, stock, description);
        product.SetPublicVisibility(isPublic);
        return product;
    }
}
