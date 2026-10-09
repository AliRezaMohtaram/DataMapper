namespace Borc.DataMapper.Application.Abstractions.Identity;

/// <summary>The signed-in user (id of the Users module's account), or null outside a request / when anonymous.</summary>
public interface ICurrentUser
{
    long? UserId { get; }
}
