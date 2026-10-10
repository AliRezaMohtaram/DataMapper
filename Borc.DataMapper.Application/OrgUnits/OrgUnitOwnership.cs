using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.OrgUnits;

/// <summary>انواع داده‌ای که واحد سازمانی دارند. Kinds of rows that belong to an org unit.</summary>
public enum OrgUnitOwnedKind
{
    Template = 1,
    DataSource = 2,
    MappingProfile = 3,
    Import = 4,
    DataRecord = 5,
}

/// <summary>Display data of one row for the "change unit" form (null when the row is missing or not visible).</summary>
public sealed record OrgUnitOwnerDto(OrgUnitOwnedKind Kind, long Id, string Title, string? OrgUnitKey);

public sealed record GetOrgUnitOwnerQuery(OrgUnitOwnedKind Kind, long Id) : IRequest<OrgUnitOwnerDto?>;

/// <summary>Moves a row to another unit (null = public). The caller checks permission and that the unit is allowed.</summary>
public sealed record ChangeOrgUnitCommand(OrgUnitOwnedKind Kind, long Id, string? OrgUnitKey) : IRequest<Result>;

public sealed class OrgUnitOwnershipHandlers(IAppDbContext db)
    : IRequestHandler<GetOrgUnitOwnerQuery, OrgUnitOwnerDto?>, IRequestHandler<ChangeOrgUnitCommand, Result>
{
    public async Task<OrgUnitOwnerDto?> Handle(GetOrgUnitOwnerQuery request, CancellationToken cancellationToken) =>
        await FindAsync(request.Kind, request.Id, cancellationToken) is { } row
            ? new OrgUnitOwnerDto(request.Kind, request.Id, row.Title, row.Entity.OrgUnitKey)
            : null;

    public async Task<Result> Handle(ChangeOrgUnitCommand request, CancellationToken cancellationToken)
    {
        if (await FindAsync(request.Kind, request.Id, cancellationToken) is not { } row)
            return Result.Failure("مورد پیدا نشد.");

        row.Entity.AssignOrgUnit(request.OrgUnitKey);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Ok("واحد سازمانی تغییر کرد.");
    }

    /// <summary>Through the query filters, so a row the user cannot see is "not found".</summary>
    private async Task<(IOrgUnitOwned Entity, string Title)?> FindAsync(OrgUnitOwnedKind kind, long id, CancellationToken ct)
    {
        switch (kind)
        {
            case OrgUnitOwnedKind.Template:
                return await db.Templates.FirstOrDefaultAsync(x => x.Id == id, ct) is { } t ? (t, t.Name) : null;
            case OrgUnitOwnedKind.DataSource:
                return await db.DataSources.FirstOrDefaultAsync(x => x.Id == id, ct) is { } d ? (d, d.Name) : null;
            case OrgUnitOwnedKind.MappingProfile:
                return await db.MappingProfiles.FirstOrDefaultAsync(x => x.Id == id, ct) is { } m ? (m, m.Name) : null;
            case OrgUnitOwnedKind.Import:
                return await db.ImportBatches.FirstOrDefaultAsync(x => x.Id == id, ct) is { } i ? (i, i.FileName) : null;
            case OrgUnitOwnedKind.DataRecord:
                return await db.DataRecords.FirstOrDefaultAsync(x => x.Id == id, ct) is { } r ? (r, $"رکورد #{r.Id}") : null;
            default:
                return null;
        }
    }
}
