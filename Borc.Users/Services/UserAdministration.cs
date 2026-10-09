using System.Security.Claims;

using Borc.Users.Model;
using Borc.Users.Persistence;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Borc.Users.Services;

internal sealed class UserAdministration(
    UserManager<AppUser> users,
    UsersDbContext db,
    IHttpContextAccessor http,
    TimeProvider time,
    IEnumerable<IUserStatusListener> listeners) : IUserAdministration, IUserLookup
{
    public async Task<UserPage> ListAsync(UserQuery query, CancellationToken cancellationToken = default)
    {
        int pageSize = Math.Clamp(query.PageSize, 1, 200);
        IQueryable<AppUser> source = Filter(db.Users.AsNoTracking(), query.Search);
        if (query.Active is { } active)
        {
            source = source.Where(u => u.IsActive == active);
        }

        int total = await source.CountAsync(cancellationToken);
        int page = Math.Clamp(query.Page, 1, Math.Max(1, (total + pageSize - 1) / pageSize));
        List<AppUser> rows = await source.OrderBy(u => u.DisplayName).ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new UserPage(rows.Select(Summary).ToList(), total, page, pageSize);
    }

    public async Task<UserSummary?> GetAsync(long id, CancellationToken cancellationToken = default) =>
        await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, cancellationToken) is { } user ? Summary(user) : null;

    public async Task<long> CreateAsync(UserInput input, CancellationToken cancellationToken = default)
    {
        Validate(input);
        if (string.IsNullOrEmpty(input.Password))
        {
            throw new UsersException(UsersErrors.Invalid, "رمز عبور را وارد کنید.");
        }

        AppUser user = new()
        {
            UserName = input.UserName.Trim(),
            DisplayName = input.DisplayName.Trim(),
            Email = Normalize(input.Email),
            IsAdministrator = input.IsAdministrator,
            CreatedAt = Now,
            CreatedBy = ActorId,
        };
        Check(await users.CreateAsync(user, input.Password));
        return user.Id;
    }

    public async Task UpdateAsync(long id, UserInput input, CancellationToken cancellationToken = default)
    {
        Validate(input);
        AppUser user = await FindAsync(id);
        if (user.IsAdministrator && !input.IsAdministrator)
        {
            if (id == ActorId)
            {
                throw new UsersException(UsersErrors.SelfChange, "نمی‌توانید دسترسی مدیریتی خودتان را بردارید.");
            }

            await EnsureAnotherAdministratorAsync(id, cancellationToken);
        }

        user.DisplayName = input.DisplayName.Trim();
        user.IsAdministrator = input.IsAdministrator;
        user.UpdatedAt = Now;
        user.UpdatedBy = ActorId;
        Check(await users.SetUserNameAsync(user, input.UserName.Trim()));
        Check(await users.SetEmailAsync(user, Normalize(input.Email)));
        Check(await users.UpdateAsync(user));
    }

    public async Task SetActiveAsync(long id, bool active, CancellationToken cancellationToken = default)
    {
        AppUser user = await FindAsync(id);
        if (user.IsActive == active)
        {
            return;
        }

        if (!active)
        {
            if (id == ActorId)
            {
                throw new UsersException(UsersErrors.SelfChange, "نمی‌توانید حساب خودتان را غیرفعال کنید.");
            }

            if (user.IsAdministrator)
            {
                await EnsureAnotherAdministratorAsync(id, cancellationToken);
            }
        }

        user.IsActive = active;
        user.UpdatedAt = Now;
        user.UpdatedBy = ActorId;
        Check(await users.UpdateAsync(user));

        // A new stamp ends the user's existing sessions at the next cookie validation.
        Check(await users.UpdateSecurityStampAsync(user));
        foreach (IUserStatusListener listener in listeners)
        {
            await listener.OnStatusChangedAsync(id, active, cancellationToken);
        }
    }

    public async Task ResetPasswordAsync(long id, string newPassword, CancellationToken cancellationToken = default)
    {
        AppUser user = await FindAsync(id);
        if (string.IsNullOrEmpty(newPassword))
        {
            throw new UsersException(UsersErrors.Invalid, "رمز عبور را وارد کنید.");
        }

        foreach (IPasswordValidator<AppUser> validator in users.PasswordValidators)
        {
            Check(await validator.ValidateAsync(users, user, newPassword));
        }

        if (await users.HasPasswordAsync(user))
        {
            Check(await users.RemovePasswordAsync(user));
        }

        Check(await users.AddPasswordAsync(user, newPassword));
        Check(await users.SetLockoutEndDateAsync(user, null));
        Check(await users.ResetAccessFailedCountAsync(user));
        user.UpdatedAt = Now;
        user.UpdatedBy = ActorId;
        Check(await users.UpdateAsync(user));
    }

    public async Task<IReadOnlyList<UserSummary>> SearchAsync(string text, int maxResults, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return (await Filter(db.Users.AsNoTracking().Where(u => u.IsActive), text)
                .OrderBy(u => u.DisplayName)
                .Take(Math.Clamp(maxResults, 1, 100))
                .ToListAsync(cancellationToken))
            .Select(Summary)
            .ToList();
    }

    public async Task<IReadOnlyDictionary<long, UserSummary>> GetAsync(IReadOnlyCollection<long> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<long, UserSummary>();
        }

        long[] list = [.. ids.Distinct()];
        return (await db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToListAsync(cancellationToken))
            .ToDictionary(u => u.Id, Summary);
    }

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    private long? ActorId =>
        long.TryParse(http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out long id) ? id : null;

    private static IQueryable<AppUser> Filter(IQueryable<AppUser> source, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return source;
        }

        string text = search.Trim();
        string upper = text.ToUpperInvariant();
        return source.Where(u => u.DisplayName.Contains(text)
            || u.NormalizedUserName!.Contains(upper)
            || (u.NormalizedEmail != null && u.NormalizedEmail.Contains(upper)));
    }

    private static UserSummary Summary(AppUser u) => new(
        u.Id,
        u.UserName ?? "",
        u.DisplayName,
        u.Email,
        u.IsActive,
        u.IsAdministrator,
        u.LockoutEnd is { } end && end > DateTimeOffset.UtcNow,
        u.CreatedAt,
        u.LastSignInAt);

    private static string? Normalize(string? email) => string.IsNullOrWhiteSpace(email) ? null : email.Trim();

    private static void Validate(UserInput input)
    {
        List<string> errors = [];
        if (string.IsNullOrWhiteSpace(input.UserName))
        {
            errors.Add("نام کاربری را وارد کنید.");
        }

        if (string.IsNullOrWhiteSpace(input.DisplayName))
        {
            errors.Add("نام نمایشی را وارد کنید.");
        }
        else if (input.DisplayName.Trim().Length > AppUser.DisplayNameMaxLength)
        {
            errors.Add($"نام نمایشی حداکثر {AppUser.DisplayNameMaxLength} حرف است.");
        }

        if (errors.Count > 0)
        {
            throw new UsersException(UsersErrors.Invalid, errors);
        }
    }

    private async Task<AppUser> FindAsync(long id) =>
        await users.FindByIdAsync(id.ToString(System.Globalization.CultureInfo.InvariantCulture))
        ?? throw new UsersException(UsersErrors.NotFound, "کاربر پیدا نشد.");

    private async Task EnsureAnotherAdministratorAsync(long id, CancellationToken cancellationToken)
    {
        if (!await db.Users.AnyAsync(u => u.Id != id && u.IsActive && u.IsAdministrator, cancellationToken))
        {
            throw new UsersException(UsersErrors.LastAdministrator, "دست‌کم یک مدیر فعال باید بماند.");
        }
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new UsersException(UsersErrors.Identity, result.Errors.Select(e => e.Description).ToList());
        }
    }
}
