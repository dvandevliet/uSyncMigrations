using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Cms.Core.Models.Blocks;

namespace uSync.Migrations.Migrators.BlockGrid.Models;

internal static class Grid 
{ 
    internal static class LayoutBlocks
    {
        public const string Background = "#cfe2f3";
        public const string Icon = "#2986cc";
	}

    internal static class CellBlocks
    {
	    public const string Background = "#d9d2e9";
	    public const string Icon = "#8e7cc3";
    }

    internal static class GridBlocks
    {
        public const string Background = "#fce5cd";
        public const string Icon = "#ce7e00"; 
    }
}

internal class GridSettingPrevalueItem
{
	[JsonProperty("label")]
	public string Label { get; set; }

	[JsonProperty("value")]
	public string Value { get; set; }
}

internal class GridSettingConfiguration
{
	[JsonProperty("label")]
	public string Label { get; set; }

	[JsonProperty("description")]
	public string Description { get; set; }

	[JsonProperty("key")]
	public string Key { get; set; }

	[JsonProperty("view")]
	public string View { get; set; }

	[JsonProperty("prevalues")]
	public IEnumerable<GridSettingPrevalueItem>? Prevalues { get; set; }

	[JsonProperty("defaultConfig")]
	public JObject? DefaultConfig { get; set; }

	[JsonProperty("ApplyTo")] // row | cell | json?
	public string ApplyTo { get; set; }

}

internal class GridTemplateConfiguration
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("sections")]
    public IEnumerable<GridSectionConfiguration>? Sections { get; set; }
}

internal class GridSectionConfiguration
{
    [JsonProperty("grid")]
    public int Grid { get; set; }

    [JsonProperty("allowAll")]
    public bool? AllowAll { get; set; }

    [JsonProperty("allowed")]
    public string[]? Allowed { get; set; }
}

internal class GridLayoutConfiguration
{
    [JsonProperty("label")]
    public string? Label { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("areas")]
    public IEnumerable<GridAreaConfiguration>? Areas { get; set; }
}

internal class GridAreaConfiguration
{
    [JsonProperty("grid")]
    public int Grid { get; set; }

    [JsonProperty("allowAll")]
    public bool? AllowAll { get; set; }

    [JsonProperty("allowed")]
    public string[]? Allowed { get; set; }
}

/// <summary>
///  contains the data for a block (content and settings)
/// </summary>
internal class BlockContentPair
{
    public BlockItemData Content { get; set; }
    public BlockItemData? Settings { get; set; }

    public BlockContentPair(BlockItemData content, BlockItemData? settings)
    {
        Content = content;
        Settings = settings;
    }
}