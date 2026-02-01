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
        string fiscalYear,
        string environment,
        bool isSpecial = false,
        SpecialVisibility? specialVisibility = null,
        string? createdByUserId = null,
        string? createdByUserName = null)
    {
        // Normalize only the folder name part
        var normalizedFolderName = S3KeyNormalizer.NormalizeForS3Key(displayName);
        
        // Construct full path for NormalizedKey
        var fullPath = $"docs/{environment}/{fiscalYear}/{normalizedFolderName}";
        
        // Handle collision detection with full path
        fullPath = await EnsureUniqueNormalizedKeyAsync(fullPath);

        // Create folder using factory method
        Folder folder;
        if (isSpecial && specialVisibility.HasValue)
        {
            folder = Folder.CreateSpecial(displayName, fullPath, specialVisibility.Value, createdByUserId, createdByUserName);
        }
        else
        {
            folder = Folder.Create(displayName, fullPath, createdByUserId, createdByUserName);
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

        var viewers = new List<ApplicationUser>();

        // Get users based on special visibility rules
        if (folder.IsSpecial && folder.SpecialVisibility.HasValue)
        {
            IQueryable<ApplicationUser> query = _context.Users.AsNoTracking();

            switch (folder.SpecialVisibility.Value)
            {
                case SpecialVisibility.Veteranos:
                    // Get users with CurrentRole = "VETERANO" or "TUNOSSAURO" or Position = Magister
                    query = query.Where(u => 
                        u.CurrentRole == "VETERANO" || 
                        u.CurrentRole == "TUNOSSAURO" || 
                        (u.Positions != null && u.Positions.Contains(Position.Magister)));
                    break;

                case SpecialVisibility.Direcao:
                    // Get users with Direção positions
                    query = query.Where(u => 
                        u.Positions != null && (
                            u.Positions.Contains(Position.Magister) ||
                            u.Positions.Contains(Position.ViceMagister) ||
                            u.Positions.Contains(Position.Secretario) ||
                            u.Positions.Contains(Position.PrimeiroTesoureiro) ||
                            u.Positions.Contains(Position.SegundoTesoureiro)));
                    break;

                case SpecialVisibility.AssembleiaGeral:
                    // Get all users except those where IsLeitao() = true
                    // IsLeitao checks if Categories contains MemberCategory.Leitao
                    query = query.Where(u => !u.Categories.Contains(MemberCategory.Leitao));
                    break;

                case SpecialVisibility.ConselhoFiscal:
                    // Get users with CF positions
                    query = query.Where(u => 
                        u.Positions != null && (
                            u.Positions.Contains(Position.PresidenteConselhoFiscal) ||
                            u.Positions.Contains(Position.PrimeiroRelatorConselhoFiscal) ||
                            u.Positions.Contains(Position.SegundoRelatorConselhoFiscal)));
                    break;

                case SpecialVisibility.Tesouraria:
                    // Get users with Tesoureiro positions
                    query = query.Where(u => 
                        u.Positions != null && (
                            u.Positions.Contains(Position.PrimeiroTesoureiro) ||
                            u.Positions.Contains(Position.SegundoTesoureiro)));
                    break;

                case SpecialVisibility.None:
                default:
                    // Return all users for None/null
                    query = _context.Users.AsNoTracking();
                    break;
            }

            viewers = await query.ToListAsync();
        }
        else
        {
            // Non-special folders: Return all users
            viewers = await _context.Users.AsNoTracking().ToListAsync();
        }

        // Also include explicit FolderViewers from the FolderViewer table
        var explicitViewerIds = folder.FolderViewers.Select(fv => fv.UserId).ToList();
        if (explicitViewerIds.Any())
        {
            var explicitViewers = await _context.Users
                .AsNoTracking()
                .Where(u => explicitViewerIds.Contains(u.Id))
                .ToListAsync();

            // Merge and deduplicate (use union to avoid duplicates)
            viewers = viewers.Union(explicitViewers).ToList();
        }

        return viewers;
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
