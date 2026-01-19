using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for synchronizing default group conversations
/// Ensures that default system groups exist and have correct participants
/// </summary>
public class GroupConversationSyncService : IGroupConversationSyncService
{
    private readonly IMessagingService _messagingService;
    private readonly IConversationRepository _conversationRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<GroupConversationSyncService> _logger;

    public GroupConversationSyncService(
        IMessagingService messagingService,
        IConversationRepository conversationRepository,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ILogger<GroupConversationSyncService> logger)
    {
        _messagingService = messagingService;
        _conversationRepository = conversationRepository;
        _userManager = userManager;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SyncDefaultGroupsAsync()
    {
        try
        {
            // Get current fiscal year to determine the right group name
            var currentFiscalYear = await GetCurrentFiscalYearAsync();

            // 1. ORGANIZAÇÃO (Fiscal Year) - Members with positions in Orgãos Sociais
            await SyncOrganizacaoGroupAsync(currentFiscalYear);

            // 2. TUNOSSAUROS - Users with CurrentRole == "TUNOSSAURO"
            await SyncRoleBasedGroupAsync("TUNOSSAUROS", "TUNOSSAURO");

            // 3. VETERANOS - Users with CurrentRole == "VETERANO"
            await SyncRoleBasedGroupAsync("VETERANOS", "VETERANO");

            // 4. TUNOS - Users whose Categories contains MemberCategory.Tuno
            await SyncCategoryBasedGroupAsync("TUNOS", new[] { MemberCategory.Tuno }, excludeTunoHonorario: true);

            // 5. LEITÕES & CALOIROS - Users whose Categories contains Leitao or Caloiro
            await SyncCategoryBasedGroupAsync("LEITÕES & CALOIROS", new[] { MemberCategory.Leitao, MemberCategory.Caloiro });

            // 6. ANUNCIOS - All active (non-retired) members, announcement-only channel
            await SyncAnunciosGroupAsync();

            // 7. NO ATIVO - All active (non-retired) members + Owner, normal chat
            await SyncNoAtivoGroupAsync();

            // 8. GERAL - All members, chat
            await SyncGeneralGroupAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during default group conversations sync");
            throw;
        }
    }

    private async Task<string> GetCurrentFiscalYearAsync()
    {
        var now = DateTime.UtcNow;
        // Fiscal year typically runs from September to August
        var fiscalYearStart = now.Month >= 9 ? now.Year : now.Year - 1;
        var fiscalYearEnd = fiscalYearStart + 1;
        return $"{fiscalYearStart}/{fiscalYearEnd}";
    }

    private async Task SyncOrganizacaoGroupAsync(string fiscalYear)
    {
        var groupTitle = $"ORGANIZAÇÃO ({fiscalYear})";

        // Get current fiscal year bounds
        var now = DateTime.UtcNow;
        var startYear = now.Month >= 9 ? now.Year : now.Year - 1;
        var endYear = startYear + 1;

        // Get all users with positions in the current fiscal year
        var roleAssignments = await _dbContext.RoleAssignments
            .Include(ra => ra.User)
            .Where(ra => ra.StartYear == startYear && ra.EndYear == endYear)
            .ToListAsync();

        var participantIds = roleAssignments
            .Where(ra => ra.User != null)
            .Select(ra => ra.UserId)
            .Distinct()
            .ToList();

        await CreateOrUpdateSystemGroupAsync(groupTitle, participantIds);
    }

    private async Task SyncRoleBasedGroupAsync(string groupTitle, string targetRole)
    {
        // Get all users and filter by CurrentRole property
        var allUsers = await _userManager.Users.ToListAsync();

        List<string> participantIds;

        if (targetRole == "VETERANO")
        {
            // VETERANOS group should include both VETERANO and TUNOSSAURO users
            participantIds = allUsers
                .Where(u => u.CurrentRole == "VETERANO" || u.CurrentRole == "TUNOSSAURO")
                .Select(u => u.Id)
                .ToList();
        }
        else
        {
            participantIds = allUsers
                .Where(u => u.CurrentRole == targetRole)
                .Select(u => u.Id)
                .ToList();
        }

        await CreateOrUpdateSystemGroupAsync(groupTitle, participantIds);
    }

    private async Task SyncCategoryBasedGroupAsync(string groupTitle, MemberCategory[] targetCategories, bool excludeTunoHonorario = false)
    {
        // Get all users
        var allUsers = await _userManager.Users.ToListAsync();

        var participantIds = allUsers
            .Where(u => u.Categories.Any(c => targetCategories.Contains(c)) &&
                        (!excludeTunoHonorario || !u.Categories.Contains(MemberCategory.TunoHonorario)))
            .Select(u => u.Id)
            .ToList();

        await CreateOrUpdateSystemGroupAsync(groupTitle, participantIds);
    }

    private async Task CreateOrUpdateSystemGroupAsync(string groupTitle, List<string> participantIds)
    {
        if (participantIds.Count == 0)
        {
            _logger.LogInformation("No participants found for group {GroupTitle}, skipping", groupTitle);
            return;
        }

        var existingGroup = await _conversationRepository.GetGroupByTitleAsync(groupTitle);

        if (existingGroup == null)
        {
            // Create new group
            await _messagingService.GetOrCreateSystemGroupAsync(groupTitle, participantIds);
            _logger.LogInformation("Created system group {GroupTitle} with {ParticipantCount} participants",
                groupTitle, participantIds.Count);
        }
        else
        {
            // Update participants if changed
            var currentParticipants = existingGroup.GetParticipantIds();
            var hasChanges = !currentParticipants.OrderBy(x => x).SequenceEqual(participantIds.OrderBy(x => x));

            if (hasChanges)
            {
                await _messagingService.UpdateGroupParticipantsAsync(existingGroup.Id, participantIds);
                _logger.LogInformation("Updated system group {GroupTitle}: {OldCount} -> {NewCount} participants",
                    groupTitle, currentParticipants.Count, participantIds.Count);
            }
            else
            {
                _logger.LogDebug("System group {GroupTitle} is up to date with {ParticipantCount} participants",
                    groupTitle, participantIds.Count);
            }
        }
    }

    private async Task SyncGeneralGroupAsync()
    {
        const string groupTitle = "GERAL";

        var participantIds = await GetAllUserIdsAsync();
        await CreateOrUpdateSystemGroupAsync(groupTitle, participantIds);
    }

    private async Task SyncAnunciosGroupAsync()
    {
        const string groupTitle = "ANUNCIOS";

        var participantIds = await GetActiveUserIdsAsync();
        await CreateOrUpdateAnnouncementGroupAsync(groupTitle, participantIds);
    }

    private async Task SyncNoAtivoGroupAsync()
    {
        const string groupTitle = "NO ATIVO";

        // Get all active (non-retired) users
        var participantIds = await GetActiveUserIdsAsync();

        // Add Owner(s) to the group
        var owners = await _userManager.GetUsersInRoleAsync("Owner");
        foreach (var owner in owners)
        {
            if (!participantIds.Contains(owner.Id))
            {
                participantIds.Add(owner.Id);
            }
        }

        await CreateOrUpdateSystemGroupAsync(groupTitle, participantIds);
    }

    private async Task<List<string>> GetActiveUserIdsAsync()
    {
        var allUsers = await _userManager.Users.ToListAsync();
        return allUsers
            .Where(u => !u.IsRetired)
            .Select(u => u.Id)
            .ToList();
    }

    private async Task<List<string>> GetAllUserIdsAsync()
    {
        var allUsers = await _userManager.Users.ToListAsync();
        return allUsers
            .Select(u => u.Id)
            .ToList();
    }

    private async Task CreateOrUpdateAnnouncementGroupAsync(string groupTitle, List<string> participantIds)
    {
        if (participantIds.Count == 0)
        {
            _logger.LogInformation("No participants found for announcement group {GroupTitle}, skipping", groupTitle);
            return;
        }

        var existingGroup = await _conversationRepository.GetGroupByTitleAsync(groupTitle);

        if (existingGroup == null)
        {
            // Create new announcement group
            await _messagingService.GetOrCreateAnnouncementGroupAsync(groupTitle, participantIds);
            _logger.LogInformation("Created announcement group {GroupTitle} with {ParticipantCount} participants",
                groupTitle, participantIds.Count);
        }
        else
        {
            // Update participants if changed
            var currentParticipants = existingGroup.GetParticipantIds();
            var hasChanges = !currentParticipants.OrderBy(x => x).SequenceEqual(participantIds.OrderBy(x => x));

            if (hasChanges)
            {
                await _messagingService.UpdateGroupParticipantsAsync(existingGroup.Id, participantIds);
                _logger.LogInformation("Updated announcement group {GroupTitle}: {OldCount} -> {NewCount} participants",
                    groupTitle, currentParticipants.Count, participantIds.Count);
            }
            else
            {
                _logger.LogDebug("Announcement group {GroupTitle} is up to date with {ParticipantCount} participants",
                    groupTitle, participantIds.Count);
            }
        }
    }
}
