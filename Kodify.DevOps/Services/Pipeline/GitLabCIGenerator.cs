using System.Text;
using Kodify.DevOps.Models;
using Kodify.Repository.Services;

namespace Kodify.DevOps.Pipeline;

public class GitLabCIGenerator : IPipelineGenerator
{
    public string PlatformName => "GitLab CI";
    private readonly DevOpsAnalyzer _analyzer;
    private readonly PipelineInfo _info;
    private readonly GitRepositoryService _gitService;
    private readonly string _defaultBranch;

    public GitLabCIGenerator()
    {
        _analyzer = new DevOpsAnalyzer();
        _gitService = new GitRepositoryService();
        _info = _analyzer.AnalyzeProjectAsync().Result;
        _defaultBranch = _gitService.GetDefaultBranch() ?? "main";
    }

    public GitLabCIGenerator(DevOpsAnalyzer analyzer)
    {
        _analyzer = analyzer;
        _gitService = new GitRepositoryService();
        _info = _analyzer.AnalyzeProjectAsync().Result;
        _defaultBranch = _gitService.GetDefaultBranch() ?? "main";
    }

    public bool SupportsProjectType(string projectType) => true;

    public async Task GenerateAsync()
    {
        var yaml = new StringBuilder();
        yaml.AppendLine("image: mcr.microsoft.com/dotnet/sdk:latest");
        yaml.AppendLine();
        yaml.AppendLine("stages:");
        yaml.AppendLine("  - build");
        yaml.AppendLine("  - test");
        if (_info.IsPackageProject)
            yaml.AppendLine("  - publish");
        yaml.AppendLine();

        yaml.AppendLine("variables:");
        yaml.AppendLine("  CONFIGURATION: Release");
        yaml.AppendLine();

        yaml.AppendLine("build:");
        yaml.AppendLine("  stage: build");
        yaml.AppendLine("  script:");
        yaml.AppendLine("    - dotnet restore");
        yaml.AppendLine("    - dotnet build --configuration $CONFIGURATION");
        yaml.AppendLine();

        if (_info.HasTests)
        {
            yaml.AppendLine("test:");
            yaml.AppendLine("  stage: test");
            yaml.AppendLine("  script:");
            yaml.AppendLine("    - dotnet test --configuration $CONFIGURATION");
            yaml.AppendLine();
        }

        if (_info.IsPackageProject)
        {
            yaml.AppendLine("publish:");
            yaml.AppendLine("  stage: publish");
            yaml.AppendLine($"  only:");
            yaml.AppendLine($"    - {_defaultBranch}");
            yaml.AppendLine("  script:");
            yaml.AppendLine("    - dotnet pack --configuration $CONFIGURATION --no-build");
            yaml.AppendLine("    - dotnet nuget push \"**/*.nupkg\" --source \"https://api.nuget.org/v3/index.json\" --api-key ${NUGET_API_KEY} --skip-duplicate");
        }

        var pipelinePath = Path.Combine(_analyzer.ProjectPath, ".gitlab-ci.yml");
        await File.WriteAllTextAsync(pipelinePath, yaml.ToString());
    }
} 