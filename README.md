# Kodify.DevOps

A Kodify subpackage that provides automated DevOps tooling for .NET projects. This package automatically generates Infrastructure as Code (IaC) and CI/CD pipelines based on project analysis.

## Features

### CI/CD Pipeline Generation

Automatically generates CI/CD pipelines with a single line of code:
```csharp
var pipeline = Pipelines.DetectCI();
await pipeline.GenerateAsync();
```

Supports multiple CI/CD platforms:
- GitHub Actions
- Azure DevOps
- GitLab CI

Features:
- Automatic source control detection
- Framework version detection
- Test pipeline configuration
- NuGet package publishing
- Branch-specific workflows

### Infrastructure as Code (IaC)

Generates cloud infrastructure templates based on project dependencies:

```csharp
// For AWS CloudFormation
var cloudformation = new CloudFormationGenerator();
await cloudformation.GenerateTemplateAsync();

// For Terraform
var terraform = new TerraformGenerator();
await terraform.GenerateTemplateAsync();
```

Features:
- Automatic service detection:
  - Databases (PostgreSQL, MySQL, SQL Server)
  - Caching (Redis)
  - Message Queues (RabbitMQ, SQS)
  - Storage (S3)
- Environment-specific configurations
- Best practices implementation
- Complete documentation generation

## Installation

```bash
dotnet add package Kodify.DevOps
```

## Quick Start

1. Generate CI/CD pipeline:
```csharp
using Kodify.DevOps.Pipeline;

// Automatically detects and generates appropriate CI/CD pipeline
await Pipelines.DetectCI().GenerateAsync();
```

2. Generate IaC templates:
```csharp
using Kodify.DevOps.IaC;

// Generate Terraform or CloudFormation templates
var generator = new TerraformGenerator();
await generator.GenerateTemplateAsync();
```

## Generated Files

### CI/CD Pipelines
- `.github/workflows/ci-cd.yml` (GitHub Actions)
- `azure-pipelines.yml` (Azure DevOps)
- `.gitlab-ci.yml` (GitLab CI)

### Infrastructure
- `/iac` directory containing:
  - Infrastructure templates
  - Environment-specific configurations
  - Backend configurations
  - Comprehensive README

## Dependencies

This package is part of the Kodify ecosystem and requires:
- Kodify (>= 0.1.8)
- .NET 8.0 or later

## Best Practices

The generated configurations follow cloud provider best practices:
- Multi-AZ deployment for production
- Environment-specific instance sizing
- Proper security group configurations
- State file management
- Backup retention policies
- Cost optimization for non-production environments

## References

This is a subpackage of [Kodify](https://github.com/mhrstv/Kodify). Please see the main package for more information.

## License

MIT License - see the main Kodify repository for details.