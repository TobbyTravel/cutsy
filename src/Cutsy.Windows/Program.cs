namespace Cutsy.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var cutsy = new MascotContext();
        Application.Run(cutsy);
    }
}
