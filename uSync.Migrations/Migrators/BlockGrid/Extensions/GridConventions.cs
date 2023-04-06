using Umbraco.Cms.Core.Strings;
using static Umbraco.Cms.Core.PropertyEditors.ListViewConfiguration;

namespace uSync.Migrations.Migrators.BlockGrid.Extensions;

internal class GridConventions
{
    public IShortStringHelper ShortStringHelper { get; }

    public GridConventions(IShortStringHelper shortStringHelper)
    {
        ShortStringHelper = shortStringHelper;
    }

    public string AreaAlias(int index)
        => $"area_{index}";

    public string SectionContentTypeAlias(string? name)
        => $"section_{name}".GetBlockGridLayoutContentTypeAlias(ShortStringHelper);

    public string RowLayoutContentTypeAlias(string? name)
        => $"{name}".GetBlockElementContentTypeAlias(ShortStringHelper);

    public string GridAreaConfigAlias(string areaAlias)
        => areaAlias.GetBlockGridAreaConfigurationAlias(ShortStringHelper);

    public string TemplateContentTypeAlias(string template)
        => template.GetBlockElementContentTypeAlias(ShortStringHelper);

    public string LayoutAreaAlias(string layout, string areaAlias)
        => $"layout_{layout}_{areaAlias}".GetBlockGridAreaConfigurationAlias(ShortStringHelper);

    public string ColumnLayoutAreaAlias()
	    => LayoutAreaAlias("Column", AreaAlias(0));

    public string LayoutContentTypeAlias(string layout)
        => layout.GetBlockGridLayoutContentTypeAlias(ShortStringHelper);

    public string ColumnContentTypeAlias(string columnLayoutAlias)
	    => columnLayoutAlias.GetBlockGridCellContentTypeAlias(ShortStringHelper);

    public string SettingContentTypeAlias(string settingAlias)
	    => settingAlias.GetBlockGridSettingContentTypeAlias(ShortStringHelper);
}
