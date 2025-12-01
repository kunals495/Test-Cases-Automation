//using Newtonsoft.Json;
//using OpenAI.Chat;
//using System.ClientModel;
//using static Test_Cases_Automation.Controllers.TestController;

//namespace Test_Cases_Automation.Services
//{
//    public class CopilotAIService
//    {
//        private readonly ChatClient _chatClient;

//        public CopilotAIService(string apiKey)
//        {
//            // Initialize with API key
//            _chatClient = new ChatClient("gpt-4", new ApiKeyCredential(apiKey));
//        }

//        public class AIGeneratedTestCase
//        {
//            public string TestCaseName { get; set; }
//            public object InputPayload { get; set; }
//            public string PayloadType { get; set; }
//            public int ExpectedStatus { get; set; }
//            public object ExpectedResponse { get; set; }
//        }

//        public async Task<List<AIGeneratedTestCase>> GenerateTestCases(ApiInfo endpoint)
//        {
//            string prompt = BuildPrompt(endpoint);

//            // Create chat messages
//            var messages = new List<ChatMessage>
//            {
//                new SystemChatMessage("You are an expert QA engineer. Generate valid, invalid, boundary and negative test cases for API endpoints. Always respond with valid JSON only."),
//                new UserChatMessage(prompt)
//            };

//            // Get completion
//            var options = new ChatCompletionOptions
//            {
//                MaxTokens = 2000,
//                Temperature = 0.3f
//            };

//            var response = await _chatClient.CompleteChatAsync(messages, options);

//            string content = response.Value.Content[0].Text;

//            // Clean up response if it contains markdown code blocks
//            content = content.Trim();
//            if (content.StartsWith("```json"))
//            {
//                content = content.Substring(7);
//            }
//            if (content.StartsWith("```"))
//            {
//                content = content.Substring(3);
//            }
//            if (content.EndsWith("```"))
//            {
//                content = content.Substring(0, content.Length - 3);
//            }
//            content = content.Trim();

//            return JsonConvert.DeserializeObject<List<AIGeneratedTestCase>>(content);
//        }

//        private string BuildPrompt(ApiInfo ep)
//        {
//            var parameters = string.Join("\n", ep.parameters.Select(p =>
//                $"- {p.name}: {p.type} ({p.source})"
//            ));

//            return $@"Generate API test cases for the following endpoint:
//URL: {ep.url}
//Method: {ep.method}
//Parameters:
//{parameters}

//Generate a JSON array of test cases. 
//Each item must strictly follow this structure:
//[
//  {{
//    ""TestCaseName"": ""Valid Payload"",
//    ""InputPayload"": {{ ""exampleKey"": ""exampleValue"" }},
//    ""PayloadType"": ""Body"",
//    ""ExpectedStatus"": 200,
//    ""ExpectedResponse"": {{ ""success"": true }}
//  }}
//]

//Return ONLY the JSON array, no markdown formatting or explanation.
//Ensure payload uses real data, not variable names.";
//        }
//    }
//}