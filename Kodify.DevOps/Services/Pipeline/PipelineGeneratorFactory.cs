namespace Kodify.DevOps.Pipeline;
public class Pipelines
{
    public static IPipelineGenerator DetectCI(string projectPath = null)
    {
        var analyzer = new DevOpsAnalyzer();
        var info = analyzer.AnalyzeProjectAsync(projectPath).Result;

        // Automatically detect and return the appropriate generator
        return info.SourceControlType?.ToLower() switch
        {
            "github" => new GithubActionsGenerator(analyzer),
            "azure devops" => new AzureDevOpsGenerator(analyzer),
            "gitlab" => new GitLabCIGenerator(analyzer),
            _ => new GithubActionsGenerator(analyzer) // Default to GitHub Actions
        };
    }
} 