using Pulumi;
using Pulumi.Aws.DynamoDB;
using Pulumi.Aws.DynamoDB.Inputs;
using Pulumi.Aws.CloudWatch;
using System;
using System.Collections.Generic;

return await Deployment.RunAsync(() =>
{

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
    };
});
