using InkkSlinger;

namespace InkkSlinger;

internal static class Program
{
	private static void Main(string[] args)
	{
		InkkSlingerUI.Initialize(static () => new ControlsCatalogView(), App.CreateOptions(args));
	}
}
