using System.Security.Claims;
using Borc.DataMapper.Application.Abstractions.Identity;

namespace Borc.DataMapper.Web.Mvc;

/// <summary>کاربر واردشده از کوکی ماژول کاربران (شناسه = NameIdentifier).</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor http) : ICurrentUser
{
    public long? UserId =>
        long.TryParse(http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
