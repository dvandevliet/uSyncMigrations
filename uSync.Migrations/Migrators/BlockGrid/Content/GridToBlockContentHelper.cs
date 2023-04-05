using Microsoft.Extensions.Logging;

using Newtonsoft.Json.Linq;

using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Extensions;

using uSync.Migrations.Context;
using uSync.Migrations.Migrators.BlockGrid.BlockMigrators;
using uSync.Migrations.Migrators.BlockGrid.Extensions;
using uSync.Migrations.Migrators.BlockGrid.Models;
using uSync.Migrations.Migrators.Models;

namespace uSync.Migrations.Migrators.BlockGrid.Content;

/// <summary>
///  helper class that actually gets content from the grid and puts it into the blocklist. 
/// </summary>
internal class GridToBlockContentHelper
{
    private readonly SyncBlockMigratorCollection _blockMigrators;
    private readonly GridConventions _conventions;
    private readonly ILogger<GridToBlockContentHelper> _logger;

    public GridToBlockContentHelper(
        GridConventions gridConventions,
        SyncBlockMigratorCollection blockMigrators,
        ILogger<GridToBlockContentHelper> logger)
    {
        _blockMigrators = blockMigrators;
        _conventions = gridConventions;
        _logger = logger;
    }

    /// <summary>
    ///  convert a grid value (so with sections, rows, areas and controls) into a block value for a grid.
    /// </summary>
    public BlockValue? ConvertToBlockValue(GridValue source, SyncMigrationContext context)
    {
        // empty grid check
        if (source.Sections.Any() != true) return null;

        var sectionContentTypeAlias = _conventions.SectionContentTypeAlias(source.Name);

        var sections = source.Sections
            .Select(x => (Grid: x.Grid.GetIntOrDefault(0), x.Rows))
            .ToArray();

        var gridColumns = sections.Sum(x => x.Grid);

        var block = new BlockValue();

        var blockLayouts = new List<BlockGridLayoutItem>();

        foreach (var (sectionColums, rows) in sections)
        {
            var sectionIsFullWidth = sectionColums == gridColumns;

            foreach (var row in rows)
            {
                var areas = row.Areas.Select((x, i) => (x, i));

                var rowColumns = row.Areas.Sum(x => x.Grid.GetIntOrDefault(0));
                var rowIsFullWidth = sectionIsFullWidth && rowColumns == gridColumns;
                
                var rowLayoutAreas = new List<BlockGridLayoutAreaItem>();

                foreach (var area in row.Areas.Select((value, index) => (value, index)))
                {
	                // get the content
                    var contentAndSettings = GetGridAreaBlockContent(area.value, context).ToList();
                    if (!contentAndSettings.Any()) continue;

                    // get the layouts 
                    var layouts = GetGridAreaBlockLayouts(area.value, contentAndSettings).ToList();
                    if (!layouts.Any()) continue;

                    // we need wrap the layouts.ToArray in a Cell wrapper so we can have config on every block grid editor area
                    var columnSetting = GetGridColumnBlockSetting(area.value, context);
                    block.SettingsData.Add(columnSetting);

                    var cellLayoutAreaKey = _conventions
	                    .LayoutAreaAlias("Column" + row.Name, _conventions.AreaAlias(area.index)).ToGuid();
                    var cellLayoutItem = new BlockGridLayoutItem
                    {
                        ContentUdi = Udi.Create(UmbConstants.UdiEntityType.Element, Guid.NewGuid()),
                        SettingsUdi = columnSetting?.Udi,
                        ColumnSpan = 12,
                        RowSpan = 1,
                        Areas = new BlockGridLayoutAreaItem[]
                        {
                            new BlockGridLayoutAreaItem()
                            {
                                Key = cellLayoutAreaKey,
                                Items = layouts.ToArray()
                            }
                        }
                    };

                    block.ContentData.Add(new BlockItemData()
                    {
                        Udi = cellLayoutItem.ContentUdi,
                        ContentTypeKey = context.ContentTypes.GetKeyByAlias(_conventions.ColumnContentTypeAlias("Column"))
                    });
                    
                    // always add content items in layout context even if area is full width
                    var areaItem = new BlockGridLayoutAreaItem
                    {
                        Key =  _conventions.LayoutAreaAlias(row.Name, _conventions.AreaAlias(area.index)).ToGuid(),
                        Items = new BlockGridLayoutItem[] { cellLayoutItem }
                    };

                    rowLayoutAreas.Add(areaItem);
                    

                    // add the content and settings to the block. 
                    contentAndSettings.ForEach(x =>
                    {
                        block.ContentData.Add(x.Content);
                        if (x.Settings != null)
                        {
                            block.SettingsData.Add(x.Settings);
                        }
                    });
                }

                // row 
                if (!rowLayoutAreas.Any()) continue;

                var rowSetting = GetGridRowBlockSetting(row, context);
                block.SettingsData.Add(rowSetting);

                var rowContent = GetGridRowBlockContent(row, context);
                block.ContentData.Add(rowContent);
                

                blockLayouts.Add(GetGridRowBlockLayout(rowContent, rowSetting, rowLayoutAreas, rowColumns));
            }

            // section 
        }

        // end - process layouts into block format. 
        block.Layout = new Dictionary<string, JToken>
        {
            { UmbConstants.PropertyEditors.Aliases.BlockGrid, JToken.FromObject(blockLayouts) }
        };

        return block;
    }

