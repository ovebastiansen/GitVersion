using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GitVersion;
using GitVersion.Helpers;
using GitVersionCore.Tests;
using NUnit.Framework;
using Shouldly;
using YamlDotNet.Serialization;

[TestFixture]
public class ConfigProviderTests
{
    string gitDirectory;
    IFileSystem fileSystem;

    [SetUp]
    public void Setup()
    {
        fileSystem = new TestFileSystem();
        gitDirectory = "c:\\MyGitRepo\\.git";
    }

    [Test]
    public void CanReadDocument()
    {
        const string text = @"
assembly-versioning-scheme: MajorMinor
next-version: 2.0.0
tag-prefix: '[vV|version-]'
mode: ContinuousDelivery
develop:
    mode: ContinuousDeployment
    tag: dev
release*:
   mode: ContinuousDeployment
   tag: rc 
";
        SetupConfigFileContent(text);

        var config = ConfigurationProvider.Provide(gitDirectory, fileSystem);
        config.AssemblyVersioningScheme.ShouldBe(AssemblyVersioningScheme.MajorMinor);
        config.NextVersion.ShouldBe("2.0.0");
        config.TagPrefix.ShouldBe("[vV|version-]");
        config.Release.VersioningMode.ShouldBe(VersioningMode.ContinuousDeployment);
        config.Develop.VersioningMode.ShouldBe(VersioningMode.ContinuousDeployment);
        config.VersioningMode.ShouldBe(VersioningMode.ContinuousDelivery);
        config.Release.Tag.ShouldBe("rc");
        config.Develop.Tag.ShouldBe("dev");
    }

    [Test]
    public void CanInheritVersioningMode()
    {
        const string text = @"
mode: ContinuousDelivery
develop:
    mode: ContinuousDeployment
";
        SetupConfigFileContent(text);
        var config = ConfigurationProvider.Provide(gitDirectory, fileSystem);
        config.Develop.VersioningMode.ShouldBe(VersioningMode.ContinuousDeployment); 
        config.Release.VersioningMode.ShouldBe(VersioningMode.ContinuousDelivery);
    }

    [Test]
    public void CanReadOldDocument()
    {
        const string text = @"
assemblyVersioningScheme: MajorMinor
develop-branch-tag: alpha
release-branch-tag: rc
";
        SetupConfigFileContent(text);
        var config = ConfigurationProvider.Provide(gitDirectory, fileSystem);
        config.AssemblyVersioningScheme.ShouldBe(AssemblyVersioningScheme.MajorMinor);
        config.Develop.Tag.ShouldBe("alpha");
        config.Release.Tag.ShouldBe("rc");
    }

    [Test]
    public void CanReadDefaultDocument()
    {
        const string text = "";
        SetupConfigFileContent(text);
        var config = ConfigurationProvider.Provide(gitDirectory, fileSystem);
        config.AssemblyVersioningScheme.ShouldBe(AssemblyVersioningScheme.MajorMinorPatch);
        config.Develop.Tag.ShouldBe("unstable");
        config.Release.Tag.ShouldBe("beta");
        config.TagPrefix.ShouldBe("[vV]");
        config.NextVersion.ShouldBe(null);
    }

    [Test]
    public void VerifyInit()
    {
        var config = typeof(Config);
        var aliases = config.GetProperties().Where(p => p.GetCustomAttribute<ObsoleteAttribute>() == null).Select(p => ((YamlAliasAttribute) p.GetCustomAttribute(typeof(YamlAliasAttribute))).Alias);
        var writer = new StringWriter();

        ConfigReader.WriteSample(writer);
        var initFile = writer.GetStringBuilder().ToString();

        foreach (var alias in aliases)
        {
            initFile.ShouldContain(alias);
        }
    }

    [Test]
    public void VerifyAliases()
    {
        var config = typeof(Config);
        var propertiesMissingAlias = config.GetProperties().Where(p => p.GetCustomAttribute(typeof(YamlAliasAttribute)) == null).Select(p => p.Name);

        propertiesMissingAlias.ShouldBeEmpty();
    }

    void SetupConfigFileContent(string text)
    {
        fileSystem.WriteAllText(Path.Combine(Directory.GetParent(gitDirectory).FullName, "GitVersionConfig.yaml"), text);
    }
}