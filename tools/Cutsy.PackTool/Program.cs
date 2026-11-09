using Cutsy.Core;
using Cutsy.PackIO;

if (args is not ["validate", var root])
{
    Console.Error.WriteLine("Usage: Cutsy.PackTool validate <pack-directory>");
    return 2;
}

try
{
    var pack = PackLoader.Load(new PackDirectory(root).Read);
    Console.WriteLine($"{pack.Manifest.DisplayName} {pack.Manifest.Version}: {pack.Sprites.Count} sprites, " +
        $"{pack.Behaviors.Count} behaviors, {pack.Sounds.Count} usable sounds");
    foreach (string warning in pack.Warnings)
        Console.Error.WriteLine(warning);
    return 0;
}
catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
