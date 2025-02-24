using Kodify.DevOps.Models;
using System.Text;
namespace Kodify.DevOps.IaC;
public class TerraformGenerator : IIaCGenerator
{
    public string PlatformName => "Terraform";
    private readonly DevOpsAnalyzer _analyzer;
    private readonly PipelineInfo _info;
    private readonly string _projectName;
    private readonly Dictionary<string, string> _detectedServices;

    public TerraformGenerator(DevOpsAnalyzer analyzer = null)
    {
        _analyzer = analyzer ?? new DevOpsAnalyzer();
        _info = _analyzer.AnalyzeProjectAsync().Result;
        _projectName = _info.PackageId ?? Path.GetFileName(_analyzer.ProjectPath);
        _detectedServices = DetectRequiredServices();
    }

    private Dictionary<string, string> DetectRequiredServices()
    {
        var services = new Dictionary<string, string>();
        
        // Detect database type from project
        if (_info.Environment.RequiresDatabase)
        {
            if (_info.Dependencies.Any(d => d.Contains("Npgsql")))
                services["database"] = "postgres";
            else if (_info.Dependencies.Any(d => d.Contains("SqlClient")))
                services["database"] = "sqlserver";
            else if (_info.Dependencies.Any(d => d.Contains("MySql")))
                services["database"] = "mysql";
        }

        // Detect caching requirements
        if (_info.Dependencies.Any(d => d.Contains("StackExchange.Redis")))
            services["cache"] = "redis";
        
        // Detect message queue requirements
        if (_info.Dependencies.Any(d => d.Contains("RabbitMQ")))
            services["queue"] = "rabbitmq";
        else if (_info.Dependencies.Any(d => d.Contains("AWSSQS")))
            services["queue"] = "sqs";

        // Detect storage requirements
        if (_info.Dependencies.Any(d => d.Contains("AWSS3")))
            services["storage"] = "s3";

        return services;
    }

