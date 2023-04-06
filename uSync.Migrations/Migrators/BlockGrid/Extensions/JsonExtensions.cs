using Newtonsoft.Json.Linq;

namespace uSync.Migrations.Migrators.BlockGrid.Extensions
{
	internal static class JsonExtensions
	{
		public static bool IsNullOrEmpty(this JToken? token)
		{
			return (token == null) ||
			       token is { Type: JTokenType.Array, HasValues: false } ||
			       token is { Type: JTokenType.Object, HasValues: false } ||
			       (token.Type == JTokenType.String && string.IsNullOrEmpty(token.ToString())) ||
			       (token.Type == JTokenType.Null);
		}
	}
}
