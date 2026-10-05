namespace Borc.DataMapper.Application.Imports.Common;

/// <summary>ورودی فرم تطبیق: یک ستون Excel به یک فیلد (کلید فیلد؛ خالی = نادیده گرفتن).</summary>
public sealed class ColumnMappingInput
{
    public string SourceColumn { get; set; } = string.Empty;

    public string? TargetFieldKey { get; set; }
}



