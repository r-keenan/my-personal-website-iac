using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ContactFormLambda;

public class ContactFormHandler
{
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly string _tableName;

    public ContactFormHandler()
    {
        _dynamoDbClient = new AmazonDynamoDBClient();
        _tableName = Environment.GetEnvironmentVariable("DYNAMODB_TABLE_NAME") ?? "ContactMessages";
    }

    // Constructor for testing with dependency injection
    public ContactFormHandler(IAmazonDynamoDB dynamoDbClient, string tableName)
    {
        _dynamoDbClient = dynamoDbClient;
        _tableName = tableName;
    }

    public async Task<APIGatewayProxyResponse> HandleContactFormAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
        context.Logger.LogInformation($"Processing contact form submission. Request ID: {context.AwsRequestId}");

        try
        {
            // Validate API key is present (API Gateway handles validation, but we can log it)
            if (request.Headers == null || !request.Headers.ContainsKey("x-api-key"))
            {
                context.Logger.LogWarning("Request missing API key header");
                return CreateErrorResponse(401, "API key required");
            }

            // Parse the request body
            if (string.IsNullOrEmpty(request.Body))
            {
                context.Logger.LogWarning("Request body is empty");
                return CreateErrorResponse(400, "Request body is required");
            }

            ContactFormData? contactData;
            try
            {
                contactData = JsonSerializer.Deserialize<ContactFormData>(request.Body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                context.Logger.LogError($"Failed to parse JSON: {ex.Message}");
                return CreateErrorResponse(400, "Invalid JSON format");
            }

            if (contactData == null)
            {
                return CreateErrorResponse(400, "Invalid contact form data");
            }

            // Validate required fields
            var validationErrors = ValidateContactData(contactData);
            if (validationErrors.Any())
            {
                context.Logger.LogWarning($"Validation errors: {string.Join(", ", validationErrors)}");
                return CreateErrorResponse(400, $"Validation errors: {string.Join(", ", validationErrors)}");
            }

            // Generate unique ID and timestamp
            var id = Guid.NewGuid().ToString();
            var submittedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var expiresAt = DateTimeOffset.UtcNow.AddDays(90).ToUnixTimeSeconds(); // TTL: 90 days

            // Create DynamoDB item
            var item = new Dictionary<string, AttributeValue>
            {
                ["id"] = new AttributeValue { S = id },
                ["submittedAt"] = new AttributeValue { S = submittedAt },
                ["firstName"] = new AttributeValue { S = contactData.FirstName },
                ["lastName"] = new AttributeValue { S = contactData.LastName },
                ["email"] = new AttributeValue { S = contactData.Email },
                ["subject"] = new AttributeValue { S = contactData.Subject },
                ["message"] = new AttributeValue { S = contactData.Message },
                ["expiresAt"] = new AttributeValue { N = expiresAt.ToString() }
            };

            // Add optional fields if provided
            if (!string.IsNullOrEmpty(contactData.CompanyName))
            {
                item["companyName"] = new AttributeValue { S = contactData.CompanyName };
            }

            if (!string.IsNullOrEmpty(contactData.CompanyWebsite))
            {
                item["companyWebsite"] = new AttributeValue { S = contactData.CompanyWebsite };
            }

            if (!string.IsNullOrEmpty(contactData.Phone))
            {
                item["phone"] = new AttributeValue { S = contactData.Phone };
            }

            // Insert into DynamoDB
            var putRequest = new PutItemRequest
            {
                TableName = _tableName,
                Item = item
            };

            await _dynamoDbClient.PutItemAsync(putRequest);

            context.Logger.LogInformation($"Successfully saved contact form submission with ID: {id}");

            // Return success response
            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" },
                    { "Access-Control-Allow-Origin", "*" }, // Configure CORS as needed
                    { "Access-Control-Allow-Headers", "Content-Type,X-Amz-Date,Authorization,X-Api-Key,X-Amz-Security-Token" },
                    { "Access-Control-Allow-Methods", "POST,OPTIONS" }
                },
                Body = JsonSerializer.Serialize(new
                {
                    success = true,
                    message = "Contact form submitted successfully",
                    submissionId = id,
                    submittedAt = submittedAt
                })
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error processing contact form: {ex.Message}");
            context.Logger.LogError($"Stack trace: {ex.StackTrace}");

            return CreateErrorResponse(500, "Internal server error");
        }
    }

    private static List<string> ValidateContactData(ContactFormData data)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(data.FirstName))
            errors.Add("First name is required");

        if (string.IsNullOrWhiteSpace(data.LastName))
            errors.Add("Last name is required");

        if (string.IsNullOrWhiteSpace(data.Email))
            errors.Add("Email is required");
        else if (!IsValidEmail(data.Email))
            errors.Add("Invalid email format");

        if (string.IsNullOrWhiteSpace(data.Subject))
            errors.Add("Subject is required");

        if (string.IsNullOrWhiteSpace(data.Message))
            errors.Add("Message is required");

        return errors;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private static APIGatewayProxyResponse CreateErrorResponse(int statusCode, string message)
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = statusCode,
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" },
                { "Access-Control-Allow-Origin", "*" },
                { "Access-Control-Allow-Headers", "Content-Type,X-Amz-Date,Authorization,X-Api-Key,X-Amz-Security-Token" },
                { "Access-Control-Allow-Methods", "POST,OPTIONS" }
            },
            Body = JsonSerializer.Serialize(new
            {
                success = false,
                error = message
            })
        };
    }
}

public class ContactFormData
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? CompanyWebsite { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
