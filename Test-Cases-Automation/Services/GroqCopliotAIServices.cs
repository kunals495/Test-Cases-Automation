using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Test_Cases_Automation.Controllers;
using static Test_Cases_Automation.Services.CopilotAIService;

namespace Test_Cases_Automation.Services
{
    public class GroqCopilotAIService
    {
        private readonly HttpClient _http;

        public GroqCopilotAIService(string apiKey)
        {
            _http = new HttpClient
            {
                BaseAddress = new Uri("https://api.groq.com/openai/v1/")
            };

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            _http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Process all endpoints ONE AT A TIME and accumulate responses
        /// </summary>
        public async Task<AIResponse> GenerateAllTestCases(List<ApiInfo> endpoints)
        {
            var finalResponse = new AIResponse
            {
                per_endpoint = new List<PerEndpoint>(),
                scenario_tests = new List<AIGeneratedTestCase>()
            };

            Console.WriteLine($"\n{'=',-80}");
            Console.WriteLine($"GENERATING TEST CASES FOR {endpoints.Count} ENDPOINTS");
            Console.WriteLine($"{'=',-80}\n");

            for (int i = 0; i < endpoints.Count; i++)
            {
                var endpoint = endpoints[i];
                Console.WriteLine($"[{i + 1}/{endpoints.Count}] Processing: {endpoint.method} {endpoint.url}");

                try
                {
                    // Generate test cases for THIS endpoint only
                    var singleResponse = await GenerateSingleEndpointTestCases(endpoint);

                    if (singleResponse?.per_endpoint != null && singleResponse.per_endpoint.Any())
                    {
                        // Add to accumulated results
                        finalResponse.per_endpoint.AddRange(singleResponse.per_endpoint);
                        
                        int testCount = singleResponse.per_endpoint[0].testcases?.Count ?? 0;
                        Console.WriteLine($"  ✓ Generated {testCount} test cases");
                    }
                    else
                    {
                        Console.WriteLine($"  ⚠ No test cases generated");
                    }

                    // Small delay to avoid rate limiting
                    await Task.Delay(300);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ✗ Error: {ex.Message}");

                    // Add fallback test case
                    finalResponse.per_endpoint.Add(new PerEndpoint
                    {
                        endpoint = endpoint.url,
                        testcases = new List<AIGeneratedTestCase>
                        {
                            new AIGeneratedTestCase
                            {
                                TestCaseName = "AI Generation Failed - Using Fallback",
                                Endpoint = endpoint.url,
                                Method = endpoint.method,
                                InputPayload = "{}",
                                PayloadType = endpoint.InputPayloadType.ToString(),
                                ExpectedStatus = 200,
                                ExpectedResponse = new { message = "Success" }
                            }
                        }
                    });
                }
            }

            int totalTests = finalResponse.per_endpoint.Sum(e => e.testcases?.Count ?? 0);
            Console.WriteLine($"\n{'=',-80}");
            Console.WriteLine($"TOTAL: {totalTests} test cases generated for {finalResponse.per_endpoint.Count} endpoints");
            Console.WriteLine($"{'=',-80}\n");

            return finalResponse;
        }

        /// <summary>
        /// Generate test cases for a SINGLE endpoint
        /// </summary>
        private async Task<AIResponse> GenerateSingleEndpointTestCases(ApiInfo endpoint)
        {
            var prompt = BuildPromptForSingleEndpoint(endpoint);

            var requestBody = new
            {
                model = "llama-3.1-8b-instant",
                temperature = 0.5, // Lower for more consistent output
                max_tokens = 1500, // Stay under the 1600 limit
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = @"You are a QA engineer generating API test cases.
CRITICAL RULES:
- Return ONLY valid JSON, no markdown, no explanations
- NEVER use JavaScript code like .repeat(), .concat(), or any functions
- For long strings, write actual repeated characters
- Generate exactly 5-6 test cases
- Use realistic test data"
                    },
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                }
            };

