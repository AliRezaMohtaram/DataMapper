namespace Borc.DataMapper.Application.Imports.Common;

public static class ImportFiles
{
    /// <summary>نوع محتوا وقتی مرورگر نفرستاده باشد (بر اساس پسوند).</summary>
    public static string DefaultContentType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".csv" => "text/csv",
            _ => "application/octet-stream"
        };
}
