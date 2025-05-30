using Pulumi;
using Pulumi.Aws.DynamoDB;
using Pulumi.Aws.DynamoDB.Inputs;
using Pulumi.Aws.CloudWatch;
using Pulumi.Aws.ApiGateway;
using Pulumi.Aws.ApiGateway.Inputs;
using System;
using System.Collections.Generic;

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

    // Create API Gateway Integration (placeholder for now)
    var contactIntegration = new Integration("contact-integration", new()
    {
        RestApi = api.Id,
        ResourceId = contactResource.Id,
        HttpMethod = contactMethod.HttpMethod,
        Type = "MOCK",
        RequestTemplates =
        {
            { "application/json", "{\"statusCode\": 200}" },
        },
    });

    // Create API Gateway Method Response
    var contactMethodResponse = new MethodResponse("contact-method-response", new()
    {
        RestApi = api.Id,
        ResourceId = contactResource.Id,
        HttpMethod = contactMethod.HttpMethod,
        StatusCode = "200",
    });

    // Create API Gateway Integration Response
    var contactIntegrationResponse = new IntegrationResponse("contact-integration-response", new()
    {
        RestApi = api.Id,
        ResourceId = contactResource.Id,
        HttpMethod = contactMethod.HttpMethod,
        StatusCode = contactMethodResponse.StatusCode,
        ResponseTemplates =
        {
            { "application/json", "{\"message\": \"Contact form received\"}" },
        },
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

    // Create API Gateway Deployment (depends on methods being created)
    var deployment = new Pulumi.Aws.ApiGateway.Deployment("contact-form-api-deployment", new()
    {
        RestApi = api.Id,
        Description = "Deployment for contact form API",
    }, new CustomResourceOptions
    {
        DependsOn = { contactIntegrationResponse }, // Ensure methods are created first
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
    var logGroup = new LogGroup("sveltekit-errors-log-group", new()
    {
        Name = "/sveltekit/errors",
        RetentionInDays = 30, // Retain logs for 30 days (adjust as needed)
        Tags =
        {
            { "Environment", "production" },
            { "Application", "sveltekit" },
            { "Purpose", "ErrorLogging" },
        },
    });

    // Create CloudWatch Log Stream for app errors
    var logStream = new LogStream("app-errors-log-stream", new()
    {
        Name = $"app-errors-{DateTime.UtcNow:yyyy-MM-dd}",
        LogGroupName = logGroup.Name,
    });

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
    };
});
