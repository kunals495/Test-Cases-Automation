using Google.GenAI;
using Google.GenAI.Types;
using Newtonsoft.Json;
using static Test_Cases_Automation.Controllers.TestController; 

namespace Test_Cases_Automation.Services
{
    public class CopilotAIService
    {
        private readonly Client _client;

        public CopilotAIService(string apiKey)
        {
            _client = new Client(apiKey: apiKey);
        }

        public class AIGeneratedTestCase
        {
            public string TestCaseName { get; set; }
            public object InputPayload { get; set; }
            public string PayloadType { get; set; }
            public int ExpectedStatus { get; set; }
            public object ExpectedResponse { get; set; }
        }

        public async Task<List<AIGeneratedTestCase>> GenerateTestCases(ApiInfo endpoint)
        {
            string prompt = BuildPrompt(endpoint);

            // Define the response schema to enforce structured JSON output
            var testCaseSchema = new Schema
            {
                Type = Google.GenAI.Types.Type.OBJECT,
                Properties = new Dictionary<string, Schema>
    {
        { "TestCaseName", new Schema { Type = Google.GenAI.Types.Type.STRING } },

        { "InputPayload", new Schema { Type = Google.GenAI.Types.Type.STRING, Nullable = true } },

        { "PayloadType", new Schema { Type = Google.GenAI.Types.Type.STRING } },
        { "ExpectedStatus", new Schema { Type = Google.GenAI.Types.Type.INTEGER } },

        { "ExpectedResponse", new Schema
            {
                Type = Google.GenAI.Types.Type.OBJECT,
                Properties = new Dictionary<string, Schema>
                {
                    { "result", new Schema { Type = Google.GenAI.Types.Type.STRING, Nullable = true } }
                }
            }
        }
    },
                Required = new List<string>
    {
        "TestCaseName", "InputPayload", "PayloadType", "ExpectedStatus", "ExpectedResponse"
    }
            };


            var arraySchema = new Schema
            {
                Type = Google.GenAI.Types.Type.ARRAY,
                Items = testCaseSchema
            };

            var config = new GenerateContentConfig
            {
                ResponseMimeType = "application/json",
                ResponseSchema = arraySchema
            };

            // Gemini API Call with JSON Schema Enforcement
            var response = await _client.Models.GenerateContentAsync(
                model: "gemini-2.5-flash",
                contents: new Content
                {
                    Role = "user",
                    Parts = new List<Part> { new Part { Text = prompt } }
                },
                config: config
            );

            string content = response.Candidates[0].Content.Parts[0].Text.Trim();

            Console.WriteLine(content);

            // Direct deserialization (no cleaning needed due to schema enforcement)
            return JsonConvert.DeserializeObject<List<AIGeneratedTestCase>>(content) ?? new List<AIGeneratedTestCase>();
        }

        private string BuildPrompt(ApiInfo ep)
        {
            var parameters = string.Join("\n", ep.parameters.Select(p =>
                $"- {p.name}: {p.type}"
            ));

            return $@"You are an expert QA engineer. 
                    Generate API test cases for the following endpoint:

                    URL: {ep.url}
                    Method: {ep.method}

                    Parameters:
                    {parameters}

                    Generate an array of test cases using the enforced JSON schema. Use realistic values for payloads and responses.";
        }
    }
}