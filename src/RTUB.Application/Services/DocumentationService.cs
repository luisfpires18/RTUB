using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Application.Utilities;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service implementation for Documentation feature
/// Handles business logic for folders and documents with visibility control
/// Note: Uses ApplicationDbContext directly for user queries (pragmatic tradeoff similar to MeetingService)
/// </summary>
public class DocumentationService : IDocumentationService
{
    private readonly IFolderRepository _folderRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly ApplicationDbContext _context;

    public DocumentationService(
        IFolderRepository folderRepository,
        IDocumentRepository documentRepository,
        ApplicationDbContext context)
    {
        _folderRepository = folderRepository;
        _documentRepository = documentRepository;
        _context = context;
    }

    public async Task<IEnumerable<Folder>> GetVisibleFoldersAsync(ApplicationUser user, bool isAdmin, bool isMod)
    {
        // Admins and moderators can see all folders
        if (isAdmin || isMod)
        {
            return await _folderRepository.GetAllAsync();
        }

        var allFolders = await _folderRepository.GetAllAsync();
        var visibleFolders = new List<Folder>();

        foreach (var folder in allFolders)
        {
            if (await CanUserAccessFolderInternalAsync(folder, user, isAdmin))
            {
                visibleFolders.Add(folder);
            }
        }

        return visibleFolders;
    }

    public async Task<IEnumerable<Document>> GetDocumentsByFolderIdAsync(int folderId, ApplicationUser user, bool isAdmin)
    {
        // Check if user can access the folder
        if (!await CanUserAccessFolderAsync(folderId, user, isAdmin))
        {
            return Enumerable.Empty<Document>();
        }

        return await _documentRepository.GetByFolderIdAsync(folderId);
    }

    public async Task<Folder> CreateFolderAsync(
        string displayName,
        bool isSpecial = false,
        SpecialVisibility? specialVisibility = null,
        string? createdByUserId = null,
        string? createdByUserName = null)
    {
        // Generate normalized key
        var normalizedKey = S3KeyNormalizer.NormalizeForS3Key(displayName);

        // Handle collision detection
        normalizedKey = await EnsureUniqueNormalizedKeyAsync(normalizedKey);

        // Create folder using factory method
        Folder folder;
        if (isSpecial && specialVisibility.HasValue)
        {
            folder = Folder.CreateSpecial(displayName, normalizedKey, specialVisibility.Value, createdByUserId, createdByUserName);
        }
        else
        {
            folder = Folder.Create(displayName, normalizedKey, createdByUserId, createdByUserName);
        }

        return await _folderRepository.AddAsync(folder);
    }

    public async Task<Document> CreateDocumentAsync(
        int folderId,
        string displayName,
        string cloudflareUrl,
        string objectKey,
        long sizeBytes,
        string? contentType = null,
        string? createdByUserId = null,
        string? createdByUserName = null)
    {
        // Verify folder exists
        var folder = await _folderRepository.GetByIdAsync(folderId);
        if (folder == null)
        {
            throw new InvalidOperationException($"Pasta com ID {folderId} não encontrada");
        }

        // Create document using factory method
        var document = Document.Create(
            folderId,
            displayName,
            cloudflareUrl,
            objectKey,
            sizeBytes,
            contentType,
            createdByUserId,
            createdByUserName);

        return await _documentRepository.AddAsync(document);
    }

    public async Task DeleteFolderAsync(int folderId)
    {
        var folder = await _folderRepository.GetByIdAsync(folderId);
        if (folder == null)
        {
            throw new InvalidOperationException($"Pasta com ID {folderId} não encontrada");
        }

        // Delete all documents in the folder first
        var documents = await _documentRepository.GetByFolderIdAsync(folderId);
        foreach (var document in documents)
        {
            await _documentRepository.DeleteAsync(document.Id);
        }

        // Delete the folder
        await _folderRepository.DeleteAsync(folderId);
    }

