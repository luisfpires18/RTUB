using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

/// <summary>
/// Seeds the database with slideshows for homepage.
/// </summary>
public static partial class SeedData
{
    public static async Task SeedSlideshowsAsync(ApplicationDbContext dbContext)
    {
        // Seed slideshow data
        if (!await dbContext.Slideshows.AnyAsync())
        {
            // Create sample SVG images
            var svg1 = @"<svg width='800' height='440' xmlns='http://www.w3.org/2000/svg'><rect width='800' height='440' fill='#6f42c1'/><text x='400' y='220' font-family='Arial' font-size='48' fill='white' text-anchor='middle'>RTUB 1991</text></svg>";
            var svg2 = @"<svg width='800' height='440' xmlns='http://www.w3.org/2000/svg'><rect width='800' height='440' fill='#000000'/><text x='400' y='220' font-family='Arial' font-size='48' fill='#6f42c1' text-anchor='middle'>Tradição</text></svg>";
            var svg3 = @"<svg width='800' height='440' xmlns='http://www.w3.org/2000/svg'><rect width='800' height='440' fill='#2c2c2c'/><text x='400' y='220' font-family='Arial' font-size='48' fill='white' text-anchor='middle'>Eventos</text></svg>";

            var slideshow1 = Slideshow.Create("RTUB", 1, "Bem-vindo à Real Tuna Universitária de Bragança. A preservar o espírito e a tradição académica desde 1991.", 5000);
            // Images will need to be uploaded to R2 storage separately
            slideshow1.ImageUrl = "https://placeholder.com/slideshow1.jpg"; // TODO: Upload actual image to R2
            slideshow1.Activate();

            var slideshow2 = Slideshow.Create("O nosso Festival", 2, "Somos os orgulhosos organizadores do FITAB – Festival Internacional de Tunas Académicas de Bragança, um marco na cultura da cidade.", 5000);
            slideshow2.ImageUrl = "https://placeholder.com/slideshow2.jpg"; // TODO: Upload actual image to R2
            slideshow2.Activate();

            var slideshow3 = Slideshow.Create("Amigos Para Sempre", 3, "Mais que uma tuna, uma família. Cantamos a boémia, a amizade e a saudade, unidos pela música e pela \"Terra dos amigos para sempre\".", 5000);
            slideshow3.ImageUrl = "https://placeholder.com/slideshow3.jpg"; // TODO: Upload actual image to R2
            slideshow3.Activate();

            await dbContext.Slideshows.AddRangeAsync(new[] { slideshow1, slideshow2, slideshow3 });
            await dbContext.SaveChangesAsync();
        }
    }
}