    private IEnumerable<BlockContentPair> GetGridAreaBlockContent(GridValue.GridArea area, SyncMigrationContext context)
    {
        foreach (var control in area.Controls)
        {
            var content = GetBlockItemDataFromGridControl(control, context);
            if (content == null) continue;

            var settings = GetBlockItemSettingsFromGridControl(control, context);

            yield return new BlockContentPair(content, settings);
        }
    }

    /// <summary>
    ///   applyTo defines what this setting can be applied to. It should be either row or cell as a string.
    ///   A JSON object can also be used if you need a more specific configuration. A JSON configuration could look like this:
	///   "applyTo": {
	///     "row": "Headline,Article",
	///     "cell": "4,8,6"
    ///   }
    ///   This would ensure the setting can only be used on rows named Article or Headline, or on cells sized: 4, 8 or 6.
    ///   If it should only apply to cells you can remove the row property. If it should apply to all rows you can specify
    ///   it by having the row property with null or an empty string as value.
    /// </summary>
    /// <param name="control"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    private BlockItemData? GetBlockItemSettingsFromGridControl(GridValue.GridControl control, SyncMigrationContext context)
    {
	    return null;
    }

    private IEnumerable<BlockGridLayoutItem> GetGridAreaBlockLayouts(GridValue.GridArea area, IEnumerable<BlockContentPair> contentAndSettings)
    {
        foreach (var item in contentAndSettings)
        {
            var layout = new BlockGridLayoutItem
            {
                ContentUdi = item.Content.Udi,
                SettingsUdi = item.Settings?.Udi,
                ColumnSpan = area.Grid.GetIntOrDefault(0),
                RowSpan = 1
            };

            yield return layout;
        }
    }

    private BlockItemData GetGridColumnBlockSetting(GridValue.GridArea area, SyncMigrationContext context)
    {
	    var settingsKey = context.ContentTypes.GetKeyByAlias( _conventions.SettingContentTypeAlias("Column"));
	    var settingsValues = area.Config?.ToObject<Dictionary<string, object?>>() == null
		    ? new Dictionary<string, object?>()
		    : JObject.FromObject(area.Config).ToObject<Dictionary<string, object?>>()
		      ?? new Dictionary<string, object?>();

	    settingsValues = settingsValues.Select(kv =>
			    new KeyValuePair<string, object?>(
				    _conventions.ShortStringHelper.CleanStringForSafeAlias(kv.Key),
				    kv.Value)
		    )
		    .ToDictionary(x => x.Key, x => x.Value);
	    
	    return new BlockItemData
	    {
		    Udi = Udi.Create(UmbConstants.UdiEntityType.Element, Guid.NewGuid()),
		    ContentTypeKey = settingsKey,
		    RawPropertyValues = settingsValues
	    };
    }

