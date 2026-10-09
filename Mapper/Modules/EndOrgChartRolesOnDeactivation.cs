using System.Globalization;
using Borc.Users.Services;
using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Core.Model;

namespace Borc.DataMapper.Web.Modules;

/// <summary>
/// وقتی حسابی غیرفعال می‌شود، انتصاب‌ها و تفویض‌های جاری و آیندهٔ او در چارت پایان می‌یابد (آینده‌ها حذف می‌شوند).
/// When an account is deactivated, its current org-chart assignments and delegations end now and future ones are
/// removed, so the chart shows the positions as vacant and Acl stops granting through them.
/// Reactivation restores nothing: positions are assigned again on purpose.
/// </summary>
public sealed class EndOrgChartRolesOnDeactivation(IOrgChartReader reader, IOrgChartAdministration admin, TimeProvider time) : IUserStatusListener
{
    public async Task OnStatusChangedAsync(long userId, bool isActive, CancellationToken cancellationToken = default)
    {
        if (isActive)
        {
            return;
        }

        string id = userId.ToString(CultureInfo.InvariantCulture);
        DateTime now = time.GetUtcNow().UtcDateTime;

        IEnumerable<int> assignmentIds = (await reader.GetUserPositionsAsync(id, cancellationToken))
            .Where(p => p.Kind is AssignmentKind.Primary or AssignmentKind.Acting)
            .Select(p => p.AssignmentId)
            .Distinct();
        foreach (int assignmentId in assignmentIds)
        {
            if (await reader.GetAssignmentAsync(assignmentId, cancellationToken) is not { } a || a.ValidTo <= now)
            {
                continue;
            }

            if (a.ValidFrom > now)
            {
                await admin.RemoveAssignmentAsync(a.Id, cancellationToken);
            }
            else
            {
                await admin.EndAssignmentAsync(a.Id, now, cancellationToken);
            }
        }

        IEnumerable<DelegationInfo> delegations =
            (await reader.GetDelegationsAsync(new DelegationQuery(FromUserId: id), cancellationToken))
            .Concat(await reader.GetDelegationsAsync(new DelegationQuery(ToUserId: id), cancellationToken))
            .DistinctBy(d => d.Id)
            .Where(d => d.ValidTo is null || d.ValidTo > now);
        foreach (DelegationInfo d in delegations)
        {
            if (d.ValidFrom > now)
            {
                await admin.RemoveDelegationAsync(d.Id, cancellationToken);
            }
            else
            {
                await admin.EndDelegationAsync(d.Id, now, cancellationToken);
            }
        }
    }
}
