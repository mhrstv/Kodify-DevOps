using Kodify.DevOps.Models;
using System.Text;
using System.IO;
namespace Kodify.DevOps.IaC;
public class TerraformGenerator : IIaCGenerator
{
    public string PlatformName => "Terraform";
    private DevOpsAnalyzer _analyzer;
    private PipelineInfo _info;
    public TerraformGenerator()
    {
        _analyzer = new DevOpsAnalyzer();
        _info = _analyzer.AnalyzeProjectAsync().Result;
    }
    public TerraformGenerator(DevOpsAnalyzer analyzer)
    {
        _analyzer = analyzer;
        _info = _analyzer.AnalyzeProjectAsync().Result;
    }

    public async Task GenerateInfrastructureTemplateAsync()
    {
        var template = new StringBuilder();
        template.AppendLine("terraform {");
        template.AppendLine("  required_providers {");
        template.AppendLine("    aws = {");
        template.AppendLine("      source  = \"hashicorp/aws\"");
        template.AppendLine("      version = \"~> 4.0\"");
        template.AppendLine("    }");
        template.AppendLine("  }");
        template.AppendLine("}");

        // Add provider configuration
        template.AppendLine("\nprovider \"aws\" {");
        template.AppendLine("  region = var.aws_region");
        template.AppendLine("}");

        // Add resources based on requirements
        if (_info.Environment.RequiresDatabase)
        {
            // Add RDS configuration
            template.AppendLine("\n# RDS Instance");
            template.AppendLine("resource \"aws_db_instance\" \"main\" {");
            template.AppendLine("  // Add RDS configuration here");
            template.AppendLine("}");
        }

        if (_info.Environment.RequiresDocker)
        {
            // Add ECS/EKS configuration
            template.AppendLine("\n# ECS Cluster");
            template.AppendLine("resource \"aws_ecs_cluster\" \"main\" {");
            template.AppendLine("  // Add ECS configuration here");
            template.AppendLine("}");
        }

        // Create infrastructure directory if it doesn't exist
        var infrastructurePath = Path.Combine(_analyzer.ProjectPath, "infrastructure");
        Directory.CreateDirectory(infrastructurePath);

        // Write the terraform configuration
        await File.WriteAllTextAsync(
            Path.Combine(infrastructurePath, "main.tf"), 
            template.ToString()
        );
    }
}