    private BlockItemData GetGridRowBlockSetting(GridValue.GridRow row, SyncMigrationContext context)
    {
	    var settingsKey = context.ContentTypes.GetKeyByAlias( _conventions.SettingContentTypeAlias("Row"));
	    var settingsValues = row.Config == null
		    ? new Dictionary<string, object?>()
		    : JObject.FromObject(row.Config).ToObject<Dictionary<string, object?>>()
		      ?? new Dictionary<string, object?>();

	    settingsValues = settingsValues.Select(kv =>
			    new KeyValuePair<string, object?>(
				    _conventions.ShortStringHelper.CleanStringForSafeAlias(kv.Key),
				    kv.Value)
		    )
		    .ToDictionary(x => x.Key, x => x.Value);
	    
	    return new BlockItemData
        {
            Udi = Udi.Create(UmbConstants.UdiEntityType.Element, Guid.NewGuid()),
            ContentTypeKey = settingsKey,
            RawPropertyValues = settingsValues
        };
    }

    private BlockItemData GetGridRowBlockContent(GridValue.GridRow row, SyncMigrationContext context)
    {
	    var rowLayoutContentTypeAlias = _conventions.LayoutContentTypeAlias(row.Name);
	    var rowContentTypeKey = context.GetContentTypeKeyOrDefault(rowLayoutContentTypeAlias, rowLayoutContentTypeAlias.ToGuid()); 

	    return new BlockItemData
	    {
		    Udi = Udi.Create(UmbConstants.UdiEntityType.Element, row.Id),
		    ContentTypeKey = rowContentTypeKey,
		    ContentTypeAlias = rowLayoutContentTypeAlias
	    };
    }

    private BlockGridLayoutItem GetGridRowBlockLayout(BlockItemData rowContent, BlockItemData rowSetting, List<BlockGridLayoutAreaItem> rowLayoutAreas, int? rowColumns)
    {
        return new BlockGridLayoutItem
        {
            ContentUdi = rowContent.Udi,
            Areas = rowLayoutAreas.ToArray(),
            ColumnSpan = rowColumns,
            RowSpan = 1,
            SettingsUdi = rowSetting?.Udi
        };

    }

    private BlockItemData? GetBlockItemDataFromGridControl(GridValue.GridControl control, SyncMigrationContext context)
    {
        if (control.Value == null) return null;

        var blockMigrator = _blockMigrators.GetMigrator(control.Editor);
        if (blockMigrator == null)
        {
            _logger.LogWarning("No Block Migrator for [{editor}/{view}]", control.Editor.Alias, control.Editor.View);
            return null;
        }

        var contentTypeAlias = blockMigrator.GetContentTypeAlias(control);
        if (contentTypeAlias == null)
        {
            _logger.LogWarning("No contentTypeAlias from migrator {migrator}", blockMigrator.GetType().Name);
            return null;
        }

        var contentTypeKey = context.ContentTypes.GetKeyByAlias(contentTypeAlias);
        if (contentTypeKey == Guid.Empty)
        {
            _logger.LogWarning("Cannot find content type key from alias {alias}", contentTypeAlias);
            return null;
        }

        var data = new BlockItemData
        {
            Udi = Udi.Create(UmbConstants.UdiEntityType.Element, Guid.NewGuid()),
            ContentTypeAlias = contentTypeAlias,
            ContentTypeKey = contentTypeKey
        };


        foreach (var (propertyAlias, value) in blockMigrator.GetPropertyValues(control, context))
        {
            var editorAlias = context.ContentTypes.GetEditorAliasByTypeAndProperty(contentTypeAlias, propertyAlias);
            var propertyValue = value;
            if (editorAlias != null)
            {

                var migrator = context.Migrators.TryGetMigrator(editorAlias.OriginalEditorAlias);
                if (migrator != null)
                {
                    var property = new SyncMigrationContentProperty(
                        contentTypeAlias, propertyAlias,
                        editorAlias.OriginalEditorAlias, value?.ToString() ?? string.Empty);
                    propertyValue = migrator.GetContentValue(property, context);
                    _logger.LogDebug("Migrator: {migrator} returned {value}", migrator.GetType().Name, propertyValue); 
                }
                else
                {
                    _logger.LogDebug("No Block Migrator found for [{alias}] (value will be passed through)", editorAlias.OriginalEditorAlias);
                }
            }

            data.RawPropertyValues[propertyAlias] = propertyValue;
        }

        return data;
    }
}

internal static class GridToBlockContentExtensions
{
    public static int GetIntOrDefault(this string? value, int defaultValue)
        => int.TryParse(value, out var intValue) ? intValue : defaultValue;
}
