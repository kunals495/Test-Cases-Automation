////using Google.GenAI;
////using Google.GenAI.Types;
////using Newtonsoft.Json;
////using static Test_Cases_Automation.Controllers.TestController; 

////namespace Test_Cases_Automation.Services
////{
////    public class CopilotAIService
////    {
////        private readonly Client _client;

////        public CopilotAIService(string apiKey)
////        {
////            _client = new Client(apiKey: apiKey);
////        }

////        public class AIGeneratedTestCase
////        {
////            public string TestCaseName { get; set; }
////            public object InputPayload { get; set; }
////            public string PayloadType { get; set; }
////            public int ExpectedStatus { get; set; }
////            public object ExpectedResponse { get; set; }
////        }

////        public async Task<List<AIGeneratedTestCase>> GenerateTestCases(ApiInfo endpoint)
////        {
////            string prompt = BuildPrompt(endpoint);

////            // Define the response schema to enforce structured JSON output
////            var testCaseSchema = new Schema
////            {
////                Type = Google.GenAI.Types.Type.OBJECT,
////                Properties = new Dictionary<string, Schema>
////    {
////        { "TestCaseName", new Schema { Type = Google.GenAI.Types.Type.STRING } },

////        { "InputPayload", new Schema { Type = Google.GenAI.Types.Type.STRING, Nullable = true } },

////        { "PayloadType", new Schema { Type = Google.GenAI.Types.Type.STRING } },
////        { "ExpectedStatus", new Schema { Type = Google.GenAI.Types.Type.INTEGER } },

////        { "ExpectedResponse", new Schema
////            {
////                Type = Google.GenAI.Types.Type.OBJECT,
////                Properties = new Dictionary<string, Schema>
////                {
////                    { "result", new Schema { Type = Google.GenAI.Types.Type.STRING, Nullable = true } }
////                }
////            }
////        }
////    },
////                Required = new List<string>
////    {
////        "TestCaseName", "InputPayload", "PayloadType", "ExpectedStatus", "ExpectedResponse"
////    }
////            };


////            var arraySchema = new Schema
////            {
////                Type = Google.GenAI.Types.Type.ARRAY,
////                Items = testCaseSchema
////            };

////            var config = new GenerateContentConfig
////            {
////                ResponseMimeType = "application/json",
////                ResponseSchema = arraySchema
////            };

////            // Gemini API Call with JSON Schema Enforcement
////            var response = await _client.Models.GenerateContentAsync(
////                model: "gemini-2.5-flash",
////                contents: new Content
////                {
////                    Role = "user",
////                    Parts = new List<Part> { new Part { Text = prompt } }
////                },
////                config: config
////            );

////            string content = response.Candidates[0].Content.Parts[0].Text.Trim();

////            Console.WriteLine(content);

////            // Direct deserialization (no cleaning needed due to schema enforcement)
////            return JsonConvert.DeserializeObject<List<AIGeneratedTestCase>>(content) ?? new List<AIGeneratedTestCase>();
////        }

////        private string BuildPrompt(ApiInfo ep)
////        {
////            var parameters = string.Join("\n", ep.parameters.Select(p =>
////                $"- {p.name}: {p.type}"
////            ));

////            return $@"
////                You are an expert QA engineer.
////                Generate API test cases for the following endpoint.

////                URL: {ep.url}
////                Method: {ep.method}

////                Parameters:
////                {parameters}

////                Follow these STRICT RULES when producing test cases:

////                1. InputPayload rules:
////                   - If parameter source = 'query' or 'body':
////                       → InputPayload MUST be a JSON object with key-value pairs.
////                   - If parameter source = 'formfile' or type includes 'IFormFile':
////                       → InputPayload MUST be a STRING containing a file path.
////                         Example: ""{"Insert your file Path Here"}""
////                   - If endpoint has no parameters:
////                       → InputPayload = null.

////                2. DELETE and GET requests:
////                   - Never leave InputPayload empty.
////                   - PayloadType = ""query"".
////                   - InputPayload = JSON object containing all query parameters.

////                3. ExpectedResponse:
////                   - Always include: {{ ""result"": ""some meaningful message"" }}

////                4. Count:
////                   - Generate exactly 4 test cases:
////                       • valid / success
////                       • missing parameter
////                       • invalid parameter
////                       • edge case

////                5. Output:
////                   - MUST be strictly valid JSON array matching the schema.
////                ";
////        }
////    }
////}

