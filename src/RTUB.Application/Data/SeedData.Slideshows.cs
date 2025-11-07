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
            // Note: Seed data will need images to be uploaded via the admin interface
            // These are placeholder slideshows without images
            var slideshow1 = Slideshow.Create("RTUB", 1, "Bem-vindo à Real Tuna Universitária de Bragança. A preservar o espírito e a tradição académica desde 1991.", 5000);
            slideshow1.SetImage(""); // Will need to upload image via admin
            slideshow1.Deactivate(); // Deactivate until image is uploaded

            var slideshow2 = Slideshow.Create("O nosso Festival", 2, "Somos os orgulhosos organizadores do FITAB – Festival Internacional de Tunas Académicas de Bragança, um marco na cultura da cidade.", 5000);
            slideshow2.SetImage(""); // Will need to upload image via admin
            slideshow2.Deactivate(); // Deactivate until image is uploaded

            var slideshow3 = Slideshow.Create("Amigos Para Sempre", 3, "Mais que uma tuna, uma família. Cantamos a boémia, a amizade e a saudade, unidos pela música e pela \"Terra dos amigos para sempre\".", 5000);
            slideshow3.SetImage(""); // Will need to upload image via admin
            slideshow3.Deactivate(); // Deactivate until image is uploaded

            await dbContext.Slideshows.AddRangeAsync(new[] { slideshow1, slideshow2, slideshow3 });
            await dbContext.SaveChangesAsync();
        }
    }
}