            var content = new StringContent(
                JsonConvert.SerializeObject(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _http.PostAsync("chat/completions", content);
            var raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Groq API Error: " + raw);
            }

            var groqResponse = JsonConvert.DeserializeObject<GroqResponse>(raw);

            // Check if truncated
            if (groqResponse?.choices?[0]?.finish_reason == "length")
            {
                Console.WriteLine("  ⚠ Response truncated - attempting to fix JSON");
            }

            var jsonText = groqResponse?.choices?[0]?.message?.content;

            if (string.IsNullOrWhiteSpace(jsonText))
                throw new Exception("Groq returned empty content");

            // Clean up the response
            jsonText = SanitizeAIResponse(jsonText);

            // Try to fix truncated JSON
            jsonText = TryFixTruncatedJson(jsonText);

            try
            {
                return JsonConvert.DeserializeObject<AIResponse>(jsonText);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"  ✗ JSON Parse Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Build prompt for a single endpoint
        /// </summary>
        private string BuildPromptForSingleEndpoint(ApiInfo endpoint)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Generate 5-6 test cases for this REST API endpoint.");
            sb.AppendLine();
            sb.AppendLine("RULES:");
            sb.AppendLine("- Return ONLY valid JSON");
            sb.AppendLine("- NO JavaScript code (.repeat, .concat, etc.)");
            sb.AppendLine("- Use realistic data values");
            sb.AppendLine();

            sb.AppendLine("Test types to include:");
            sb.AppendLine("1. Happy path with valid data");
            sb.AppendLine("2. Missing required field");
            sb.AppendLine("3. Invalid data type");
            sb.AppendLine("4. Empty/null value");
            sb.AppendLine("5. Boundary value (max/min integers)");
            sb.AppendLine("6. Security test (SQL injection OR XSS)");
            sb.AppendLine();

            sb.AppendLine("JSON FORMAT:");
            sb.AppendLine(@"{
  ""per_endpoint"": [
    {
      ""endpoint"": ""<url>"",
      ""testcases"": [
        {
          ""TestCaseName"": ""Description"",
          ""Endpoint"": ""<url>"",
          ""Method"": ""<HTTP_METHOD>"",
          ""InputPayload"": ""{\""param\"": \""value\""}"",
          ""PayloadType"": ""<query|body|path|formfile>"",
          ""ExpectedStatus"": 200,
          ""ExpectedResponse"": { ""message"": ""Success"" }
        }
      ]
    }
  ],
  ""scenario_tests"": []
}");
            sb.AppendLine();

            sb.AppendLine("ENDPOINT:");
            sb.AppendLine($"URL: {endpoint.url}");
            sb.AppendLine($"Method: {endpoint.method}");
            sb.AppendLine($"PayloadType: {endpoint.InputPayloadType}");

            if (endpoint.parameters != null && endpoint.parameters.Any())
            {
                sb.AppendLine("Parameters:");
                foreach (var p in endpoint.parameters)
                {
                    sb.AppendLine($"  - {p.name} ({p.type})");
                }
            }
            else
            {
                sb.AppendLine("Parameters: None");
            }

            sb.AppendLine();
            sb.AppendLine("Generate the test cases now:");

            return sb.ToString();
        }

        /// <summary>
        /// Remove markdown code blocks and JavaScript patterns
        /// </summary>
        private string SanitizeAIResponse(string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText)) return jsonText;

            // Remove markdown code blocks
            jsonText = jsonText.Trim();
            if (jsonText.StartsWith("```"))
            {
                jsonText = System.Text.RegularExpressions.Regex.Replace(
                    jsonText,
                    @"^```(?:json)?\s*|\s*```$",
                    "",
                    System.Text.RegularExpressions.RegexOptions.Multiline
                );
            }

            // Replace .repeat() patterns
            var repeatRegex = new System.Text.RegularExpressions.Regex(
                @"""([^""]+)""\s*\.\s*repeat\s*\(\s*(\d+)\s*\)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            jsonText = repeatRegex.Replace(jsonText, match =>
            {
                string value = match.Groups[1].Value;
                int count = Math.Min(int.Parse(match.Groups[2].Value), 500);
                return $"\"{new string(value.Length > 0 ? value[0] : 'A', count)}\"";
            });

            // Remove .concat() patterns
            var concatRegex = new System.Text.RegularExpressions.Regex(
                @"\.\s*concat\s*\([^)]*\)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            jsonText = concatRegex.Replace(jsonText, "");

            return jsonText.Trim();
        }

        /// <summary>
        /// Attempt to fix truncated JSON by closing unclosed brackets/braces
        /// </summary>
        private string TryFixTruncatedJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return "{}";

            try
            {
                // Try to parse as-is
                JsonConvert.DeserializeObject<AIResponse>(json);
                return json; // Already valid
            }
            catch
            {
                // JSON is invalid, try to fix it
            }

            // Count brackets and braces
            int openBraces = json.Count(c => c == '{');
            int closeBraces = json.Count(c => c == '}');
            int openBrackets = json.Count(c => c == '[');
            int closeBrackets = json.Count(c => c == ']');

            // Check if we're in the middle of a string
            int quoteCount = json.Count(c => c == '"');
            if (quoteCount % 2 != 0)
            {
                json += "\""; // Close the string
            }

            // Remove incomplete last line if it doesn't end properly
            var lines = json.Split('\n').ToList();
            if (lines.Count > 0)
            {
                var lastLine = lines[lines.Count - 1].Trim();
                if (!string.IsNullOrEmpty(lastLine) &&
                    !lastLine.EndsWith("}") &&
                    !lastLine.EndsWith("]") &&
                    !lastLine.EndsWith(",") &&
                    !lastLine.EndsWith("\""))
                {
                    lines.RemoveAt(lines.Count - 1);
                }
            }

            json = string.Join("\n", lines);

            // Close unclosed arrays
            for (int i = 0; i < openBrackets - closeBrackets; i++)
                json += "\n]";

            // Close unclosed objects
            for (int i = 0; i < openBraces - closeBraces; i++)
                json += "\n}";

            return json;
        }
    }

    public class GroqResponse
    {
        public List<GroqChoice> choices { get; set; }
    }

    public class GroqChoice
    {
        public GroqMessage message { get; set; }
        public string finish_reason { get; set; }
    }

    public class GroqMessage
    {
        public string content { get; set; }
    }
}
