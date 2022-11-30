using System;
using System.Runtime.InteropServices;
using Build;
using Cake.Core.IO;

public record Target(OSPlatform Platform, Architecture Architecture)
{
    public bool CanBuild => RuntimeInformation.IsOSPlatform(Platform);

    public string RuntimeIdentifier { get; } =
        Platform switch
        {
            _ when Platform == OSPlatform.Linux => "linux",
            _ when Platform == OSPlatform.Windows => "win",
            _ when Platform == OSPlatform.OSX => "osx",
            _ => throw new ArgumentOutOfRangeException(nameof(Platform), Platform, null),
        }
        + "-" +
        Architecture switch
        {
            Architecture.X86 => "x86",
            Architecture.X64 => "x64",
            Architecture.Arm => "arm",
            Architecture.Arm64 => "arm64",
            _ => throw new ArgumentOutOfRangeException(nameof(Architecture), Architecture, null),
        };

    public ProcessArgumentBuilder ConfigureCMake(ProcessArgumentBuilder builder)
    {
        builder
            .AppendDefine("CMAKE_SYSTEM_NAME", Platform switch
            {
                _ when Platform == OSPlatform.Linux => "Linux",
                _ when Platform == OSPlatform.Windows => "Windows",
                _ when Platform == OSPlatform.OSX => "Darwin",
                _ => throw new ArgumentOutOfRangeException(nameof(Platform), Platform, null),
            });

        if (Platform == OSPlatform.Linux)
        {
            if (Architecture == Architecture.X86)
            {
                builder.AppendDefine("CMAKE_C_FLAGS", "-m32").AppendDefine("CMAKE_CXX_FLAGS", "-m32");
            }
            else if (Architecture != Architecture.X64)
            {
                var toolchain = Architecture switch
                {
                    Architecture.Arm64 => "aarch64-linux-gnu",
                    Architecture.Arm => "arm-linux-gnueabihf",
                    _ => throw new ArgumentOutOfRangeException(),
                };

                string GetToolchainBinary(string name) => $"/bin/{toolchain}-{name}";

                builder.AppendDefine("CMAKE_C_COMPILER", GetToolchainBinary("gcc"))
                    .AppendDefine("CMAKE_CXX_COMPILER", GetToolchainBinary("g++"))
                    .AppendDefine("CMAKE_AR", GetToolchainBinary("ar"))
                    .AppendDefine("CMAKE_RANLIB", GetToolchainBinary("ranlib"))
                    .AppendDefine("CMAKE_STRIP", GetToolchainBinary("strip"));
            }
        }
        else if (Platform == OSPlatform.OSX)
        {
            builder.AppendDefine("CMAKE_OSX_ARCHITECTURES", Architecture switch
            {
                Architecture.X64 => "x86_64",
                Architecture.Arm64 => "arm64",
                _ => throw new ArgumentOutOfRangeException(),
            });
        }
        else if (Platform == OSPlatform.Windows)
        {
            builder.AppendDefine("CMAKE_GENERATOR_PLATFORM", Architecture switch
            {
                Architecture.X64 => "x64",
                Architecture.X86 => "Win32",
                Architecture.Arm64 => "ARM64",
                _ => throw new ArgumentOutOfRangeException(),
            });
        }

        return builder;
    }
}
