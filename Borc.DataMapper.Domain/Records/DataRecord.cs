using Borc.DataMapper.Domain.Common;

namespace Borc.DataMapper.Domain.Records;

public sealed class DataRecord : EntityBase, IOrgUnitOwned
{
    /// <summary>واحد سازمانی مالک (کلید چارت)؛ null = عمومی.</summary>
    public string? OrgUnitKey { get; private set; }

    public void AssignOrgUnit(string? orgUnitKey) =>
        OrgUnitKey = string.IsNullOrWhiteSpace(orgUnitKey) ? null : orgUnitKey.Trim();

    private DataRecord()
    {
    }

    public long TemplateId { get; private set; }

    public long TemplateVersionId { get; private set; }

    public string DataJson { get; private set; } = null!;

    public RecordSource SourceType { get; private set; }

    public long? ImportBatchId { get; private set; }

    public long? ImportRowId { get; private set; }

    public static DataRecord CreateManual(
        long templateId,
        long templateVersionId,
        string dataJson)
    {
        return new DataRecord
        {
            TemplateId = templateId,
            TemplateVersionId = templateVersionId,
            DataJson = dataJson,
            SourceType = RecordSource.Manual,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static DataRecord CreateFromImport(
        long templateId,
        long templateVersionId,
        string dataJson,
        long importBatchId,
        long importRowId,
        string? orgUnitKey = null)
    {
        return new DataRecord
        {
            OrgUnitKey = orgUnitKey,
            TemplateId = templateId,
            TemplateVersionId = templateVersionId,
            DataJson = dataJson,
            SourceType = RecordSource.Import,
            ImportBatchId = importBatchId,
            ImportRowId = importRowId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateData(string dataJson)
    {
        DataJson = dataJson;
        UpdatedAt = DateTime.UtcNow;
    }
}