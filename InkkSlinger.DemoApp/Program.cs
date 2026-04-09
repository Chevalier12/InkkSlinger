using InkkSlinger;

namespace InkkSlinger;

internal static class Program
{
	private static void Main(string[] args)
	{
		InkkSlingerUI.Initialize(
			static () => new ControlsCatalogView(),
			new InkkSlingerOptions
			{
				WindowTitle = "InkkSlinger Controls Catalog",
				FpsEnabled = true,
				InkkOopsRuntimeOptions = App.ParseInkkOopsOptions(args)
			});

		
	}
}
