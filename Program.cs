using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Build;
using Cake.Common;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

public static class Program
{
    public static int Main(string[] args)
    {
        return new CakeHost()
            .UseContext<BuildContext>()
            .Run(args);
    }
}

public class BuildContext : FrostingContext
{
    public const string CapstoneVersion = "4.0.2";

    public string Version { get; }
    public DirectoryPath RepositoryPath { get; }
    public DirectoryPath TopBuildPath { get; }

    public Target[] Targets { get; } =
    {
        new(OSPlatform.Linux, Architecture.X64),
        new(OSPlatform.Linux, Architecture.X86),
        new(OSPlatform.Linux, Architecture.Arm64),
        new(OSPlatform.Linux, Architecture.Arm),

        new(OSPlatform.Windows, Architecture.X64),
        new(OSPlatform.Windows, Architecture.X86),
        new(OSPlatform.Windows, Architecture.Arm64),

        new(OSPlatform.OSX, Architecture.X64),
        new(OSPlatform.OSX, Architecture.Arm64),
    };

    public BuildContext(ICakeContext context)
        : base(context)
    {
        Version = context.Argument("Version", CapstoneVersion);
        RepositoryPath = context.Directory("capstone");
        TopBuildPath = RepositoryPath.Combine("build");
    }
}

[TaskName("Clean")]
public sealed class CleanTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.EnsureDirectoryDoesNotExist(context.TopBuildPath);
    }
}

[TaskName("Build")]
public sealed class BuildTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.Information("Cloning capstone repository");

        context.EnsureDirectoryExists(context.TopBuildPath);

        foreach (var target in context.Targets)
        {
            if (!target.CanBuild) continue;

            context.Information($"Building for {target.RuntimeIdentifier}");

            var buildPath = context.TopBuildPath.Combine(target.RuntimeIdentifier);

            context.EnsureDirectoryDoesNotExist(buildPath);
            context.CreateDirectory(buildPath);

            {
                var argumentBuilder = new ProcessArgumentBuilder()
                    .AppendDefine("CMAKE_BUILD_TYPE", "RELEASE")
                    .AppendDefine("CAPSTONE_BUILD_STATIC", "OFF")
                    .AppendDefine("CAPSTONE_BUILD_TESTS", "OFF")
                    .AppendDefine("CAPSTONE_BUILD_CSTOOL", "OFF");

                target.ConfigureCMake(argumentBuilder);

                argumentBuilder.Append("../..");

                context.StartProcessOrThrow("cmake", new ProcessSettings
                {
                    WorkingDirectory = buildPath,
                    Arguments = argumentBuilder,
                });
            }

            context.StartProcessOrThrow("cmake", new ProcessSettings
            {
                WorkingDirectory = buildPath,
                Arguments = new ProcessArgumentBuilder()
                    .AppendSwitch("--build", ".")
                    .AppendSwitch("--config", "Release")
                    .AppendSwitch("-j", Environment.ProcessorCount.ToString()),
            });

            if (target.Platform == OSPlatform.Windows) buildPath = buildPath.Combine("Release");

            var nativeOutPath = context.Directory($"out/runtimes/{target.RuntimeIdentifier}/native");
            context.EnsureDirectoryExists(nativeOutPath);

            context.CopyFileToDirectory(buildPath.CombineWithFilePath(target.Platform switch
            {
                _ when target.Platform == OSPlatform.Linux => "libcapstone.so",
                _ when target.Platform == OSPlatform.OSX => "libcapstone.dylib",
                _ when target.Platform == OSPlatform.Windows => "capstone.dll",
                _ => throw new ArgumentOutOfRangeException(),
            }), nativeOutPath);
        }
    }
}

[TaskName("Package")]
public sealed class PackageTask : AsyncFrostingTask<BuildContext>
{
    public override async Task RunAsync(BuildContext context)
    {
        PackageBuilder CreatePackageBuilder(string id, string description)
        {
            var packageBuilder = new PackageBuilder
            {
                Id = id,
                Description = description,
                Version = NuGetVersion.Parse(context.Version),
                Authors = { "js6pak" },
                RequireLicenseAcceptance = true,
                LicenseMetadata = new LicenseMetadata(LicenseType.File, "LICENSE.TXT", null, null, LicenseMetadata.EmptyVersion),
                Copyright = "Copyright (c) 2013, COSEINC",
                Repository = new RepositoryMetadata("git", "https://github.com/js6pak/capstone.runtime", null, null),
            };

            packageBuilder.AddFiles(context.RepositoryPath.FullPath, "LICENSE.TXT", "");

            return packageBuilder;
        }

        // TODO consider splitting the packages using runtime.json like in this commit: 8e5ab0319d179711ffe3d09873e1a47701363966, after following issues are solved:
        // https://github.com/dotnet/ClangSharp/issues/118
        // https://github.com/NuGet/Home/issues/6083

        var outPath = context.Directory("out");

        context.EnsureDirectoryExists(outPath);

        var packageBuilder = CreatePackageBuilder("capstone", "Multi-platform native library for capstone.");

        packageBuilder.AddFiles(outPath, "runtimes/**", "runtimes");

        await packageBuilder.SaveAsync(outPath.Path);
    }
}

[TaskName("Default")]
[IsDependentOn(typeof(CleanTask))]
[IsDependentOn(typeof(BuildTask))]
public sealed class DefaultTask : FrostingTask
{
}
