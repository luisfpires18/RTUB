// Persona: test class moved here from MeetingsApiTests.cs.
using RTUB.Core.Entities;

namespace RTUB.Integration.Tests.Api;
/// <summary>A signed-in member, with the antiforgery header already set, and the account behind it.</summary>
internal sealed record Persona(HttpClient Client, ApplicationUser User)
{
    public string Id => User.Id;
}
