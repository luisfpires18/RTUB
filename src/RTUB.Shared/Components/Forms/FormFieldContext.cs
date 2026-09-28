namespace RTUB.Shared;

/// <summary>
/// What <see cref="FormField"/> hands to its control: the id its label points at and the ids of
/// the help and error text. <see cref="Attributes"/> carries both, ready for <c>@attributes</c>.
/// </summary>
public sealed record FormFieldContext(string Id, string? DescribedBy, IReadOnlyDictionary<string, object> Attributes);
