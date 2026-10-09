using Acl.Core;
using Acl.Core.Admin;
using Acl.Core.Model;
using Microsoft.Extensions.Options;

namespace Borc.DataMapper.Web.Modules;

/// <summary>
/// اولین اجرا: نقش «مدیر Mapper» با همهٔ مجوزها روی ماژول Mapper (همهٔ صفحه‌ها و چارت از آن ارث می‌برند) ساخته و به
/// ادمین‌های Acl:SuperAdminUserIds داده می‌شود؛ وگرنه پس از فعال شدن کنترل دسترسی هیچ‌کس به صفحه‌های Mapper دسترسی ندارد.
/// First run only: creates the role "مدیر Mapper" with every action on the Mapper module (all pages, the org chart
/// included, inherit it) and gives it to the configured Acl super admins. Once the role exists it is left alone, so
/// admins can change or remove it. Registered after AddAccessControl, so Acl has synced the resources by then.
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

        if ((await roles.ListRolesAsync(cancellationToken)).Any(r => r.Name == RoleName))
        {
            return;
        }

        ResourceOption? root = (await assignments.GetResourceOptionsAsync(cancellationToken))
            .FirstOrDefault(r => r.Key == MapperResources.Root);
        if (root is null)
        {
            logger.LogWarning("Acl resource {Resource} not found; the {Role} role was not created.", MapperResources.Root, RoleName);
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

        foreach (string userId in acl.Value.SuperAdminUserIds)
        {
            await assignments.AddUserRoleAsync(userId, new UserRoleInput { RoleId = roleId }, cancellationToken);
        }

        logger.LogInformation("Created the {Role} role and gave it to {Users}.", RoleName, string.Join(", ", acl.Value.SuperAdminUserIds));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
