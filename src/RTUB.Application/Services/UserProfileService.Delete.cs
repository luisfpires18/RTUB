// User profile: hard delete of a member with all related data.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace RTUB.Application.Services;

public partial class UserProfileService
{
    // User Role Management methods

    /// <summary>
    /// Deletes a member and all related entities that have FK constraints preventing direct deletion.
    /// Entities with Cascade/SetNull delete behaviors are handled automatically by the database.
    /// Entities with Restrict/NoAction/ClientSetNull must be manually cleaned up before user deletion.
    /// Uses a combination of repository pattern and direct DbContext access for comprehensive cleanup.
    /// </summary>
    public async Task<bool> DeleteMemberWithRelatedDataAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("Cannot delete member: User {UserName} not found", userId);
            return false;
        }

        try
        {
            // ===================================================================
            // Phase 1: Delete child entities of parents we'll delete later
            // (prevents FK violations in cascade chains)
            // ===================================================================

            await using var ctx = _contextFactory.CreateDbContext();

            // Delete GalleryMediaPersonTags for media uploaded by this user (before GalleryMedia)
            var uploadedMediaIds = await ctx.GalleryMedia
                .Where(gm => gm.UploaderId == userId)
                .Select(gm => gm.Id)
                .ToListAsync();
            if (uploadedMediaIds.Count > 0)
            {
                await ctx.GalleryMediaPersonTags
                    .Where(t => uploadedMediaIds.Contains(t.GalleryMediaId))
                    .ExecuteDeleteAsync();
            }

            // Delete NaipeComments and NaipePlayCounts for NaipeContents created by this user
            var userNaipeContentIds = await ctx.NaipeContents
                .Where(nc => nc.CreatedByUserId == userId)
                .Select(nc => nc.Id)
                .ToListAsync();
            if (userNaipeContentIds.Count > 0)
            {
                await ctx.NaipeComments
                    .Where(nc => userNaipeContentIds.Contains(nc.NaipeContentId))
                    .ExecuteDeleteAsync();
                await ctx.NaipePlayCounts
                    .Where(pc => userNaipeContentIds.Contains(pc.NaipeContentId))
                    .ExecuteDeleteAsync();
            }

            // Delete QuestionReplies for Questions authored by or assigned to this user
            var userQuestionIds = await ctx.Questions
                .Where(q => q.AuthorId == userId || q.AssignedMemberId == userId)
                .Select(q => q.Id)
                .ToListAsync();
            if (userQuestionIds.Count > 0)
            {
                await ctx.QuestionReplies
                    .Where(qr => userQuestionIds.Contains(qr.QuestionId))
                    .ExecuteDeleteAsync();
            }

            // Delete MeetingAta child entities for atas where PresidentUserId = userId
            var userPresidedAtaIds = await ctx.MeetingAtas
                .Where(ma => ma.PresidentUserId == userId)
                .Select(ma => ma.Id)
                .ToListAsync();
            if (userPresidedAtaIds.Count > 0)
            {
                await ctx.MeetingAtaAgendaPoints
                    .Where(ap => userPresidedAtaIds.Contains(ap.MeetingAtaId))
                    .ExecuteDeleteAsync();
                await ctx.MeetingAtaAttachments
                    .Where(a => userPresidedAtaIds.Contains(a.MeetingAtaId))
                    .ExecuteDeleteAsync();
                await ctx.MeetingAtaConfirmations
                    .Where(c => userPresidedAtaIds.Contains(c.MeetingAtaId))
                    .ExecuteDeleteAsync();
            }

            // ===================================================================
            // Phase 2: Delete/update direct Restrict FK references
            // ===================================================================

            // LeaderboardCommentLikes by UserId (likes on other users' comments)
            await ctx.LeaderboardCommentLikes
                .Where(l => l.UserId == userId)
                .ExecuteDeleteAsync();

            // LeaderboardComments where AuthorId or TargetUserId = userId
            var leaderboardComments = await _leaderboardCommentRepository.QueryAsync(q => q
                .Include(c => c.Likes)
                .Where(c => c.AuthorId == userId || c.TargetUserId == userId)
                .ToListAsync());
            foreach (var comment in leaderboardComments)
                await _leaderboardCommentRepository.DeleteAsync(comment);

            // Comments where AuthorId = userId
            var comments = await _commentRepository.QueryAsync(q => q
                .Where(c => c.AuthorId == userId)
                .ToListAsync());
            foreach (var comment in comments)
                await _commentRepository.DeleteAsync(comment);

            // Posts where AuthorId = userId
            var posts = await _postRepository.QueryAsync(q => q
                .Where(p => p.AuthorId == userId)
                .ToListAsync());
            foreach (var post in posts)
                await _postRepository.DeleteAsync(post);

            // EventVideos by CreatedByUserId
            await ctx.EventVideos
                .Where(ev => ev.CreatedByUserId == userId)
                .ExecuteDeleteAsync();

            // GalleryMediaPersonTags where UserId = this user (tags of this user in other media)
            await ctx.GalleryMediaPersonTags
                .Where(t => t.UserId == userId)
                .ExecuteDeleteAsync();

            // GalleryMedia by UploaderId (person tags already deleted in Phase 1)
            await ctx.GalleryMedia
                .Where(gm => gm.UploaderId == userId)
                .ExecuteDeleteAsync();

            // NaipeComments by AuthorId (comments this user made on any content)
            await ctx.NaipeComments
                .Where(nc => nc.AuthorId == userId)
                .ExecuteDeleteAsync();

            // NaipeContents by CreatedByUserId (children deleted in Phase 1)
            await ctx.NaipeContents
                .Where(nc => nc.CreatedByUserId == userId)
                .ExecuteDeleteAsync();

            // QuestionReplies by AuthorId
            await ctx.QuestionReplies
                .Where(qr => qr.AuthorId == userId)
                .ExecuteDeleteAsync();

            // Questions by AuthorId or AssignedMemberId (children deleted in Phase 1)
            await ctx.Questions
                .Where(q => q.AuthorId == userId || q.AssignedMemberId == userId)
                .ExecuteDeleteAsync();

            // SongVideos by CreatedByUserId
            await ctx.SongVideos
                .Where(sv => sv.CreatedByUserId == userId)
                .ExecuteDeleteAsync();

            // ===================================================================
            // Phase 3: Handle NoAction FK relationships (nullify or delete)
            // ===================================================================

            // Meeting - set all user references to null
            var meetings = await _meetingRepository.QueryAsync(q => q
                .Where(m => m.OrganizerUserId == userId
                         || m.TunoRepresentativeUserId == userId
                         || m.DelegatedAtaWriterMemberId == userId)
                .ToListAsync());
            foreach (var meeting in meetings)
            {
                if (meeting.OrganizerUserId == userId) meeting.OrganizerUserId = null;
                if (meeting.TunoRepresentativeUserId == userId) meeting.TunoRepresentativeUserId = null;
                if (meeting.DelegatedAtaWriterMemberId == userId) meeting.DelegatedAtaWriterMemberId = null;
                await _meetingRepository.UpdateAsync(meeting);
            }

            // MeetingRequests by AuthorUserId
            var meetingRequests = await _meetingRequestRepository.QueryAsync(q => q
                .Where(mr => mr.AuthorUserId == userId)
                .ToListAsync());
            foreach (var meetingRequest in meetingRequests)
                await _meetingRequestRepository.DeleteAsync(meetingRequest);

            // MeetingPollVotes by UserId (required FK with NoAction: blocks the user delete otherwise)
            await ctx.MeetingPollVotes
                .Where(vote => vote.UserId == userId)
                .ExecuteDeleteAsync();

            // MeetingAtas - delete where PresidentUserId = userId (required field, children deleted in Phase 1)
            await ctx.MeetingAtas
                .Where(ma => ma.PresidentUserId == userId)
                .ExecuteDeleteAsync();

            // MeetingAtas - set nullable secretary fields to null
            await ctx.MeetingAtas
                .Where(ma => ma.FirstSecretaryUserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(ma => ma.FirstSecretaryUserId, (string?)null));
            await ctx.MeetingAtas
                .Where(ma => ma.SecondSecretaryUserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(ma => ma.SecondSecretaryUserId, (string?)null));

            // ===================================================================
            // Phase 4: Handle ClientSetNull FK relationships
            // (EF Core only handles these for tracked entities, not at DB level)
            // ===================================================================

            // LogisticsCards - set AssignedToUserId to null
            await ctx.LogisticsCards
                .Where(lc => lc.AssignedToUserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(lc => lc.AssignedToUserId, (string?)null));

            // ===================================================================
            // Phase 5: Delete the user (Cascade/SetNull FKs handled by DB)
            // ===================================================================
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError("Failed to delete user {UserName}: {Errors}", user.UserName ?? userId, errors);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting member {UserName}", user.UserName ?? userId);
            return false;
        }
    }
}
