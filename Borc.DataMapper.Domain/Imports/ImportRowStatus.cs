namespace Borc.DataMapper.Domain.Imports;

/// <summary>پیشنهادی: نیاز به تأیید.</summary>
public enum ImportRowStatus : short
{
    Pending = 1,
    Valid = 2,
    Invalid = 3,
    Imported = 4
}