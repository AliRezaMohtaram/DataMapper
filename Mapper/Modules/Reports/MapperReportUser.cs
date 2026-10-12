using System.Globalization;

namespace Borc.DataMapper.Web.Modules.Reports;

/// <summary>MReporter's current user: Mapper's signed-in user id as a string (like OrgChart and Acl).</summary>
public sealed class MapperReportUser(Borc.DataMapper.Application.Abstractions.Identity.ICurrentUser user) : MReporter.Core.Abstractions.ICurrentUser
{
    public string? UserId => user.UserId?.ToString(CultureInfo.InvariantCulture);
}
