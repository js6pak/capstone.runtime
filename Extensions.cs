using System;
using System.IO;
using System.Threading.Tasks;
using Cake.Common;
using Cake.Core;
using Cake.Core.IO;
using NuGet.Packaging;

namespace Build;

internal static class Extensions
{
    public static ProcessArgumentBuilder AppendDefine(this ProcessArgumentBuilder builder, string @switch, string text)
    {
        return builder.AppendSwitch("-D" + @switch, "=", text);
    }

    public static void StartProcessOrThrow(this ICakeContext context, FilePath fileName, ProcessSettings settings)
    {
        var process = context.StartAndReturnProcess(fileName, settings);

        process.WaitForExit();

        var exitCode = process.GetExitCode();
        if (exitCode != 0)
        {
            throw new Exception($"Process {fileName} failed with exit code {exitCode}");
        }
    }


    public static async Task SaveAsync(this PackageBuilder packageBuilder, string path)
    {
        await using var stream = File.Create(path);
        packageBuilder.Save(stream);
    }

    public static async Task SaveAsync(this PackageBuilder packageBuilder, DirectoryPath directory)
    {
        var path = directory.CombineWithFilePath($"{packageBuilder.Id}.{packageBuilder.Version}.nupkg").FullPath;
        await packageBuilder.SaveAsync(path);
    }
}
