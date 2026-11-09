namespace Cutsy.PackIO;

internal sealed class PackDirectory(string root)
{
    private readonly string fullRoot = Path.GetFullPath(root);

    internal byte[]? Read(string relativePath, int limit)
    {
        // PackLoader already validates the wire path. Recheck containment for direct asset reads.
        string file = Path.GetFullPath(relativePath, fullRoot);
        string prefix = Path.TrimEndingDirectorySeparator(fullRoot) + Path.DirectorySeparatorChar;
        if (!file.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException($"{relativePath}: path escapes the pack");
        try
        {
            string current = fullRoot;
            foreach (string component in relativePath.Split('/'))
            {
                current = Path.Combine(current, component);
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException($"{relativePath}: linked files and directories are not allowed");
            }

            using var stream = File.OpenRead(file);
            if (stream.Length > limit)
                throw new InvalidDataException($"{relativePath}: exceeds {limit} bytes");
            byte[] data = new byte[checked((int)stream.Length)];
            stream.ReadExactly(data);
            return data;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }
}
