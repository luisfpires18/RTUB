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
/// Seeds the database with albums and songs.
/// </summary>
public static partial class SeedData
{
    public static async Task SeedMusicAsync(ApplicationDbContext dbContext)
    {
        // Seed music albums and songs
        if (!await dbContext.Albums.AnyAsync())
        {
            var album1 = Album.Create("Tunalidades", 1995);
            var album2 = Album.Create("50% Música 51% Álcool", 1998, "Edição de 2000");
            var album3 = Album.Create("Boémios e Trovadores", 2007);
            var album4 = Album.Create("É Esta a Tuna", 2013);

            await dbContext.Albums.AddRangeAsync(new[] { album1, album2, album3, album4 });
            await dbContext.SaveChangesAsync();

            // Add songs for Album 1: Tunalidades 1995
            var album1Songs = new[]
            {
                Song.Create("Apresentança", album1.Id, 1),
                Song.Create("Boémio de Bragança", album1.Id, 2),
                Song.Create("À Entrada do Café", album1.Id, 3),
                Song.Create("Maria se Fores ao Baile", album1.Id, 4),
                Song.Create("Tuna Mix", album1.Id, 5),
                Song.Create("Fado do Peido", album1.Id, 6),
                Song.Create("Tunos Naturais de Bragança", album1.Id, 7),
                Song.Create("Manuel Ceguinho", album1.Id, 8),
                Song.Create("Sob a Janela", album1.Id, 9),
                Song.Create("Aventura Sexual de Levi", album1.Id, 10),
                Song.Create("Esperança de Cá Voltar", album1.Id, 11)
            };

            // Add songs for Album 2: 50% Música 51% Álcool
            var album2Songs = new[]
            {
                Song.Create("Cânticos Gregorianos", album2.Id, 1),
                Song.Create("Vai mais um Copo", album2.Id, 2),
                Song.Create("Serenata à Janela (Sob a Janela)", album2.Id, 3),
                Song.Create("Clavelitos", album2.Id, 4),
                Song.Create("Boémio de Bragança", album2.Id, 5),
                Song.Create("Alma Corazon e Vida", album2.Id, 6),
                Song.Create("Improviso de Mi por Si Sem DÓ ao Sol#", album2.Id, 7),
                Song.Create("Pões-me à Noite a Sonhar", album2.Id, 8),
                Song.Create("A Morte pode Dançar", album2.Id, 9),
                Song.Create("Pasando por el Parque", album2.Id, 10),
                Song.Create("Noite Memorável", album2.Id, 11),
                Song.Create("Sombras desta Cidade", album2.Id, 12),
                Song.Create("Hino 69", album2.Id, 13),
                Song.Create("À Entrada do Café", album2.Id, 14),
                Song.Create("Vim Parar ao IPB", album2.Id, 15),
                Song.Create("Festa :-)", album2.Id, 16)
            };

            // Add songs for Album 3: Boémios e Trovadores
            var album3Songs = new[]
            {
                Song.Create("Noites Presentes", album3.Id, 1),
                Song.Create("Caçador de Raposas", album3.Id, 2),
                Song.Create("Procissões", album3.Id, 3),
                Song.Create("Ver-te ao Luar", album3.Id, 4),
                Song.Create("Manel Ceguinho", album3.Id, 5),
                Song.Create("O Sonho", album3.Id, 6),
                Song.Create("Manhãs Ausentes", album3.Id, 7),
                Song.Create("Malefício do Copo", album3.Id, 8),
                Song.Create("Arre Burra", album3.Id, 9),
                Song.Create("Acredita podes Crer", album3.Id, 10),
                Song.Create("Venezuela", album3.Id, 11),
                Song.Create("Saudade da Tuna", album3.Id, 12),
                Song.Create("Há Esperança de cá Voltar", album3.Id, 13),
                Song.Create("Fado do até Sempre", album3.Id, 14),
                Song.Create("La Çarandieira", album3.Id, 15)
            };

            // Add songs for Album 4: É Esta a Tuna
            var album4Songs = new[]
            {
                Song.Create("É Esta a Tuna", album4.Id, 1),
                Song.Create("Coração de Papelão", album4.Id, 2),
                Song.Create("Miragem", album4.Id, 3),
                Song.Create("Favaios", album4.Id, 4),
                Song.Create("Bragança", album4.Id, 5),
                Song.Create("Deixa-me Ser um Pouco Mais", album4.Id, 6),
                Song.Create("Histórias", album4.Id, 7),
                Song.Create("Cor de Mar", album4.Id, 8),
                Song.Create("Des-serenata (Canção do Desengate)", album4.Id, 9),
                Song.Create("Boémio de Bragança", album4.Id, 10),
                Song.Create("Eu Vim Parar ao IPB", album4.Id, 11)
            };

            await dbContext.Songs.AddRangeAsync(album1Songs);
            await dbContext.Songs.AddRangeAsync(album2Songs);
            await dbContext.Songs.AddRangeAsync(album3Songs);
            await dbContext.Songs.AddRangeAsync(album4Songs);
            await dbContext.SaveChangesAsync();
        }
    }
}