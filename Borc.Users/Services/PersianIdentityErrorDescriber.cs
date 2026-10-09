using Microsoft.AspNetCore.Identity;

namespace Borc.Users.Services;

/// <summary>Identity's validation messages in Persian.</summary>
internal sealed class PersianIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Error(nameof(DefaultError), "خطای ناشناخته رخ داد.");
    public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure), "این حساب هم‌زمان تغییر کرده است؛ دوباره تلاش کنید.");
    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch), "رمز عبور فعلی درست نیست.");
    public override IdentityError InvalidUserName(string? userName) =>
        Error(nameof(InvalidUserName), $"نام کاربری «{userName}» معتبر نیست؛ فقط حروف انگلیسی، رقم و . - _ @ مجاز است.");
    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), $"ایمیل «{email}» معتبر نیست.");
    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName), $"نام کاربری «{userName}» قبلاً ثبت شده است.");
    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail), $"ایمیل «{email}» قبلاً ثبت شده است.");
    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), $"رمز عبور باید دست‌کم {length} حرف باشد.");
    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit), "رمز عبور باید دست‌کم یک رقم داشته باشد.");
    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower), "رمز عبور باید یک حرف کوچک انگلیسی داشته باشد.");
    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper), "رمز عبور باید یک حرف بزرگ انگلیسی داشته باشد.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric), "رمز عبور باید یک نماد داشته باشد.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Error(nameof(PasswordRequiresUniqueChars), $"رمز عبور باید دست‌کم {uniqueChars} حرف متفاوت داشته باشد.");
    public override IdentityError UserAlreadyHasPassword() => Error(nameof(UserAlreadyHasPassword), "این کاربر رمز عبور دارد.");
    public override IdentityError UserLockoutNotEnabled() => Error(nameof(UserLockoutNotEnabled), "قفل حساب برای این کاربر فعال نیست.");

    private static IdentityError Error(string code, string description) => new() { Code = code, Description = description };
}
