using Acl.Core;
using Acl.Core.Admin;
using Acl.Core.Model;
using Microsoft.Extensions.Options;

namespace Borc.DataMapper.Web.Modules;

/// <summary>
/// اولین اجرا: نقش «مدیر Mapper» با همهٔ مجوزها روی ماژول Mapper (همهٔ صفحه‌ها و چارت از آن ارث می‌برند) ساخته و به
/// ادمین‌های Acl:SuperAdminUserIds داده می‌شود؛ وگرنه پس از فعال شدن کنترل دسترسی هیچ‌کس به صفحه‌های Mapper دسترسی ندارد.
/// First run only: creates the role "مدیر Mapper" with every action and the data scope "all rows" on the Mapper module
/// (all pages, the org chart included, inherit it) and gives it to the configured Acl super admins. Once the role
/// exists it is left alone (except that a role without any data scope rule gets "all rows" once), so admins can
/// change or remove it. Registered after AddAccessControl, so Acl has synced the resources by then.
/// </summary>
public sealed class MapperAccessBootstrapper(IServiceProvider services, IOptions<AclOptions> acl, ILogger<MapperAccessBootstrapper> logger)
    : IHostedService
{
    public const string RoleName = "مدیر Mapper";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IRoleAdministration roles = scope.ServiceProvider.GetRequiredService<IRoleAdministration>();
        IAssignmentAdministration assignments = scope.ServiceProvider.GetRequiredService<IAssignmentAdministration>();

        ResourceOption? root = (await assignments.GetResourceOptionsAsync(cancellationToken))
            .FirstOrDefault(r => r.Key == MapperResources.Root);
        if (root is null)
        {
            logger.LogWarning("Acl resource {Resource} not found; the {Role} role was not created.", MapperResources.Root, RoleName);
            return;
        }

        IDataScopeAdministration scopes = scope.ServiceProvider.GetRequiredService<IDataScopeAdministration>();
        if ((await roles.ListRolesAsync(cancellationToken)).FirstOrDefault(r => r.Name == RoleName) is { } existing)
        {
            // Installations from before data scopes: give the role "all rows" once (only while it has no rule at all).
            if ((await scopes.GetRoleRulesAsync(existing.Id, cancellationToken)).Count == 0)
            {
                await scopes.AddRoleRuleAsync(existing.Id, new DataScopeRuleInput { ResourceId = root.Id, ScopeType = DataScopeType.All }, cancellationToken);
            }

            return;
        }

        int roleId = await roles.CreateRoleAsync(new RoleInput
        {
            Name = RoleName,
            Description = "همهٔ صفحه‌ها و عملیات Mapper و چارت سازمانی (ساخته‌شده در اولین اجرا).",
            IsActive = true,
        }, cancellationToken);
        await roles.SetPermissionsAsync(roleId,
            root.Actions.Select(a => new PermissionChange(root.Id, a, PermissionEffect.Allow)).ToList(), cancellationToken);
        await scopes.AddRoleRuleAsync(roleId, new DataScopeRuleInput { ResourceId = root.Id, ScopeType = DataScopeType.All }, cancellationToken);

        foreach (string userId in acl.Value.SuperAdminUserIds)
        {
            await assignments.AddUserRoleAsync(userId, new UserRoleInput { RoleId = roleId }, cancellationToken);
        }

        logger.LogInformation("Created the {Role} role and gave it to {Users}.", RoleName, string.Join(", ", acl.Value.SuperAdminUserIds));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
