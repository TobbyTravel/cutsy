namespace Cutsy.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        MessageBox.Show("The interactive mascot is implemented in C03.", "Cutsy");
    }
}
