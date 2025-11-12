using Microsoft.AspNetCore.Identity;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using System.Globalization;
using System.Text;

namespace RTUB.Application.Data.Builders;

// ---------- Builder ----------
public sealed class MemberBuilder
{
    private readonly UserManager<ApplicationUser> _userManager;

    private string _nickname = "";
    private string _firstName = "";
    private string _lastName = "";
    private string? _role;
    private string _phone = "";
    private string? _degree;
    private InstrumentType? _instrument;
    private readonly List<Position> _positions = new();
    private readonly List<MemberCategory> _categories = new();
    private int? _yearTuno;
    private int? _yearCaloiro;
    private int? _yearLeitao;
    private string? _mentorId;
    private string _password = "Rtub123!";

    public MemberBuilder(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public MemberBuilder Nickname(string nickname) { _nickname = nickname; return this; }
    public MemberBuilder Name(string first, string last) { _firstName = first; _lastName = last; return this; }
    public MemberBuilder Role(string role) { _role = role; return this; }
    public MemberBuilder Phone(string phone) { _phone = phone; return this; }
    public MemberBuilder Degree(string? degree) { _degree = degree; return this; }
    public MemberBuilder Instrument(InstrumentType? inst) { _instrument = inst; return this; }
    public MemberBuilder Position(Position pos) { _positions.Add(pos); return this; }
    public MemberBuilder Category(MemberCategory cat) { _categories.Add(cat); return this; }
    public MemberBuilder YearTuno(int year) { _yearTuno = year; return this; }
    public MemberBuilder YearCaloiro(int year) { _yearCaloiro = year; return this; }
    public MemberBuilder YearLeitao(int year) { _yearLeitao = year; return this; }
    public MemberBuilder Mentor(string mentorId) { _mentorId = mentorId; return this; }
    public MemberBuilder Password(string password) { _password = password; return this; }

    public async Task<ApplicationUser?> CreateAsync()
    {
        var email = EmailFromNickname(_nickname);
        return await CreateSampleUser(
            _userManager,
            email,
            _password,
            _role,
            _firstName,
            _lastName,
            _nickname,
            _phone,
            _degree,
            _instrument,
            _positions,
            _categories,
            _yearTuno,
            _yearCaloiro,
            _yearLeitao,
            _mentorId
        );
    }

    private static string EmailFromNickname(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname)) return "unknown@rtub.pt";

        string decomp = nickname.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomp.Length);
        foreach (var ch in decomp)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc == UnicodeCategory.NonSpacingMark) continue;

            char c = char.ToLowerInvariant(ch);
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
        }

        return $"{sb}@rtub.pt";
    }

    // ---------- Core creator (returns the user so you can get user.Id) ----------
    private static async Task<ApplicationUser?> CreateSampleUser(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string? role,
        string firstName,
        string lastName,
        string nickname,
        string phone,
        string? degree,
        InstrumentType? instrument,
        List<Position>? positions,
        List<MemberCategory>? categories,
        int? yearTuno = null,
        int? yearCaloiro = null,
        int? yearLeitao = null,
        string? mentorId = null)
    {
        var username = UsernameFromNickname(nickname);
        var user = await userManager.FindByNameAsync(username);

        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = username,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                Nickname = nickname,
                PhoneContact = phone,
                Degree = degree,
                MainInstrument = instrument,
                YearTuno = yearTuno,
                YearCaloiro = yearCaloiro,
                YearLeitao = yearLeitao,
                PhoneNumber = new Random().Next(900000000, 999999999).ToString(),
                Subscribed = false,
                Positions = positions ?? new List<Position>(),
                Categories = categories ?? new List<MemberCategory>(),
                MentorId = mentorId
            };

            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded && !string.IsNullOrEmpty(role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
            else if (!result.Succeeded)
            {
                // creation failed – return null so caller can skip
                return null;
            }
        }
        else
        {
            user.Email = email;
            user.FirstName = firstName;
            user.LastName = lastName;
            user.Nickname = nickname;
            user.PhoneContact = phone;
            user.Degree = degree;
            user.Positions = positions ?? new List<Position>();
            user.Categories = categories ?? new List<MemberCategory>();
            user.YearTuno = yearTuno;
            user.YearCaloiro = yearCaloiro;
            user.YearLeitao = yearLeitao;
            user.MainInstrument = instrument;

            await userManager.UpdateAsync(user);

            // ensure role is set if it changed
            if (!string.IsNullOrEmpty(role) && !(await userManager.IsInRoleAsync(user, role)))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }

        return user;
    }

    private static string UsernameFromNickname(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname)) return "unknown";
        string decomp = nickname.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomp.Length);
        foreach (var ch in decomp)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc == UnicodeCategory.NonSpacingMark) continue;

            char c = char.ToLowerInvariant(ch);
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
        }

        return sb.ToString();
    }
}