namespace Borc.DataMapper.Application.Imports.Common;

public static class ImportLimits
{
    public const int MaxRows = 20_000;
    public const int MaxColumns = 100;
    public const int MaxFileBytes = 10 * 1024 * 1024;
    public const int ChunkSize = 1000;
}