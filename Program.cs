using Pulumi;
using Pulumi.Aws.ApiGateway;
using Pulumi.Aws.ApiGateway.Inputs;
using Pulumi.Aws.DynamoDB;
using Pulumi.Aws.DynamoDB.Inputs;
using Pulumi.Aws.CloudWatch;
using Pulumi.Aws.Iam;
using Pulumi.Aws.Lambda;
using Pulumi.Aws.Lambda.Inputs;
using Pulumi.Aws.SecretsManager;
using System;
using System.Collections.Generic;
using System.Text.Json;

return await Pulumi.Deployment.RunAsync(() =>
{
    // Create API Gateway REST API
    var api = new RestApi("contact-form-api", new()
    {
        Name = "contact-form-api",
        Description = "API Gateway for contact form submissions",
        EndpointConfiguration = new RestApiEndpointConfigurationArgs
        {
            Types = "REGIONAL",
        },
        Tags =
        {
            { "Environment", "production" },
            { "Application", "contact-form" },
            { "Purpose", "ContactFormAPI" },
        },
    });

    // Create API Gateway Resource for /contact
    var contactResource = new Pulumi.Aws.ApiGateway.Resource("contact-resource", new()
    {
        RestApi = api.Id,
        ParentId = api.RootResourceId,
        PathPart = "contact",
    });

    // Create API Gateway Method for POST /contact
    var contactMethod = new Method("contact-method", new()
    {
        RestApi = api.Id,
        ResourceId = contactResource.Id,
        HttpMethod = "POST",
        Authorization = "NONE",
        ApiKeyRequired = true, // Require API key for this method
    });

    // Create API Key
    var apiKey = new ApiKey("contact-form-api-key", new()
    {
        Name = "contact-form-api-key",
        Description = "API Key for contact form API",
        Tags =
        {
            { "Environment", "production" },
            { "Application", "contact-form" },
            { "Purpose", "APIAccess" },
        },
    });

    // Create DynamoDB table
    var contact_form_table = new Table("contact-form-table", new()
    {
        Name = "ContactMessages",
        BillingMode = "PAY_PER_REQUEST", 
        HashKey = "id",
        RangeKey = "submittedAt",
        Attributes = new[]
        {
            new TableAttributeArgs
            {
                Name = "id",
                Type = "S", 
            },
            new TableAttributeArgs
            {
                Name = "submittedAt",
                Type = "S", 
            },
            new TableAttributeArgs
            {
                Name = "email",
                Type = "S", 
            },
            new TableAttributeArgs
            {
                Name = "subject",
                Type = "S", 
            },
            new TableAttributeArgs
            {
                Name = "firstName",
                Type = "S", 
            },
            new TableAttributeArgs
            {
                Name = "lastName",
                Type = "S", 
            },
            new TableAttributeArgs
            {
                Name = "companyName",
                Type = "S", 
            },
        },
        GlobalSecondaryIndexes = new[]
        {
            new TableGlobalSecondaryIndexArgs
            {
                Name = "EmailIndex",
                HashKey = "email",
                RangeKey = "submittedAt",
                ProjectionType = "ALL", 
            },
            new TableGlobalSecondaryIndexArgs
            {
                Name = "SubjectIndex",
                HashKey = "subject",
                RangeKey = "submittedAt",
                ProjectionType = "ALL", 
            },
            new TableGlobalSecondaryIndexArgs
            {
                Name = "FirstNameIndex",
                HashKey = "firstName",
                RangeKey = "submittedAt",
                ProjectionType = "ALL",
            },
            new TableGlobalSecondaryIndexArgs
            {
                Name = "LastNameIndex",
                HashKey = "lastName",
                RangeKey = "submittedAt",
                ProjectionType = "ALL",
            },
            new TableGlobalSecondaryIndexArgs
            {
                Name = "CompanyIndex",
                HashKey = "companyName",
                RangeKey = "submittedAt",
                ProjectionType = "ALL",
            },
        },
        Ttl = new TableTtlArgs
        {
            AttributeName = "expiresAt",
            Enabled = true,
        },
        Tags =
        {
            { "Name", "contact-form-table" },
            { "Environment", "production" },
            { "Purpose", "ContactFormSubmissions" },
        },
    });

    // Create IAM role for Lambda function
    var lambdaRole = new Role("contact-form-lambda-role", new()
    {
        AssumeRolePolicy = @"{
            ""Version"": ""2012-10-17"",
            ""Statement"": [
                {
                    ""Action"": ""sts:AssumeRole"",
                    ""Principal"": {
                        ""Service"": ""lambda.amazonaws.com""
                    },
                    ""Effect"": ""Allow""
                }
            ]
        }",
    });

    // Create IAM policy for Lambda function
    var lambdaPolicy = new Policy("contact-form-lambda-policy", new()
    {
        PolicyDocument = Output.Format($@"{{
            ""Version"": ""2012-10-17"",
            ""Statement"": [
                {{
                    ""Action"": [
                        ""logs:CreateLogGroup"",
                        ""logs:CreateLogStream"",
                        ""logs:PutLogEvents""
                    ],
                    ""Resource"": ""arn:aws:logs:*:*:*"",
                    ""Effect"": ""Allow""
                }},
                {{
                    ""Action"": [
                        ""dynamodb:GetItem"",
                        ""dynamodb:PutItem"",
                        ""dynamodb:UpdateItem"",
                        ""dynamodb:DeleteItem""
                    ],
                    ""Resource"": ""{contact_form_table.Arn}"",
                    ""Effect"": ""Allow""
                }},
                {{
                    ""Action"": [
                        ""secretsmanager:GetSecretValue"",
                        ""secretsmanager:DescribeSecret""
                    ],
                    ""Resource"": ""arn:aws:secretsmanager:*:*:*"",
                    ""Effect"": ""Allow""
                }}
            ]
        }}"),
    });

    // Attach IAM policy to Lambda role
    var lambdaRolePolicyAttachment = new RolePolicyAttachment("contact-form-lambda-role-policy-attachment", new()
    {
        Role = lambdaRole.Name,
        PolicyArn = lambdaPolicy.Arn,
    });

    // Create Lambda function
    var lambdaFunction = new Function("contact-form-lambda-function", new()
    {
        Runtime = "dotnet8",
        Handler = "ContactFormLambda::ContactFormLambda.ContactFormHandler::HandleContactFormAsync",
        Role = lambdaRole.Arn,
        Code = new FileArchive("./ContactFormLambda/bin/Release/net8.0/publish"),
        Environment = new FunctionEnvironmentArgs
        {
            Variables =
            {
                { "DYNAMODB_TABLE_NAME", contact_form_table.Name },
            },
        },
        Timeout = 30,
        MemorySize = 256,
        Tags =
        {
            { "Environment", "production" },
            { "Application", "contact-form" },
            { "Purpose", "ContactFormProcessor" },
        },
    });

    // Give API Gateway permission to invoke Lambda function
    var lambdaPermission = new Permission("contact-form-lambda-permission", new()
    {
        Action = "lambda:InvokeFunction",
        Function = lambdaFunction.Name,
        Principal = "apigateway.amazonaws.com",
        SourceArn = Output.Format($"{api.ExecutionArn}/*/*"),
    });

    // Create API Gateway Integration (Lambda integration)
    var contactIntegration = new Integration("contact-integration", new()
    {
        RestApi = api.Id,
        ResourceId = contactResource.Id,
        HttpMethod = contactMethod.HttpMethod,
        Type = "AWS_PROXY",
        IntegrationHttpMethod = "POST",
        Uri = lambdaFunction.InvokeArn,
    });

    // Create API Gateway Deployment (depends on methods being created)
    var deployment = new Pulumi.Aws.ApiGateway.Deployment("contact-form-api-deployment", new()
    {
        RestApi = api.Id,
        Description = "Deployment for contact form API",
    }, new CustomResourceOptions
    {
        DependsOn = { contactIntegration }, // Ensure methods are created first
    });

    // Create API Gateway Stage
    var stage = new Stage("contact-form-api-stage", new()
    {
        RestApi = api.Id,
        Deployment = deployment.Id,
        StageName = "prod",
        Description = "Production stage for contact form API",
        Variables =
        {
            { "deployed_at", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") },
        },
        Tags =
        {
            { "Environment", "production" },
            { "Application", "contact-form" },
            { "Purpose", "ContactFormAPIStage" },
        },
    });

    // Create secret to store API Gateway base URL
    var apiGatewaySecret = new Secret("contact-form-api-gateway-url-secret", new()
    {
        Name = "CONTACT_FORM_API_GATEWAY_URL",
        Description = "API Gateway base URL for contact form API",
        Tags =
        {
            { "Environment", "production" },
            { "Application", "contact-form" },
            { "Purpose", "APIGatewayURL" },
        },
    });

    // Create secret version to store the API Gateway URL value
    var apiGatewaySecretVersion = new SecretVersion("contact-form-api-gateway-url-secret-version", new()
    {
        SecretId = apiGatewaySecret.Id,
        SecretString = stage.InvokeUrl.Apply(url => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            { "CONTACT_FORM_API_GATEWAY_URL", url }
        })),
    });

    // Create secret to store API Gateway API key
    var apiKeySecret = new Secret("contact-form-api-key-secret", new()
    {
        Name = "CONTACT_FORM_API_KEY",
        Description = "API key for contact form API Gateway",
        Tags =
        {
            { "Environment", "production" },
            { "Application", "contact-form" },
            { "Purpose", "APIKey" },
        },
    });

    // Create secret version to store the API key value
    var apiKeySecretVersion = new SecretVersion("contact-form-api-key-secret-version", new()
    {
        SecretId = apiKeySecret.Id,
        SecretString = apiKey.Value.Apply(key => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            { "CONTACT_FORM_API_KEY", key }
        })),
    });

    // Create Usage Plan (after stage is created)
    var usagePlan = new UsagePlan("contact-form-usage-plan", new()
    {
        Name = "contact-form-usage-plan",
        Description = "Usage plan for contact form API",
        ApiStages = new[]
        {
            new UsagePlanApiStageArgs
            {
                ApiId = api.Id,
                Stage = stage.StageName,
            },
        },
        QuotaSettings = new UsagePlanQuotaSettingsArgs
        {
            Limit = 1000,
            Period = "DAY",
        },
        ThrottleSettings = new UsagePlanThrottleSettingsArgs
        {
            RateLimit = 100,
            BurstLimit = 200,
        },
        Tags =
        {
            { "Environment", "production" },
            { "Application", "contact-form" },
            { "Purpose", "APIUsagePlan" },
        },
    }, new CustomResourceOptions
    {
        DependsOn = { stage }, // Ensure stage is created first
    });

    // Associate API Key with Usage Plan
    var usagePlanKey = new UsagePlanKey("contact-form-usage-plan-key", new()
    {
        KeyId = apiKey.Id,
        KeyType = "API_KEY",
        UsagePlanId = usagePlan.Id,
    });

    // Create CloudWatch Log Group for error logging
    var logGroup = new LogGroup("contact-form-errors-log-group", new()
    {
        Name = "/contact-form/errors",
        RetentionInDays = 30, // Retain logs for 30 days (adjust as needed)
        Tags =
        {
            { "Environment", "production" },
            { "Application", "contact-form" },
            { "Purpose", "ErrorLogging" },
        },
    });

    // Create CloudWatch Log Stream for app errors
    var logStream = new LogStream("app-errors-log-stream", new()
    {
        Name = $"app-errors-{DateTime.UtcNow:yyyy-MM-dd}",
        LogGroupName = logGroup.Name,
    });

    return new Dictionary<string, object?>
    {
        ["tableName"] = contact_form_table.Name,
        ["tableArn"] = contact_form_table.Arn,
        ["logGroupName"] = logGroup.Name,
        ["logStreamName"] = logStream.Name,
        ["apiGatewayId"] = api.Id,
        ["apiGatewayUrl"] = stage.InvokeUrl,
        ["apiKeyId"] = apiKey.Id,
        ["apiKeyValue"] = apiKey.Value,
        ["usagePlanId"] = usagePlan.Id,
        ["lambdaFunctionArn"] = lambdaFunction.Arn,
        ["apiGatewaySecretArn"] = apiGatewaySecret.Arn,
        ["apiKeySecretArn"] = apiKeySecret.Arn,
    };
});
