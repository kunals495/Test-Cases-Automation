using Google.GenAI;
using Google.GenAI.Types;
using Newtonsoft.Json;
using System.Text;
using Test_Cases_Automation.Controllers;

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
            public string Endpoint { get; set; }
            public string Method { get; set; }
            public object InputPayload { get; set; }
            public string PayloadType { get; set; }
            public int ExpectedStatus { get; set; }
            public object ExpectedResponse { get; set; }
        }

        public async Task<AIResponse> GenerateAllTestCases(List<ApiInfo> endpoints)
        {
            string prompt = BuildPrompt(endpoints);

            var schema = new Schema
            {
                Type = Google.GenAI.Types.Type.OBJECT,
                Properties = new()
            {
                {
                    "per_endpoint",
                    new Schema
                    {
                        Type = Google.GenAI.Types.Type.ARRAY,
                        Items = new Schema
                        {
                            Type = Google.GenAI.Types.Type.OBJECT,
                            Properties = new()
                            {
                                { "endpoint", new Schema { Type = Google.GenAI.Types.Type.STRING } },
                                {
                                    "testcases",
                                    new Schema
                                    {
                                        Type = Google.GenAI.Types.Type.ARRAY,
                                        Items = TestCaseSchema()
                                    }
                                }
                            }
                        }
                    }
                },
                { "scenario_tests", new Schema { Type = Google.GenAI.Types.Type.ARRAY, Items = TestCaseSchema() } }
            }
            };

            var response = await _client.Models.GenerateContentAsync(
                model: "gemini-3-flash-preview",
                contents: new Content
                {
                    Role = "user",
                    Parts = new() { new Part { Text = prompt } }
                },
                config: new GenerateContentConfig
                {
                    ResponseMimeType = "application/json",
                    ResponseSchema = schema
                }
            );

            return JsonConvert.DeserializeObject<AIResponse>(
                response.Candidates[0].Content.Parts[0].Text
            );
        }

        private Schema TestCaseSchema()
        {
            return new Schema
            {
                Type = Google.GenAI.Types.Type.OBJECT,
                Properties = new Dictionary<string, Schema>  // 🔥 EXPLICIT TYPE TO AVOID INFERENCE ISSUES
        {
            { "TestCaseName", new Schema { Type = Google.GenAI.Types.Type.STRING } },
            { "Endpoint", new Schema { Type = Google.GenAI.Types.Type.STRING } },
            { "Method", new Schema { Type = Google.GenAI.Types.Type.STRING } },
            { "InputPayload", new Schema { Type = Google.GenAI.Types.Type.STRING } },
            { "PayloadType", new Schema { Type = Google.GenAI.Types.Type.STRING } },
            { "ExpectedStatus", new Schema { Type = Google.GenAI.Types.Type.INTEGER } },
            {
                "ExpectedResponse",
                new Schema
                {
                    Type = Google.GenAI.Types.Type.OBJECT,
                    Properties = new Dictionary<string, Schema>  // 🔥 SAME FIX FOR NESTED
                    {
                        { "result", new Schema { Type = Google.GenAI.Types.Type.STRING } }
                    }
                }
            }
        }
            };
        }

        private string BuildPrompt(List<ApiInfo> endpoints)
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are a QA engineer.");
            sb.AppendLine("DO NOT change JSON structure.");
            sb.AppendLine("ONLY modify values.");
            sb.AppendLine("Generate EXACTLY 4 test cases per endpoint.");
            sb.AppendLine("If you see json inputpayloadtype add body in inputpayloadtype not json");

            foreach (var ep in endpoints)
            {
                sb.AppendLine($"Endpoint: {ep.url}");
                sb.AppendLine($"Method: {ep.method}");
                sb.AppendLine("SwaggerPayloadTemplate:");
                sb.AppendLine(JsonConvert.SerializeObject(ep.SwaggerPayloadTemplate, Formatting.Indented));
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
