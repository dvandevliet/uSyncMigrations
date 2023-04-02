using System.Data;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json.Linq;
using NPoco;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;
using uSync.Migrations.Context;
using uSync.Migrations.Migrators.BlockGrid.Extensions;
using uSync.Migrations.Migrators.BlockGrid.Models;
using uSync.Migrations.Models;
using static NPoco.SqlBuilder;
using static Umbraco.Cms.Core.PropertyEditors.ListViewConfiguration;
using static uSync.Migrations.Migrators.BlockGrid.Models.Grid;

namespace uSync.Migrations.Migrators.BlockGrid.Config;


internal class GridToBlockGridConfigLayoutBlockHelper
{
    private readonly GridConventions _conventions;
    private readonly IDataTypeService _dataTypeService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GridToBlockGridConfigLayoutBlockHelper> _logger;

    public GridToBlockGridConfigLayoutBlockHelper(
        GridConventions conventions,
        IDataTypeService dataTypeService,
        IServiceScopeFactory scopeFactory,
        ILogger<GridToBlockGridConfigLayoutBlockHelper> logger)
    {
        _conventions = conventions;
        _dataTypeService = dataTypeService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void AddLayoutBlocks(GridToBlockGridConfigContext gridBlockContext, SyncMigrationContext context)
    {
        // gather all the layout blocks we can from the templates 
        // and layouts sections of the config. 

        GetSettings(gridBlockContext.GridConfiguration.GetItemBlock("config"), gridBlockContext, context);

        GetTemplateLayouts(gridBlockContext.GridConfiguration.GetItemBlock("templates"), gridBlockContext, context);

        GetLayoutLayouts(gridBlockContext.GridConfiguration.GetItemBlock("layouts"), gridBlockContext, context);

        AddContentTypesForLayoutBlocks(gridBlockContext, context);
    }

    private void GetSettings(JToken? settings, GridToBlockGridConfigContext gridBlockContext,
	    SyncMigrationContext context)
    {
	    if (settings == null) return;

	    _logger.LogDebug("Processing Settings for grid to blockgrid");

	    var gridSettings = settings
		    .ToObject<IEnumerable<GridSettingConfiguration>>() ?? Enumerable.Empty<GridSettingConfiguration>();

	    // row => layout setting
	    var rowSettingAlias = _conventions.SettingContentTypeAlias("row");
	    var rowSettingContentType = new NewContentTypeInfo
	    {
		    Key = rowSettingAlias.ToGuid(),
		    Alias = rowSettingAlias,
		    Name = "Row Setting",
		    Description = "Grid row setting",
		    Folder = "BlockGrid/Settings",
		    Icon = "icon-settings color-purple",
		    IsElement = true,
		    Properties = gridSettings?.Where(s => s.ApplyTo.InvariantEquals("row")).Select(ToProperty).ToList() ??
		                 new List<NewContentTypeProperty>()
	    };
	    context.ContentTypes.AddNewContentType(rowSettingContentType);

	    // cell => block setting
	    var cellSettingAlias = _conventions.SettingContentTypeAlias("cell");
	    var cellSettingContentType = new NewContentTypeInfo
	    {
		    Key = cellSettingAlias.ToGuid(),
		    Alias = cellSettingAlias,
		    Name = "Cell Setting",
		    Description = "Grid cell setting",
		    Folder = "BlockGrid/Settings",
		    Icon = "icon-settings color-purple",
		    IsElement = true,
		    Properties = gridSettings?.Where(s => s.ApplyTo.InvariantEquals("cell")).Select(ToProperty).WhereNotNull().ToList() ??
		                 new List<NewContentTypeProperty>()
	    };
	    context.ContentTypes.AddNewContentType(cellSettingContentType);

    }

    private NewContentTypeProperty? ToProperty(GridSettingConfiguration setting)
    {
	    using var scope = _scopeFactory.CreateScope();

	    switch (setting.View)
	    {
		    case "radiobuttonlist":

			    var dataTypeName = $"BlockGrid {setting.ApplyTo.ToLower().ToFirstUpperInvariant()} Setting - {setting.Key.ToLower().ToFirstUpperInvariant()} - Radiobuttonlist";

			    var dataType = _dataTypeService.GetDataType(dataTypeName);
			    
			    if (dataType == null)
			    {
				    var propertyEditorCollection = scope.ServiceProvider.GetRequiredService<PropertyEditorCollection>();
				    var serializer = scope.ServiceProvider.GetRequiredService<IConfigurationEditorJsonSerializer>();

				    propertyEditorCollection.TryGet(UmbConstants.PropertyEditors.Aliases.RadioButtonList, out IDataEditor? editor);
				    var config = new ValueListConfiguration();
				    if (setting.Prevalues != null)
				    {
					    var itemId = 1;
					    foreach (var item in setting.Prevalues)
					    {

						    config.Items.Add(new ValueListConfiguration.ValueListItem
						    {

							    Id = itemId,
							    Value = item.Value
						    });
						    itemId++;
					    }
				    }

				    dataType = new DataType(editor, serializer)
				    {
					    DatabaseType = ValueStorageType.Ntext,
					    CreateDate = DateTime.Now,
					    CreatorId = -1,
					    Name = dataTypeName,
					    Configuration = config
				    };
				    _dataTypeService.Save(dataType);
			    }

                var shortStringHelper = scope.ServiceProvider.GetRequiredService<IShortStringHelper>();
			    return new NewContentTypeProperty()
			    {
				    Alias = setting.Key.ToSafeAlias(shortStringHelper), //dataTypeName.ToSafeAlias(shortStringHelper),
				    DataTypeAlias = dataTypeName,
				    Name = setting.Description.Length > 0 ? setting.Description : setting.Label
			    };
			    break;

		    default:
			    _logger.LogWarning($"Missing property conversion for Setting {setting.View}. Setting was not added");
			    return null;
	    }
    
    }

    private void GetTemplateLayouts(JToken? templates, GridToBlockGridConfigContext gridBlockContext, SyncMigrationContext context)
    {
        if (templates == null) return;

        var gridTemplateConfiguration = templates
                .ToObject<IEnumerable<GridTemplateConfiguration>>() ?? Enumerable.Empty<GridTemplateConfiguration>();

        _logger.LogDebug("Processing Template Layouts for grid to blockgrid");

        foreach (var template in gridTemplateConfiguration)
        {
            if (template.Sections == null) continue;

            var areas = new List<BlockGridConfiguration.BlockGridAreaConfiguration>();

            foreach (var (section, gridSectionIndex) in template.Sections.Select((x, i) => (x, i)))
            {
	            var allowed = new List<string>();
	            if (section.Allowed?.Any() == true)
	            {
		            allowed.AddRange(section.Allowed);
	            }
	            else
	            {
		            allowed.Add("*");
	            }

                if (section.Grid == gridBlockContext.GridColumns)
                {
                    _logger.LogDebug("Adding [{allowed}] to root layouts from template {template?.Name}", string.Join(",", allowed));
                    gridBlockContext.AppendToRootLayouts(allowed);
                    continue;
                }

                var areaAlias = _conventions.AreaAlias(gridSectionIndex);
                var area = new BlockGridConfiguration.BlockGridAreaConfiguration
                {
                    Alias = areaAlias,
                    ColumnSpan = section.Grid,
                    Key = _conventions.LayoutAreaAlias("template" + template.Name, areaAlias).ToGuid()
                };

                areas.Add(area);

                if (allowed.Any())
                {
                    gridBlockContext.AllowedLayouts[area] = allowed;
                }
            }

            if (areas.Count == 0)
            {
                _logger.LogDebug("No areas added");
                continue;
            }

            var contentTypeAlias = _conventions.TemplateContentTypeAlias(template.Name);

            var layoutBlock = GetLayoutBlockGridConfiguration(gridBlockContext, context, template?.Name, areas, contentTypeAlias);

            gridBlockContext.LayoutBlocks.TryAdd(contentTypeAlias, layoutBlock);

            context.ContentTypes.AddNewContentType(GetGridLayoutContentType(
	            layoutBlock.ContentElementTypeKey, contentTypeAlias, template?.Name ?? contentTypeAlias));
            
        }
    }

    private BlockGridConfiguration.BlockGridBlockConfiguration GetLayoutBlockGridConfiguration(
	    GridToBlockGridConfigContext gridBlockContext,
	    SyncMigrationContext context,
	    string? label,
	    List<BlockGridConfiguration.BlockGridAreaConfiguration> areas,
	    string contentTypeAlias)
    {
	    var layoutBlock = new BlockGridConfiguration.BlockGridBlockConfiguration
	    {
		    Label = label,
		    Areas = areas.ToArray(),
		    AllowAtRoot = true,
		    ContentElementTypeKey = context.GetContentTypeKeyOrDefault(contentTypeAlias, contentTypeAlias.ToGuid()),
		    GroupKey = gridBlockContext.LayoutsGroup.Key.ToString(),
		    BackgroundColor = Grid.LayoutBlocks.Background,
		    IconColor = Grid.LayoutBlocks.Icon,
		    SettingsElementTypeKey = _conventions.SettingContentTypeAlias("row").ToGuid()
	    };
	    return layoutBlock;
    }

    private NewContentTypeInfo GetGridLayoutContentType(Guid key, string alias, string name)
    {
	    return new NewContentTypeInfo
	    {
		    Key = key,
		    Alias = alias,
		    Name = name,
		    Description = "Grid Layoutblock",
		    Folder = "BlockGrid/Layouts",
		    Icon = "icon-layout color-purple",
		    IsElement = true
	    };
    }
    private void GetLayoutLayouts(JToken? layouts, GridToBlockGridConfigContext gridBlockContext, SyncMigrationContext context)
    {
        if (layouts == null) return;

        var gridLayoutConfigurations = layouts
            .ToObject<IEnumerable<GridLayoutConfiguration>>() ?? Enumerable.Empty<GridLayoutConfiguration>();

        foreach (var layout in gridLayoutConfigurations)
        {
            if (layout.Areas == null) continue;

            var rowAreas = new List<BlockGridConfiguration.BlockGridAreaConfiguration>();

            foreach (var (gridArea, gridAreaIndex) in layout.Areas.Select((x, i) => (x, i)))
            {
	            var allowed = new List<string>();
	            var cellName = "Cell";
	            if (gridArea.Allowed?.Any() == true)
	            {
		            allowed.Add(cellName);
		            allowed.AddRange(gridArea.Allowed);
	            }
	            else
	            {
		            allowed.Add("*");
	            }

				// layout.Name in old dataset. layout.Label otherwise
				// always add sections inside layouts even if they are full width
				// so no continue exception here
				// to have config on cell we need to introduce a new layout type 'cell'
				// in which to place the blocks in an area.
				// - every layout area must be allowed to only contain cell.
				// - A cell may contain all types of blocks
				var area = new BlockGridConfiguration.BlockGridAreaConfiguration
                {
                    Alias = _conventions.AreaAlias(gridAreaIndex),
                    ColumnSpan = gridArea.Grid,
                    RowSpan = 1
                };

                var alias = _conventions.LayoutAreaAlias(layout.Name, area.Alias);
                area.Key = alias.ToGuid();

                rowAreas.Add(area);
                if (allowed.Any())
                {
	                gridBlockContext.AllowedEditors[area] = allowed;
                }
                    
                // cell
                var contentTypeCellAlias = _conventions.CellContentTypeAlias(cellName);
                var cellBlock = new BlockGridConfiguration.BlockGridBlockConfiguration
                {
	                Label = cellName,
	                Areas = new List<BlockGridConfiguration.BlockGridAreaConfiguration>()
	                {
		                new BlockGridConfiguration.BlockGridAreaConfiguration
		                {
			                Alias = _conventions.AreaAlias(gridAreaIndex),
			                ColumnSpan = gridArea.Grid,
			                RowSpan = 1,
							Key = _conventions.LayoutAreaAlias("Cell" + layout.Name, _conventions.AreaAlias(gridAreaIndex)).ToGuid(),
						}
	                }.ToArray(),
	                ContentElementTypeKey = context.GetContentTypeKeyOrDefault(contentTypeCellAlias, contentTypeCellAlias.ToGuid()),
	                GroupKey = gridBlockContext.CellsGroup.Key.ToString(),
	                BackgroundColor = Grid.CellBlocks.Background,
	                IconColor = Grid.CellBlocks.Icon,
	                SettingsElementTypeKey = _conventions.SettingContentTypeAlias("cell").ToGuid(),
                };

                gridBlockContext.CellBlocks.TryAdd(contentTypeCellAlias, cellBlock);
                
                context.ContentTypes.AddNewContentType(new NewContentTypeInfo
                {
	                Key = cellBlock.ContentElementTypeKey,
	                Alias = contentTypeCellAlias,
	                Name = cellName,
	                Description = "Grid cellblock",
	                Folder = "BlockGrid/Cells",
	                Icon = "icon-application-window-alt color-purple",
	                IsElement = true
                });

            }

            if (rowAreas.Count == 0) continue;

            // layout
            var contentTypeLayoutAlias = _conventions.LayoutContentTypeAlias(layout.Name);

            if (rowAreas.Sum(a => a.ColumnSpan ?? 0) == gridBlockContext.GridColumns)
            {
	            gridBlockContext.AppendToRootLayouts(new[] { contentTypeLayoutAlias });
            }

            var layoutBlock = GetLayoutBlockGridConfiguration(gridBlockContext, context, layout?.Name, rowAreas, contentTypeLayoutAlias);

            gridBlockContext.LayoutBlocks.TryAdd(contentTypeLayoutAlias, layoutBlock);

            context.ContentTypes.AddNewContentType(GetGridLayoutContentType(
	            layoutBlock.ContentElementTypeKey, contentTypeLayoutAlias, layout?.Name ?? contentTypeLayoutAlias));
        }

    }

    private void AddContentTypesForLayoutBlocks(GridToBlockGridConfigContext gridBlockContext, SyncMigrationContext context)
    {
        var rootAllowed = gridBlockContext.GetRootAllowedLayouts();

        foreach (var (alias, block) in gridBlockContext.LayoutBlocks)
        {
            if (rootAllowed.Contains(block.Label))
            {
                block.AllowAtRoot = true;
            }

            foreach (var area in block.Areas)
            {
                var areaAllowed = gridBlockContext.GetAllowedLayouts(area);

                if (!areaAllowed.Any()) continue;

                var layoutContentTypeAliases = areaAllowed
                    .Select(x => _conventions.LayoutContentTypeAlias(x));

                var specificedAllowance = new List<BlockGridConfiguration.BlockGridAreaConfigurationSpecifiedAllowance>(area.SpecifiedAllowance);

                foreach (var layoutContentTypeAlias in layoutContentTypeAliases)
                {
                    var contentTypeKey = context.ContentTypes.GetKeyByAlias(layoutContentTypeAlias);
                    if (contentTypeKey != Guid.Empty)
                    {
                        specificedAllowance.Add(new()
                        {
                            ElementTypeKey = contentTypeKey
                        });
                    }
                }

                area.SpecifiedAllowance = specificedAllowance.ToArray();
            }

            if (block.ContentElementTypeKey == Guid.Empty)
            {
                block.ContentElementTypeKey = alias.ToGuid();
                context.ContentTypes.AddAliasAndKey(alias, block.ContentElementTypeKey);
            }

            context.ContentTypes.AddElementType(block.ContentElementTypeKey);

			// assign setting
			var contentTypeSettingAlias = _conventions.SettingContentTypeAlias("row");
			block.SettingsElementTypeKey = contentTypeSettingAlias.ToGuid();
 
        }
    }
}
