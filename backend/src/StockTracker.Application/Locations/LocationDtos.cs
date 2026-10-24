using StockTracker.Domain.Locations;

namespace StockTracker.Application.Locations;

public sealed record LocationTreeNodeDto(
    Guid Id,
    string Code,
    string Name,
    LocationType Type,
    bool IsActive,
    IReadOnlyList<LocationTreeNodeDto> Children);

public sealed record LocationRefDto(Guid Id, string Code, string Name);

public sealed record LocationChildDto(Guid Id, string Code, string Name, LocationType Type, bool IsActive);

public sealed record LocationDto(
    Guid Id,
    string Code,
    string Name,
    LocationType Type,
    Guid? ParentId,
    int Depth,
    bool IsActive,
    IReadOnlyList<LocationRefDto> Ancestors,
    IReadOnlyList<LocationChildDto> Children,
    DateTime CreatedAt);