//using Google.GenAI;
//using Google.GenAI.Types;
//using Newtonsoft.Json;
//using System.Text;
//using Test_Cases_Automation.Controllers;

//namespace Test_Cases_Automation.Services
//{
//    public class CopilotAIService
//    {
//        private readonly Client _client;

//        public CopilotAIService(string apiKey)
//        {
//            _client = new Client(apiKey: apiKey);
//        }
//        public class AIGeneratedTestCase
//        {
//            public string TestCaseName { get; set; }

//            public string Endpoint { get; set; }
//            public string Method { get; set; }

//            public object InputPayload { get; set; }
//            public string PayloadType { get; set; }
//            public int ExpectedStatus { get; set; }
//            public object ExpectedResponse { get; set; }
//        }

//        public class AIResponseWrapper
//        {
//            public List<PerEndpointResponse> per_endpoint { get; set; } = new List<PerEndpointResponse>();
//            public List<AIGeneratedTestCase> scenario_tests { get; set; } = new List<AIGeneratedTestCase>();
//        }

//        public class PerEndpointResponse
//        {
//            public string endpoint { get; set; }
//            public List<AIGeneratedTestCase> testcases { get; set; } = new List<AIGeneratedTestCase>();
//        }

//        public async Task<TestController.AIResponse> GenerateAllTestCases(List<TestController.ApiInfo> endpoints)
//        {
//            string prompt = BuildPromptForAll(endpoints);
//            var testCaseSchema = BuildTestCaseSchema();

//            var perEndpointItemSchema = new Schema
//            {
//                Type = Google.GenAI.Types.Type.OBJECT,
//                Properties = new Dictionary<string, Schema>
//                {
//                    { "endpoint", new Schema { Type = Google.GenAI.Types.Type.STRING } },
//                    { "testcases", new Schema { Type = Google.GenAI.Types.Type.ARRAY, Items = testCaseSchema } }
//                },
//                Required = new List<string> { "endpoint", "testcases" }
//            };

//            var rootSchema = new Schema
//            {
//                Type = Google.GenAI.Types.Type.OBJECT,
//                Properties = new Dictionary<string, Schema>
//                {
//                    { "per_endpoint", new Schema { Type = Google.GenAI.Types.Type.ARRAY, Items = perEndpointItemSchema } },
//                    { "scenario_tests", new Schema { Type = Google.GenAI.Types.Type.ARRAY, Items = testCaseSchema } }
//                },
//                Required = new List<string> { "per_endpoint", "scenario_tests" }
//            };

//            var config = new GenerateContentConfig
//            {
//                ResponseMimeType = "application/json",
//                ResponseSchema = rootSchema
//            };

//            var response = await _client.Models.GenerateContentAsync(
//                model: "gemini-2.5-flash-lite",
//                contents: new Content
//                {
//                    Role = "user",
//                    Parts = new List<Part> { new Part { Text = prompt } }
//                },
//                config: config
//            );

//            string content = response.Candidates[0].Content.Parts[0].Text.Trim();
//            var wrapper = JsonConvert.DeserializeObject<AIResponseWrapper>(content)
//                          ?? new AIResponseWrapper();

//            return new TestController.AIResponse
//            {
//                per_endpoint = wrapper.per_endpoint.Select(p => new TestController.PerEndpoint
//                {
//                    endpoint = p.endpoint,
//                    testcases = p.testcases
//                }).ToList(),

//                scenario_tests = wrapper.scenario_tests
//            };
//        }

//        private Schema BuildTestCaseSchema()
//        {
//            return new Schema
//            {
//                Type = Google.GenAI.Types.Type.OBJECT,
//                Properties = new Dictionary<string, Schema>
//                {
//                    { "TestCaseName", new Schema { Type = Google.GenAI.Types.Type.STRING } },

//                    // NEW
//                    { "Endpoint", new Schema { Type = Google.GenAI.Types.Type.STRING } },
//                    { "Method", new Schema { Type = Google.GenAI.Types.Type.STRING } },

//                    { "InputPayload", new Schema { Type = Google.GenAI.Types.Type.STRING, Nullable = true } },
//                    { "PayloadType", new Schema { Type = Google.GenAI.Types.Type.STRING } },
//                    { "ExpectedStatus", new Schema { Type = Google.GenAI.Types.Type.INTEGER } },