    public async Task GenerateTemplateAsync()
    {
        var template = new StringBuilder();
        
        template.AppendLine("terraform {");
        template.AppendLine("  required_version = \">= 1.0.0\"");
        template.AppendLine("  required_providers {");
        template.AppendLine("    aws = {");
        template.AppendLine("      source  = \"hashicorp/aws\"");
        template.AppendLine("      version = \"~> 4.0\"");
        template.AppendLine("    }");
        template.AppendLine("  }");
        template.AppendLine("  backend \"s3\" {}");
        template.AppendLine("}");

        // Add common variables
        template.AppendLine("\n# Common Variables");
        template.AppendLine("variable \"aws_region\" {");
        template.AppendLine("  description = \"AWS region\"");
        template.AppendLine("  type        = string");
        template.AppendLine("  default     = \"us-east-1\"");
        template.AppendLine("}");

        template.AppendLine("\nvariable \"environment\" {");
        template.AppendLine("  description = \"Environment name (dev, staging, prod)\"");
        template.AppendLine("  type        = string");
        template.AppendLine("}");

        template.AppendLine("\nvariable \"project_name\" {");
        template.AppendLine("  description = \"Project name\"");
        template.AppendLine($"  default     = \"{_projectName}\"");
        template.AppendLine("}");

        // Add environment-specific variables
        if (_detectedServices.ContainsKey("database"))
        {
            template.AppendLine("\n# Database Variables");
            template.AppendLine("variable \"db_instance_class\" {");
            template.AppendLine("  description = \"Database instance class\"");
            template.AppendLine("  type        = string");
            template.AppendLine("  default     = \"db.t3.micro\"");
            template.AppendLine("}");
        }

        // Add provider configuration with default tags
        template.AppendLine("\n# Provider Configuration");
        template.AppendLine("provider \"aws\" {");
        template.AppendLine("  region = var.aws_region");
        template.AppendLine("  default_tags {");
        template.AppendLine("    tags = {");
        template.AppendLine("      Environment = var.environment");
        template.AppendLine("      Project     = var.project_name");
        template.AppendLine("      ManagedBy   = \"terraform\"");
        template.AppendLine($"      Application = \"{_info.PackageId}\"");
        template.AppendLine($"      Version     = \"{_info.Version}\"");
        template.AppendLine("    }");
        template.AppendLine("  }");
        template.AppendLine("}");

        // Add data sources for existing resources
        template.AppendLine("\n# Data Sources");
        template.AppendLine("data \"aws_availability_zones\" \"available\" {");
        template.AppendLine("  state = \"available\"");
        template.AppendLine("}");

        // Add networking with best practices
        template.AppendLine("\n# VPC Configuration");
        template.AppendLine("module \"vpc\" {");
        template.AppendLine("  source = \"terraform-aws-modules/vpc/aws\"");
        template.AppendLine("  version = \"~> 4.0\"");
        template.AppendLine("  name = \"${var.project_name}-${var.environment}\"");
        template.AppendLine("  cidr = \"10.0.0.0/16\"");
        template.AppendLine("  azs = slice(data.aws_availability_zones.available.names, 0, 3)");
        template.AppendLine("  private_subnets = [\"10.0.1.0/24\", \"10.0.2.0/24\", \"10.0.3.0/24\"]");
        template.AppendLine("  public_subnets  = [\"10.0.101.0/24\", \"10.0.102.0/24\", \"10.0.103.0/24\"]");
        template.AppendLine("  enable_nat_gateway = true");
        template.AppendLine("  single_nat_gateway = var.environment != \"prod\"");
        template.AppendLine("  enable_vpn_gateway = false");
        template.AppendLine("  enable_dns_hostnames = true");
        template.AppendLine("  enable_dns_support = true");
        template.AppendLine("}");

        // Add detected services
        if (_detectedServices.TryGetValue("database", out var dbType))
        {
            template.AppendLine("\n# RDS Configuration");
            template.AppendLine("module \"rds\" {");
            template.AppendLine("  source = \"terraform-aws-modules/rds/aws\"");
            template.AppendLine("  version = \"~> 5.0\"");
            template.AppendLine("  identifier = \"${var.project_name}-${var.environment}\"");
            template.AppendLine($"  engine = \"{dbType}\"");
            template.AppendLine($"  engine_version = \"{GetLatestEngineVersion(dbType)}\"");
            template.AppendLine("  instance_class = var.db_instance_class");
            template.AppendLine("  allocated_storage = 20");
            template.AppendLine("  db_name = replace(var.project_name, \"-\", \"_\")");
            template.AppendLine("  vpc_security_group_ids = [aws_security_group.rds.id]");
            template.AppendLine("  subnet_ids = module.vpc.private_subnets");
            template.AppendLine("  multi_az = var.environment == \"prod\"");
            template.AppendLine("  backup_retention_period = var.environment == \"prod\" ? 30 : 7");
            template.AppendLine("  deletion_protection = var.environment == \"prod\"");
            template.AppendLine("}");

            // Add security group for RDS
            template.AppendLine("\nresource \"aws_security_group\" \"rds\" {");
            template.AppendLine("  name = \"${var.project_name}-${var.environment}-rds\"");
            template.AppendLine("  vpc_id = module.vpc.vpc_id");
            template.AppendLine("  ingress {");
            template.AppendLine($"    from_port = {GetDefaultDbPort(dbType)}");
            template.AppendLine($"    to_port = {GetDefaultDbPort(dbType)}");
            template.AppendLine("    protocol = \"tcp\"");
            template.AppendLine("    cidr_blocks = module.vpc.private_subnets_cidr_blocks");
            template.AppendLine("  }");
            template.AppendLine("}");
        }

        if (_info.Environment.RequiresDocker)
        {
            template.AppendLine("\n# ECS Configuration");
            template.AppendLine("module \"ecs\" {");
            template.AppendLine("  source = \"terraform-aws-modules/ecs/aws\"");
            template.AppendLine("  version = \"~> 4.0\"");
            template.AppendLine("  cluster_name = \"${var.project_name}-${var.environment}\"");
            template.AppendLine("  cluster_configuration = {");
            template.AppendLine("    execute_command_configuration = {");
            template.AppendLine("      logging = \"OVERRIDE\"");
            template.AppendLine("      log_configuration = {");
            template.AppendLine("        cloud_watch_log_group_name = \"/aws/ecs/${var.project_name}\"");
            template.AppendLine("      }");
            template.AppendLine("    }");
            template.AppendLine("  }");
            template.AppendLine("  fargate_capacity_providers = {");
            template.AppendLine("    FARGATE = {");
            template.AppendLine("      default_capacity_provider_strategy = {");
            template.AppendLine("        weight = 50");
            template.AppendLine("      }");
            template.AppendLine("    }");
            template.AppendLine("    FARGATE_SPOT = {");
            template.AppendLine("      default_capacity_provider_strategy = {");
            template.AppendLine("        weight = var.environment == \"prod\" ? 0 : 50");
            template.AppendLine("      }");
            template.AppendLine("    }");
            template.AppendLine("  }");
            template.AppendLine("}");
        }

        // Add detected cache if needed
        if (_detectedServices.TryGetValue("cache", out var cacheType) && cacheType == "redis")
        {
            template.AppendLine("\n# ElastiCache Configuration");
            template.AppendLine("module \"elasticache\" {");
            template.AppendLine("  source = \"terraform-aws-modules/elasticache/aws\"");
            template.AppendLine("  version = \"~> 3.0\"");
            template.AppendLine("  cluster_id = \"${var.project_name}-${var.environment}\"");
            template.AppendLine("  engine = \"redis\"");
            template.AppendLine("  engine_version = \"7.0\"");
            template.AppendLine("  node_type = \"cache.t3.micro\"");
            template.AppendLine("  num_cache_nodes = 1");
            template.AppendLine("  subnet_ids = module.vpc.private_subnets");
            template.AppendLine("  security_group_ids = [aws_security_group.redis.id]");
            template.AppendLine("}");
        }

        // Add outputs
        template.AppendLine("\n# Outputs");
        template.AppendLine("output \"vpc_id\" {");
        template.AppendLine("  value = module.vpc.vpc_id");
        template.AppendLine("}");

        if (_detectedServices.ContainsKey("database"))
        {
            template.AppendLine("\noutput \"database_endpoint\" {");
            template.AppendLine("  value = module.rds.db_instance_endpoint");
            template.AppendLine("  sensitive = true");
            template.AppendLine("}");
        }

        if (_info.Environment.RequiresDocker)
        {
            template.AppendLine("\noutput \"ecs_cluster_id\" {");
            template.AppendLine("  value = module.ecs.cluster_id");
            template.AppendLine("}");
        }

        // Create infrastructure files
        var infraPath = Path.Combine(_analyzer.ProjectPath, "iac");
        Directory.CreateDirectory(infraPath);

        // Write main configuration
        await File.WriteAllTextAsync(
            Path.Combine(infraPath, "main.tf"), 
            template.ToString()
        );

        // Create environment-specific variable files
        await CreateEnvironmentVariableFiles(infraPath);

        // Create backend configurations for each environment
        var environments = new[] { "dev", "staging", "prod" };
        foreach (var env in environments)
        {
            var backendConfig = new StringBuilder();
            backendConfig.AppendLine($"bucket         = \"{_projectName}-terraform-state\"");
            backendConfig.AppendLine($"key            = \"{env}/terraform.tfstate\"");
            backendConfig.AppendLine("region         = \"us-east-1\"");
            backendConfig.AppendLine("encrypt        = true");
            backendConfig.AppendLine($"dynamodb_table = \"{_projectName}-terraform-lock\"");

            await File.WriteAllTextAsync(
                Path.Combine(infraPath, $"backend.{env}.hcl"),
                backendConfig.ToString()
            );
        }

        // Create comprehensive README
        await GenerateReadme(infraPath);
    }

