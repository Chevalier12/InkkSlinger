using InkkSlinger;
using System;

namespace InkkSlinger;

internal static class Program
{
	private static void Main(string[] args)
	{
		InkkSlingerUI.Initialize(
			CreateRootContent,
			new InkkSlingerOptions
			{
				WindowTitle = "InkkSlinger Controls Catalog",
				FpsEnabled = true,
				InkkOopsRuntimeOptions = App.ParseInkkOopsOptions(args)
			});

		
	}

	private static UserControl CreateRootContent()
	{
		var rootView = Environment.GetEnvironmentVariable("INKKSLINGER_DEMO_ROOT_VIEW");
		if (!string.IsNullOrWhiteSpace(rootView) && ControlViews.HasCatalogView(rootView))
		{
			return ControlViews.CreateCatalogView(rootView);
		}

		return new ControlsCatalogView();
	}
}
