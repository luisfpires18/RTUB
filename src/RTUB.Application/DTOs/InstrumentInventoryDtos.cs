namespace RTUB.Application.DTOs;

// Contracts of the React /inventory ("Instrumentos"). Signed-in
// members only; every field here is shown to any signed-in member. No EF entity leaves
// the server.

/// <summary>Filters: <c>Category</c> an <c>InstrumentType</c> name, <c>Condition</c> an <c>InstrumentCondition</c> name.</summary>
public sealed record InstrumentQuery(string? Search, string? Category, string? Condition);

/// <summary>
/// The list (by name), the counters (total and the first three conditions present, in enum order, over every
/// instrument), the filter options and whether the caller manages (Mod, Admin, Owner).
/// </summary>
public sealed record InstrumentListDto(
    int Total,
    IReadOnlyList<InstrumentStatDto> Stats,
    IReadOnlyList<InstrumentCardDto> Instruments,
    IReadOnlyList<MemberOptionDto> Categories,
    IReadOnlyList<MemberOptionDto> Conditions,
    bool CanManage);

public sealed record InstrumentStatDto(string Condition, string Label, int Count);

/// <summary><c>Category</c> as stored (the enum name), <c>CategoryLabel</c> its display; <c>ThumbnailUrl</c> the thumbnail, else the image.</summary>
public sealed record InstrumentCardDto(
    int Id,
    string Name,
    string Category,
    string CategoryLabel,
    string Condition,
    string ConditionLabel,
    string? Brand,
    string? Location,
    string? ThumbnailUrl);

/// <summary>"Detalhes do Instrumento", every field of the instrument. <c>LastMaintenanceDate</c> yyyy-MM-dd.</summary>
public sealed record InstrumentDetailDto(
    int Id,
    string Name,
    string Category,
    string CategoryLabel,
    string Condition,
    string ConditionLabel,
    string? Brand,
    string? SerialNumber,
    string? Location,
    string? LastMaintenanceDate,
    string? MaintenanceNotes,
    string? ImageUrl,
    string? ThumbnailUrl);

/// <summary>Create or edit. <c>Category</c> is only read when creating: an edit never changes it.</summary>
public sealed record InstrumentInput(
    string? Name,
    string? Category,
    string? Condition,
    string? Brand,
    string? SerialNumber,
    string? Location,
    DateOnly? LastMaintenanceDate,
    string? MaintenanceNotes);

public sealed record InstrumentCreatedDto(int Id);

/// <summary>An uploaded image: the original and its cropped 1:1 thumbnail.</summary>
public sealed record InstrumentImageUpload(Stream Content, string FileName, string ContentType, long Length);
