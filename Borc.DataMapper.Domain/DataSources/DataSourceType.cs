namespace Borc.DataMapper.Domain.DataSources;

/// <summary>پیشنهادی: نیاز به تأیید.</summary>
public enum DataSourceType : short
{
    StaticList = 1,
    SqlQuery = 2,
    Api = 3
}