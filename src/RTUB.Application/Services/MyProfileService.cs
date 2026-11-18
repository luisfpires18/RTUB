using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// The React /profile (docs/react-member-area.md). The member edits
/// only their own profile, with the rules enforced here:
/// - Pessoal: names, nickname, email, phone, birth date, city, degree, with the entity's own checks; a member who is
///   only a Leitão cannot change the nickname (it comes at Caloiro); an email another account uses is refused;
/// - Tuna: the padrinho (a Tuno or above, never yourself; none for a Leitão, a Fundador or a Tuno Honorário) and the
///   Leitão / Caloiro / Tuno dates (none for a Fundador); a complete date pair is locked (the Magister corrects it);
/// - instruments one by one (the first is the primary; removing the primary promotes the next);
/// - the photo: WebP, JPEG or PNG up to 10 MB, stored by <see cref="IUserProfileService"/> (the previous photo
///   is deleted, the new one goes to the member's profile folder);
/// - email notifications on / off; the password (at least 8 characters, confirmed), which clears
///   RequirePasswordChange.
/// Push notifications are not here (the Push v2 task). No schema change.
/// </summary>
public sealed partial class MyProfileService : IMyProfileService
{
    public const long MaxPhotoBytes = 10 * 1024 * 1024;
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 100;
    public const int MentorLimit = 20;
    public const string LockedDateMessage = "Não podes alterar as datas. Se estão em erro, contacta o Magister.";

    private static readonly string[] PhotoTypes = { "image/webp", "image/jpeg", "image/png" };

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IMemberDirectoryService _directory;
    private readonly IMemberInstrumentService _instruments;
    private readonly IMemberMentorService _mentors;
    private readonly IRankingService _ranking;
    private readonly IUserProfileService _profiles;
    private readonly IOptions<RankingConfiguration> _rankingConfig;

    public MyProfileService(
        IDbContextFactory<ApplicationDbContext> contexts,
        UserManager<ApplicationUser> users,
        IMemberDirectoryService directory,
        IMemberInstrumentService instruments,
        IMemberMentorService mentors,
        IRankingService ranking,
        IUserProfileService profiles,
        IOptions<RankingConfiguration> rankingConfig)
    {
        _contexts = contexts;
        _users = users;
        _directory = directory;
        _instruments = instruments;
        _mentors = mentors;
        _ranking = ranking;
        _profiles = profiles;
        _rankingConfig = rankingConfig;
    }
}