    public async Task DeleteDocumentAsync(int documentId)
    {
        var document = await _documentRepository.GetByIdAsync(documentId);
        if (document == null)
        {
            throw new InvalidOperationException($"Documento com ID {documentId} não encontrado");
        }

        await _documentRepository.DeleteAsync(documentId);
    }

    public async Task<IEnumerable<ApplicationUser>> GetFolderViewersAsync(int folderId)
    {
        var folder = await _folderRepository.GetByIdAsync(folderId);
        if (folder == null)
        {
            return Enumerable.Empty<ApplicationUser>();
        }

        var viewerUserIds = folder.FolderViewers.Select(fv => fv.UserId).ToList();

        return await _context.Users
            .AsNoTracking()
            .Where(u => viewerUserIds.Contains(u.Id))
            .ToListAsync();
    }

    public async Task<bool> CanUserAccessFolderAsync(int folderId, ApplicationUser user, bool isAdmin)
    {
        var folder = await _folderRepository.GetByIdAsync(folderId);
        if (folder == null)
        {
            return false;
        }

        return await CanUserAccessFolderInternalAsync(folder, user, isAdmin);
    }

    /// <summary>
    /// Internal method to check folder access without fetching the folder again
    /// </summary>
    private async Task<bool> CanUserAccessFolderInternalAsync(Folder folder, ApplicationUser user, bool isAdmin)
    {
        // Admins can access everything
        if (isAdmin)
        {
            return true;
        }

        // Non-special folders are visible to all
        if (!folder.IsSpecial)
        {
            return true;
        }

        // Check if user has explicit viewer permission
        if (folder.FolderViewers.Any(fv => fv.UserId == user.Id))
        {
            return true;
        }

        // Apply special visibility rules
        return folder.SpecialVisibility switch
        {
            SpecialVisibility.None => true,
            
            // Veteranos: VETERANO + TUNOSSAURO roles + Magister position
            SpecialVisibility.Veteranos => 
                user.IsVeterano() || 
                user.IsTunossauro() || 
                (user.Positions != null && user.Positions.Contains(Position.Magister)),
            
            // Direção: Only Direção members (Magister, Vice-Magister, Secretario, Tesoureiros)
            SpecialVisibility.Direcao => 
                user.Positions != null && (
                    user.Positions.Contains(Position.Magister) ||
                    user.Positions.Contains(Position.ViceMagister) ||
                    user.Positions.Contains(Position.Secretario) ||
                    user.Positions.Contains(Position.PrimeiroTesoureiro) ||
                    user.Positions.Contains(Position.SegundoTesoureiro)),
            
            // Assembleia Geral: All except Leitão
            SpecialVisibility.AssembleiaGeral => !user.IsLeitao(),
            
            // Conselho Fiscal: Only CF members
            SpecialVisibility.ConselhoFiscal => 
                user.Positions != null && (
                    user.Positions.Contains(Position.PresidenteConselhoFiscal) ||
                    user.Positions.Contains(Position.PrimeiroRelatorConselhoFiscal) ||
                    user.Positions.Contains(Position.SegundoRelatorConselhoFiscal)),
            
            // Tesouraria: Only Tesoureiros
            SpecialVisibility.Tesouraria => 
                user.Positions != null && (
                    user.Positions.Contains(Position.PrimeiroTesoureiro) ||
                    user.Positions.Contains(Position.SegundoTesoureiro)),
            
            _ => false
        };
    }

    /// <summary>
    /// Ensures the normalized key is unique by appending _2, _3, etc. if needed
    /// </summary>
    private async Task<string> EnsureUniqueNormalizedKeyAsync(string normalizedKey)
    {
        var baseKey = normalizedKey;
        var counter = 2;

        while (await _folderRepository.ExistsAsync(normalizedKey))
        {
            normalizedKey = $"{baseKey}_{counter}";
            counter++;
        }

        return normalizedKey;
    }
}