    private string GetLatestEngineVersion(string dbType) => dbType switch
    {
        "postgres" => "14",
        "mysql" => "8.0",
        "sqlserver" => "15.00",
        _ => "14"
    };

    private int GetDefaultDbPort(string dbType) => dbType switch
    {
        "postgres" => 5432,
        "mysql" => 3306,
        "sqlserver" => 1433,
        _ => 5432
    };

    private async Task CreateEnvironmentVariableFiles(string infraPath)
    {
        var environments = new[] { "dev", "staging", "prod" };
        
        foreach (var env in environments)
        {
            var vars = new StringBuilder();
            vars.AppendLine($"environment = \"{env}\"");
            vars.AppendLine("aws_region = \"us-east-1\"");

            if (_detectedServices.ContainsKey("database"))
            {
                vars.AppendLine($"db_instance_class = \"{GetDbInstanceClass(env)}\"");
            }

            await File.WriteAllTextAsync(
                Path.Combine(infraPath, $"{env}.tfvars"),
                vars.ToString()
            );
        }
    }

    private string GetDbInstanceClass(string environment) => environment switch
    {
        "prod" => "db.t3.small",
        "staging" => "db.t3.micro",
        _ => "db.t3.micro"
    };

    private async Task GenerateReadme(string infraPath)
    {
        var readme = new StringBuilder();
        readme.AppendLine("# Infrastructure as Code");
        readme.AppendLine("\n## Project Infrastructure");
        readme.AppendLine($"This infrastructure is automatically generated for {_projectName} based on project analysis.");
        readme.AppendLine("\nDetected requirements:");
        
        foreach (var service in _detectedServices)
        {
            readme.AppendLine($"- {service.Key}: {service.Value}");
        }

        readme.AppendLine("\n## Prerequisites");
        readme.AppendLine("- Terraform >= 1.0.0");
        readme.AppendLine("- AWS CLI configured");
        
        readme.AppendLine("\n## Quick Start");
        readme.AppendLine("1. Create S3 bucket and DynamoDB table for Terraform state:");
        readme.AppendLine("   ```bash");
        readme.AppendLine($"   aws s3 mb s3://{_projectName}-terraform-state");
        readme.AppendLine($"   aws dynamodb create-table \\");
        readme.AppendLine($"     --table-name {_projectName}-terraform-lock \\");
        readme.AppendLine("     --attribute-definitions AttributeName=LockID,AttributeType=S \\");
        readme.AppendLine("     --key-schema AttributeName=LockID,KeyType=HASH \\");
        readme.AppendLine("     --provisioned-throughput ReadCapacityUnits=1,WriteCapacityUnits=1");
        readme.AppendLine("   ```");

        readme.AppendLine("\n2. Initialize Terraform for your environment:");
        readme.AppendLine("   ```bash");
        readme.AppendLine("   # For development");
        readme.AppendLine("   terraform init -backend-config=backend.dev.hcl");
        readme.AppendLine("   ");
        readme.AppendLine("   # For staging");
        readme.AppendLine("   terraform init -backend-config=backend.staging.hcl");
        readme.AppendLine("   ");
        readme.AppendLine("   # For production");
        readme.AppendLine("   terraform init -backend-config=backend.prod.hcl");
        readme.AppendLine("   ```");

        readme.AppendLine("\n3. Create a workspace for your environment:");
        readme.AppendLine("   ```bash");
        readme.AppendLine("   terraform workspace new dev");
        readme.AppendLine("   ```");

        readme.AppendLine("\n4. Apply the configuration:");
        readme.AppendLine("   ```bash");
        readme.AppendLine("   terraform apply -var-file=dev.tfvars");
        readme.AppendLine("   ```");

        readme.AppendLine("\n## Environment-Specific Configurations");
        readme.AppendLine("- `dev.tfvars`: Development environment settings");
        readme.AppendLine("- `staging.tfvars`: Staging environment settings");
        readme.AppendLine("- `prod.tfvars`: Production environment settings");

        readme.AppendLine("\n## Best Practices");
        readme.AppendLine("- Use workspaces for environment separation");
        readme.AppendLine("- Store state remotely in S3 with DynamoDB locking");
        readme.AppendLine("- Use different instance sizes per environment");
        readme.AppendLine("- Enable multi-AZ for production");
        readme.AppendLine("- Use Fargate Spot in non-production environments");

        await File.WriteAllTextAsync(
            Path.Combine(infraPath, "README.md"),
            readme.ToString()
        );
    }
}