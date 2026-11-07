namespace RTUB.Application.Interfaces;

/// <summary>
/// What the send path needs from the push preferences (task 082): the accounts that turned a key off. The mandatory
/// key answers an empty set. Implemented by <see cref="Services.PushPreferenceService"/>.
/// </summary>
public interface IPushPreferenceStore
{
    Task<IReadOnlySet<string>> MutedUserIdsAsync(string key);
}
