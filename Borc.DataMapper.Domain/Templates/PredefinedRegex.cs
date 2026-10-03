namespace Borc.DataMapper.Domain.Templates;

/// <summary>الگوهای آماده اعتبارسنجی (ایمیل، موبایل، ...). جدول audit و حذف منطقی ندارد.</summary>
public sealed class PredefinedRegex
{
    private PredefinedRegex()
    {
    }

    public int Id { get; private set; }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string Pattern { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public static PredefinedRegex Create(string code, string name, string pattern)
    {
        return new PredefinedRegex
        {
            Code = code.Trim(),
            Name = name.Trim(),
            Pattern = pattern,
            IsActive = true
        };
    }

    public void Update(string name, string pattern)
    {
        Name = name.Trim();
        Pattern = pattern;
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}