//                    {
//                        "ExpectedResponse",
//                        new Schema
//                        {
//                            Type = Google.GenAI.Types.Type.OBJECT,
//                            Properties = new Dictionary<string, Schema>
//                            {
//                                { "result", new Schema { Type = Google.GenAI.Types.Type.STRING, Nullable = true } }
//                            }
//                        }
//                    }
//                },

//                Required = new List<string>
//                {
//                    "TestCaseName",
//                    "Endpoint",
//                    "Method",
//                    "InputPayload",
//                    "PayloadType",
//                    "ExpectedStatus",
//                    "ExpectedResponse"
//                }
//            };
//        }

//        private string BuildPromptForAll(List<TestController.ApiInfo> endpoints)
//        {
//            var sb = new StringBuilder();

//            sb.AppendLine("You are an expert QA engineer.");
//            sb.AppendLine("Generate API test cases for ALL endpoints together as JSON.");

//            sb.AppendLine("");
//            sb.AppendLine("REQUIREMENTS:");
//            sb.AppendLine("1) For each endpoint, generate EXACTLY 4 test cases:");
//            sb.AppendLine("   - valid");
//            sb.AppendLine("   - missing parameter");
//            sb.AppendLine("   - invalid parameter");
//            sb.AppendLine("   - edge case");

//            sb.AppendLine("");
//            sb.AppendLine("2) InputPayload rules:");
//            sb.AppendLine("   - If source = 'query' or 'body': InputPayload MUST be JSON.");
//            sb.AppendLine("   - If source = 'formfile' or type contains 'IFormFile':");
//            sb.AppendLine("       InputPayload MUST be a STRING path like: \"{Insert Your File Path Here}\"");
//            sb.AppendLine("   - No parameters => InputPayload = null.");

//            sb.AppendLine("");
//            sb.AppendLine("2.5) PayloadType rules (MANDATORY):");
//            sb.AppendLine("   - If the parameter source is \"query\":");
//            sb.AppendLine("        PayloadType MUST be \"query\".");
//            sb.AppendLine("   - If the parameter source is \"body\":");
//            sb.AppendLine("        PayloadType MUST be \"body\".");
//            sb.AppendLine("   - If the parameter source is \"formfile\" or type includes \"IFormFile\":");
//            sb.AppendLine("        PayloadType MUST be \"formfile\".");
//            sb.AppendLine("   - For GET and DELETE requests:");
//            sb.AppendLine("        PayloadType MUST be \"query\".");


//            sb.AppendLine("");
//            sb.AppendLine("3) GET and DELETE must always use PayloadType = \"query\".");

//            sb.AppendLine("");
//            sb.AppendLine("4) ExpectedResponse must be:");
//            sb.AppendLine("   { \"result\": \"some meaningful message\" }");

//            sb.AppendLine("");
//            sb.AppendLine("5) Create scenario-based tests that chain multiple endpoints.");
//            sb.AppendLine("   For EVERY step in scenario_tests, YOU MUST include:");
//            sb.AppendLine("       - Endpoint");
//            sb.AppendLine("       - Method");
//            sb.AppendLine("       - InputPayload");
//            sb.AppendLine("       - PayloadType");
//            sb.AppendLine("       - ExpectedStatus");
//            sb.AppendLine("       - ExpectedResponse");

//            sb.AppendLine("");
//            sb.AppendLine("6) Output ONLY JSON with:");
//            sb.AppendLine("   - per_endpoint");
//            sb.AppendLine("   - scenario_tests");

//            sb.AppendLine("");
//            sb.AppendLine("7) For EVERY test case (including scenario_tests), ALWAYS set:");
//            sb.AppendLine("   - Endpoint = exact API URL");
//            sb.AppendLine("   - Method = GET/POST/PUT/DELETE");

//            sb.AppendLine("");
//            sb.AppendLine("ENDPOINT LIST:");
//            foreach (var ep in endpoints)
//            {
//                sb.AppendLine($"- URL: {ep.url}");
//                sb.AppendLine($"  Method: {ep.method}");

//                if (ep.parameters?.Count > 0)
//                {
//                    sb.AppendLine("  Parameters:");
//                    foreach (var p in ep.parameters)
//                        sb.AppendLine($"    - name: {p.name}, type: {p.type}, source: {p.source}");
//                }
//                else
//                {
//                    sb.AppendLine("  Parameters: none");
//                }

//                sb.AppendLine();
//            }

//            sb.AppendLine("Return valid JSON only.");

//            return sb.ToString();
//        }
//    }
//}
