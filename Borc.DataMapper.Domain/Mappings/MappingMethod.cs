namespace Borc.DataMapper.Domain.Mappings;

/// <summary>روش پیدا شدن نگاشت ستون. پیشنهادی: نیاز به تأیید.</summary>
public enum MappingMethod : short
{
    Manual = 1,
    Alias = 2,
    Regex = 3,
    Auto = 4
}