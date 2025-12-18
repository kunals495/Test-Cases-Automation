using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OfficeOpenXml;
using Test_Cases_Automation.Model;
using Test_Cases_Automation.Services;

namespace Test_Cases_Automation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly ApiTestRunnerService _service;

        public TestController(ApiTestRunnerService service)
        {
            _service = service;
        }

        [HttpPost("execute-api")]
        public async Task<IActionResult> RunAPI([FromBody] PayloadRowDto payloadRow)
        {
            try
            {
                // Validate request body
                if (payloadRow == null)
                    return BadRequest(new { message = "Payload cannot be null" });

                if (string.IsNullOrWhiteSpace(payloadRow.endpoint))
                    return BadRequest(new { message = "Endpoint is required" });

                if (string.IsNullOrWhiteSpace(payloadRow.method))
                    return BadRequest(new { message = "HTTP method is required" });

                if (string.IsNullOrWhiteSpace(payloadRow.type))
                    return BadRequest(new { message = "Payload type must be provided (file/body/query)" });

                // Validate HTTP Method
                string[] allowedMethods = { "GET", "POST", "PUT", "DELETE" };

                if (!allowedMethods.Contains(payloadRow.method.ToUpper()))
                {
                    return BadRequest(new
                    {
                        message = $"Invalid HTTP method. Allowed: {string.Join(", ", allowedMethods)}"
                    });
                }

                // Authenticate and get token
                var token = await _service.LoginAndGetToken();

                if (token == null)
                {
                    return StatusCode(StatusCodes.Status401Unauthorized,
                        new { message = "Unable to authenticate. Token generation failed." });
                }

                // Call API and execute request
                var response = await _service.CallApi(
                    payloadRow.endpoint,
                    payloadRow.method,
                    payloadRow.payload ?? "",
                    token,
                    payloadRow.type
                );

                // Return API result
                return Ok(new
                {
                    statusCode = response.StatusCode,
                    responseBody = response.ResponseBody
                });
            }
            catch (Exception ex)
            {
                // Catch unexpected exceptions
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new
                    {
                        message = "An error occurred while executing API.",
                        error = ex.Message
                    });
            }
        }


        [HttpPost("run-test-live")]
        public async Task RunTestLive(IFormFile excelFile)
        {
            Response.Headers.Add("Content-Type", "text/event-stream");
            Response.Headers.Add("Cache-Control", "no-cache");
            Response.Headers.Add("Connection", "keep-alive");

            // Read file stream safely
            byte[] inputBytes;
            using (var ms = new MemoryStream())
            {
                await excelFile.CopyToAsync(ms);
                inputBytes = ms.ToArray();
            }

            // Give buffer to service
            var resultBytes = await _service.ProcessExcelLive(inputBytes, Response);

            // Create Temp folder
            var tempFolder = Path.Combine(Directory.GetCurrentDirectory(), "Temp");
            if (!Directory.Exists(tempFolder))
                Directory.CreateDirectory(tempFolder);

            string fileId = Guid.NewGuid().ToString();
            string filePath = Path.Combine(tempFolder, $"{fileId}.xlsx");

            await System.IO.File.WriteAllBytesAsync(filePath, resultBytes);

            // Send final SSE event
            var finalEvent = new { completed = true, fileId };
            await Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(finalEvent)}\n\n");
            await Response.Body.FlushAsync();
        }


        [HttpGet("download-result/{fileId}")]
        public IActionResult DownloadResult(string fileId)
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", $"{fileId}.xlsx");

            if (!System.IO.File.Exists(filePath))
                return NotFound("File expired or not found.");

            byte[] bytes = System.IO.File.ReadAllBytes(filePath);

            // Delete AFTER reading
            System.IO.File.Delete(filePath);

            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "TestResult.xlsx"
            );
        }

        //        public class ApiParameterDto
        //        {
        //            public string name { get; set; }
        //            public string type { get; set; }
        //            public string source { get; set; } // Query / Body / Path / FormFile
        //        }

        //        public class ApiInfo
        //        {
        //            public string method { get; set; }
        //            public string route { get; set; }
        //            public string url { get; set; }
        //            public List<ApiParameterDto> parameters { get; set; } = new List<ApiParameterDto>();
        //        }


        //        public class AIResponse
        //        {
        //            public List<PerEndpoint> per_endpoint { get; set; } = new List<PerEndpoint>();
        //            public List<CopilotAIService.AIGeneratedTestCase> scenario_tests { get; set; } = new List<CopilotAIService.AIGeneratedTestCase>();
        //        }

        //        public class PerEndpoint
        //        {
        //            public string endpoint { get; set; } // we will use "url method" or just url depending on prompt
        //            public List<CopilotAIService.AIGeneratedTestCase> testcases { get; set; } = new List<CopilotAIService.AIGeneratedTestCase>();
        //        }

        //        [HttpPost("generate-testcases")]
        //        public async Task<IActionResult> GenerateTestCases([FromBody] List<ApiInfo> endpoints)
        //        {
        //            if (endpoints == null || endpoints.Count == 0)
        //                return BadRequest("No endpoint data provided.");

        //            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        //            using (var package = new ExcelPackage())
        //            {
        //                try
        //                {
        //                    // TestCases sheet
        //                    var ws = package.Workbook.Worksheets.Add("TestCases");
        //                    string[] headers = {
        //                        "Endpoint", "Method", "Test Case",
        //                        "Input Payload", "Payload Type",
        //                        "Expected Status", "Expected Response",
        //                        "Actual Status", "Actual Response", "Test Result"
        //                    };
        //                    for (int i = 0; i < headers.Length; i++)
        //                    {
        //                        ws.Cells[1, i + 1].Value = headers[i];
        //                        ws.Cells[1, i + 1].Style.Font.Bold = true;
        //                        ws.Column(i + 1).Width = 28;
        //                    }
        //                    ws.Column(4).Width = 55;
        //                    ws.Column(1).Width = 60;
        //                    int row = 2;

        //                    // Lookup sheet
        //                    var lookup = package.Workbook.Worksheets.Add("Lookup");
        //                    lookup.Cells[1, 1].Value = "EndpointUrl";
        //                    lookup.Cells[1, 2].Value = "Method";
        //                    lookup.Cells[1, 3].Value = "PayloadTypes";
        //                    lookup.Cells[1, 4].Value = "InputPayload";
        //                    int lookupRow = 2;
        //                    int baseTypeColumn = 10;
        //                    foreach (var ep in endpoints)
        //                    {
        //                        lookup.Cells[lookupRow, 1].Value = ep.url;
        //                        lookup.Cells[lookupRow, 2].Value = ep.method;
        //                        var payloadTypes = ep.parameters?
        //                            .Select(p => p.source)
        //                            .Where(s => !string.IsNullOrWhiteSpace(s))
        //                            .Distinct()
        //                            .ToList() ?? new List<string>();
        //                        lookup.Cells[lookupRow, 3].Value = string.Join(",", payloadTypes);
        //                        lookup.Cells[lookupRow, 4].Value = MakeInputPayloadString(ep);
        //                        int col = baseTypeColumn;
        //                        foreach (var t in payloadTypes)
        //                            lookup.Cells[lookupRow, col++].Value = t;
        //                        if (payloadTypes.Count > 0)
        //                        {
        //                            string name = $"PT_LIST_{lookupRow}";
        //                            var range = lookup.Cells[
        //                                lookupRow, baseTypeColumn,
        //                                lookupRow, baseTypeColumn + payloadTypes.Count - 1
        //                            ];
        //                            package.Workbook.Names.Add(name, range);
        //                        }
        //                        lookupRow++;
        //                    }
        //                    lookup.Hidden = eWorkSheetHidden.Hidden;

        //                    // Bulk AI Call
        //                    var aiService = new CopilotAIService("AIzaSyBPlwSAWu-iZ5HGBzAOzDC0y8UzEjSIAxc");
        //                    AIResponse aiResponse;
        //                    try
        //                    {
        //                        aiResponse = await aiService.GenerateAllTestCases(endpoints);
        //                    }
        //                    catch (Exception ex)
        //                    {
        //                        // If bulk generation fails, fallback to an error-only response so Excel still returns
        //                        aiResponse = new AIResponse
        //                        {
        //                            per_endpoint = endpoints.Select(e => new PerEndpoint
        //                            {
        //                                endpoint = $"{e.url} {e.method}",
        //                                testcases = new List<CopilotAIService.AIGeneratedTestCase>
        //                                {
        //                                    new CopilotAIService.AIGeneratedTestCase
        //                                    {
        //                                        TestCaseName = "AI Generation Failed",
        //                                        InputPayload = "{}",
        //                                        PayloadType = "N/A",
        //                                        ExpectedStatus = 500,
        //                                        ExpectedResponse = new { error = ex.Message }
        //                                    }
        //                                }
        //                            }).ToList(),
        //                            scenario_tests = new List<CopilotAIService.AIGeneratedTestCase>()
        //                        };
        //                    }

        //                    Console.WriteLine(aiResponse.per_endpoint);
        //                    foreach (var epResult in aiResponse.per_endpoint)
        //                    {
        //                        var matchedApi = endpoints.FirstOrDefault(e =>
        //                            string.Equals(e.url, epResult.endpoint, StringComparison.OrdinalIgnoreCase)
        //                            || string.Equals($"{e.url} {e.method}", epResult.endpoint, StringComparison.OrdinalIgnoreCase)
        //                            || string.Equals($"{e.method} {e.url}", epResult.endpoint, StringComparison.OrdinalIgnoreCase)
        //                        );

        //                        foreach (var tc in epResult.testcases)
        //                        {
        //                            ws.Cells[row, 1].Value = matchedApi?.url ?? epResult.endpoint;
        //                            ws.Cells[row, 2].Value = matchedApi?.method ?? ExtractMethodFromEndpointString(epResult.endpoint);
        //                            ws.Cells[row, 3].Value = tc.TestCaseName;
        //                            ws.Cells[row, 4].Value = PrettyPrintJson(tc.InputPayload);
        //                            ws.Cells[row, 5].Value = tc.PayloadType ?? (matchedApi?.parameters?.FirstOrDefault()?.source ?? "");
        //                            ws.Cells[row, 6].Value = tc.ExpectedStatus;
        //                            ws.Cells[row, 7].Value = ExtractMessage(tc.ExpectedResponse);
        //                            row++;
        //                        }
        //                    }

        //                    //Append scenario-based test cases
        //                    if (aiResponse.scenario_tests != null && aiResponse.scenario_tests.Count > 0)
        //                    {
        //                        // Optional: add a header/separator row before scenarios
        //                        ws.Cells[row, 1].Value = "SCENARIO TESTS";
        //                        ws.Cells[row, 1, row, headers.Length].Merge = true;
        //                        ws.Cells[row, 1].Style.Font.Bold = true;
        //                        row++;

        //                        foreach (var sc in aiResponse.scenario_tests)
        //                        {
        //                            ws.Cells[row, 1].Value = sc.Endpoint;
        //                            ws.Cells[row, 2].Value = sc.Method;
        //                            ws.Cells[row, 3].Value = sc.TestCaseName;
        //                            ws.Cells[row, 4].Value = PrettyPrintJson(sc.InputPayload);
        //                            ws.Cells[row, 5].Value = sc.PayloadType;
        //                            ws.Cells[row, 6].Value = sc.ExpectedStatus;
        //                            ws.Cells[row, 7].Value = ExtractMessage(sc.ExpectedResponse);
        //                            row++;
        //                        }
        //                    }

        //                    // Manual 150 rows (unchanged) — adjust start from 'row'
        //                    int dynamicStart = row;
        //                    int dynamicEnd = dynamicStart + 150;
        //                    for (int r = dynamicStart; r <= dynamicEnd; r++)
        //                    {
        //                        var dvEndpoint = ws.DataValidations.AddListValidation($"A{r}");
        //                        dvEndpoint.Formula.ExcelFormula = $"=Lookup!$A$2:$A${lookupRow - 1}";
        //                        ws.Cells[r, 2].Formula =
        //                            $"=IF($A{r}=\"\",\"\",IFERROR(VLOOKUP($A{r},Lookup!$A$2:$D${lookupRow - 1},2,false),\"\"))";
        //                        ws.Cells[r, 4].Formula =
        //                            $"=IFERROR(VLOOKUP($A{r},Lookup!$A$2:$D${lookupRow - 1},4,false),\"\")";
        //                        ws.Cells[r, 4].Style.Locked = false;
        //                        ws.Cells[r, 5].Formula =
        //                            $"=IF($A{r}=\"\",\"\",INDEX(INDIRECT(\"PT_LIST_\" & MATCH($A{r},Lookup!$A$2:$A${lookupRow - 1},0)+1),1))";
        //                        var dvPayload = ws.DataValidations.AddListValidation($"E{r}");
        //                        dvPayload.Formula.ExcelFormula =
        //                            $"=INDIRECT(\"PT_LIST_\" & MATCH($A{r},Lookup!$A$2:$A${lookupRow - 1},0)+1)";
        //                    }

        //                    ws.View.FreezePanes(2, 1);
        //                    ws.Cells.Style.WrapText = true;

        //                    // Return Excel
        //                    var bytes = package.GetAsByteArray();
        //                    return File(
        //                        bytes,
        //                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        //                        "TestCases.xlsx"
        //                    );
        //                }
        //                catch (Exception ex)
        //                {
        //                    return StatusCode(500, new
        //                    {
        //                        message = "Error generating test cases.",
        //                        error = ex.Message
        //                    });
        //                }
        //            }
        //        }

        //        // Helper: try to parse method from returned endpoint string
        //        private static string ExtractMethodFromEndpointString(string ep)
        //        {
        //            if (string.IsNullOrWhiteSpace(ep)) return "";
        //            var tokens = ep.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        //            foreach (var t in tokens)
        //            {
        //                var up = t.ToUpperInvariant();
        //                if (new[] { "GET", "POST", "PUT", "DELETE", "PATCH" }.Contains(up))
        //                    return up;
        //            }
        //            return "";
        //        }

        //        private static string ExtractMessage(object expectedResponse)
        //        {
        //            if (expectedResponse == null)
        //                return "";

        //            try
        //            {
        //                var json = JsonConvert.SerializeObject(expectedResponse);
        //                var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

        //                if (dict == null || dict.Count == 0)
        //                    return json;

        //                var firstValue = dict.First().Value;
        //                return firstValue?.ToString() ?? "";
        //            }
        //            catch
        //            {
        //                return expectedResponse.ToString();
        //            }
        //        }

        //        private static string PrettyPrintJson(object payload)
        //        {
        //            if (payload == null) return "{}";
        //            string jsonStr = payload.ToString();
        //            if (string.IsNullOrEmpty(jsonStr)) return "{}";
        //            try
        //            {
        //                if (payload is not string)
        //                {
        //                    return JsonConvert.SerializeObject(payload, Formatting.Indented);
        //                }
        //                var obj = JsonConvert.DeserializeObject(jsonStr);
        //                return JsonConvert.SerializeObject(obj, Formatting.Indented);
        //            }
        //            catch
        //            {
        //                return jsonStr;
        //            }
        //        }

        //        private static string MakeInputPayloadString(ApiInfo ep)
        //        {
        //            if (ep == null || ep.parameters == null || ep.parameters.Count == 0)
        //                return "{}";

        //            // Make a JSON-like preview string for lookup: { "name":"String", "parentId":"Nullable`1" }
        //            var pairs = ep.parameters.Select(p =>
        //            {
        //                var nameEscaped = p.name.Replace("\"", "\\\"");
        //                var typePreview = p.type?.Replace("\"", "\\\"") ?? "string";
        //                return $"\"{nameEscaped}\": \"{typePreview}\"";
        //            });
        //            return "{ " + string.Join(", ", pairs) + " }";
        //        }


        //        //    public class ApiParameterDto
        //        //    {
        //        //        public string name { get; set; }
        //        //        public string type { get; set; }
        //        //        public string source { get; set; } // Query / Body / Path / FormFile
        //        //    }

        //        //    public class ApiInfo
        //        //    {
        //        //        public string method { get; set; }
        //        //        public string route { get; set; }
        //        //        public string url { get; set; }
        //        //        public List<ApiParameterDto> parameters { get; set; } = new List<ApiParameterDto>();
        //        //    }

        //        //    // Local generated test case types replacing AI service types
        //        //    public class GeneratedTestCase
        //        //    {
        //        //        public string TestCaseName { get; set; }
        //        //        public object InputPayload { get; set; }
        //        //        public string PayloadType { get; set; }
        //        //        public int ExpectedStatus { get; set; }
        //        //        public object ExpectedResponse { get; set; }
        //        //        // optional fields for scenario-based tests
        //        //        public string Endpoint { get; set; }
        //        //        public string Method { get; set; }
        //        //    }

        //        //    public class PerEndpointResult
        //        //    {
        //        //        public string endpoint { get; set; } 
        //        //        public List<GeneratedTestCase> testcases { get; set; } = new List<GeneratedTestCase>();
        //        //    }

        //        //    public class AIResponseReplacement
        //        //    {
        //        //        public List<PerEndpointResult> per_endpoint { get; set; } = new List<PerEndpointResult>();
        //        //        public List<GeneratedTestCase> scenario_tests { get; set; } = new List<GeneratedTestCase>();
        //        //    }

        //        //    [HttpPost("generate-testcases-random")]
        //        //    public async Task<IActionResult> GenerateTestCases([FromBody] List<ApiInfo> endpoints)
        //        //    {
        //        //        if (endpoints == null || endpoints.Count == 0)
        //        //            return BadRequest("No endpoint data provided.");

        //        //        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        //        //        using (var package = new ExcelPackage())
        //        //        {
        //        //            try
        //        //            {
        //        //                // TestCases sheet
        //        //                var ws = package.Workbook.Worksheets.Add("TestCases");
        //        //                string[] headers = {
        //        //                        "Endpoint", "Method", "Test Case",
        //        //                        "Input Payload", "Payload Type",
        //        //                        "Expected Status", "Expected Response",
        //        //                        "Actual Status", "Actual Response", "Test Result"
        //        //                    };
        //        //                for (int i = 0; i < headers.Length; i++)
        //        //                {
        //        //                    ws.Cells[1, i + 1].Value = headers[i];
        //        //                    ws.Cells[1, i + 1].Style.Font.Bold = true;
        //        //                    ws.Column(i + 1).Width = 28;
        //        //                }
        //        //                ws.Column(4).Width = 55;
        //        //                ws.Column(1).Width = 60;
        //        //                int row = 2;

        //        //                // Lookup sheet
        //        //                var lookup = package.Workbook.Worksheets.Add("Lookup");
        //        //                lookup.Cells[1, 1].Value = "EndpointUrl";
        //        //                lookup.Cells[1, 2].Value = "Method";
        //        //                lookup.Cells[1, 3].Value = "PayloadTypes";
        //        //                lookup.Cells[1, 4].Value = "InputPayload";
        //        //                int lookupRow = 2;
        //        //                int baseTypeColumn = 10;
        //        //                foreach (var ep in endpoints)
        //        //                {
        //        //                    lookup.Cells[lookupRow, 1].Value = ep.url;
        //        //                    lookup.Cells[lookupRow, 2].Value = ep.method;
        //        //                    var payloadTypes = ep.parameters?
        //        //                        .Select(p => p.source)
        //        //                        .Where(s => !string.IsNullOrWhiteSpace(s))
        //        //                        .Distinct()
        //        //                        .ToList() ?? new List<string>();
        //        //                    lookup.Cells[lookupRow, 3].Value = string.Join(",", payloadTypes);
        //        //                    lookup.Cells[lookupRow, 4].Value = MakeInputPayloadString(ep);
        //        //                    int col = baseTypeColumn;
        //        //                    foreach (var t in payloadTypes)
        //        //                        lookup.Cells[lookupRow, col++].Value = t;
        //        //                    if (payloadTypes.Count > 0)
        //        //                    {
        //        //                        string name = $"PT_LIST_{lookupRow}";
        //        //                        var range = lookup.Cells[
        //        //                            lookupRow, baseTypeColumn,
        //        //                            lookupRow, baseTypeColumn + payloadTypes.Count - 1
        //        //                        ];
        //        //                        package.Workbook.Names.Add(name, range);
        //        //                    }
        //        //                    lookupRow++;
        //        //                }
        //        //                lookup.Hidden = eWorkSheetHidden.Hidden;

        //        //                // =========== REPLACEMENT: Generate test cases programmatically (no AI) ===========
        //        //                var aiResponse = GenerateAllTestCases(endpoints);

        //        //                // Debug
        //        //                // Console.WriteLine(JsonConvert.SerializeObject(aiResponse.per_endpoint, Formatting.Indented));

        //        //                foreach (var epResult in aiResponse.per_endpoint)
        //        //                {
        //        //                    var matchedApi = endpoints.FirstOrDefault(e =>
        //        //                        string.Equals(e.url, epResult.endpoint, StringComparison.OrdinalIgnoreCase)
        //        //                        || string.Equals($"{e.url} {e.method}", epResult.endpoint, StringComparison.OrdinalIgnoreCase)
        //        //                        || string.Equals($"{e.method} {e.url}", epResult.endpoint, StringComparison.OrdinalIgnoreCase)
        //        //                    );

        //        //                    foreach (var tc in epResult.testcases)
        //        //                    {
        //        //                        ws.Cells[row, 1].Value = matchedApi?.url ?? epResult.endpoint;
        //        //                        ws.Cells[row, 2].Value = matchedApi?.method ?? ExtractMethodFromEndpointString(epResult.endpoint);
        //        //                        ws.Cells[row, 3].Value = tc.TestCaseName;
        //        //                        ws.Cells[row, 4].Value = PrettyPrintJson(tc.InputPayload);
        //        //                        ws.Cells[row, 5].Value = tc.PayloadType ?? (matchedApi?.parameters?.FirstOrDefault()?.source ?? "");
        //        //                        ws.Cells[row, 6].Value = tc.ExpectedStatus;
        //        //                        ws.Cells[row, 7].Value = ExtractMessage(tc.ExpectedResponse);
        //        //                        row++;
        //        //                    }
        //        //                }

        //        //                //Append scenario-based test cases (global scenarios)
        //        //                if (aiResponse.scenario_tests != null && aiResponse.scenario_tests.Count > 0)
        //        //                {
        //        //                    // Optional: add a header/separator row before scenarios
        //        //                    ws.Cells[row, 1].Value = "SCENARIO TESTS";
        //        //                    ws.Cells[row, 1, row, headers.Length].Merge = true;
        //        //                    ws.Cells[row, 1].Style.Font.Bold = true;
        //        //                    row++;

        //        //                    foreach (var sc in aiResponse.scenario_tests)
        //        //                    {
        //        //                        ws.Cells[row, 1].Value = sc.Endpoint;
        //        //                        ws.Cells[row, 2].Value = sc.Method;
        //        //                        ws.Cells[row, 3].Value = sc.TestCaseName;
        //        //                        ws.Cells[row, 4].Value = PrettyPrintJson(sc.InputPayload);
        //        //                        ws.Cells[row, 5].Value = sc.PayloadType;
        //        //                        ws.Cells[row, 6].Value = sc.ExpectedStatus;
        //        //                        ws.Cells[row, 7].Value = ExtractMessage(sc.ExpectedResponse);
        //        //                        row++;
        //        //                    }
        //        //                }

        //        //                // Manual 150 rows (unchanged) — adjust start from 'row'
        //        //                int dynamicStart = row;
        //        //                int dynamicEnd = dynamicStart + 150;
        //        //                for (int r = dynamicStart; r <= dynamicEnd; r++)
        //        //                {
        //        //                    var dvEndpoint = ws.DataValidations.AddListValidation($"A{r}");
        //        //                    dvEndpoint.Formula.ExcelFormula = $"=Lookup!$A$2:$A${lookupRow - 1}";
        //        //                    ws.Cells[r, 2].Formula =
        //        //                        $"=IF($A{r}=\"\",\"\",IFERROR(VLOOKUP($A{r},Lookup!$A$2:$D${lookupRow - 1},2,false),\"\"))";
        //        //                    ws.Cells[r, 4].Formula =
        //        //                        $"=IFERROR(VLOOKUP($A{r},Lookup!$A$2:$D${lookupRow - 1},4,false),\"\")";
        //        //                    ws.Cells[r, 4].Style.Locked = false;
        //        //                    ws.Cells[r, 5].Formula =
        //        //                        $"=IF($A{r}=\"\",\"\",INDEX(INDIRECT(\"PT_LIST_\" & MATCH($A{r},Lookup!$A$2:$A${lookupRow - 1},0)+1),1))";
        //        //                    var dvPayload = ws.DataValidations.AddListValidation($"E{r}");
        //        //                    dvPayload.Formula.ExcelFormula =
        //        //                        $"=INDIRECT(\"PT_LIST_\" & MATCH($A{r},Lookup!$A$2:$A${lookupRow - 1},0)+1)";
        //        //                }

        //        //                ws.View.FreezePanes(2, 1);
        //        //                ws.Cells.Style.WrapText = true;

        //        //                // Return Excel
        //        //                var bytes = package.GetAsByteArray();
        //        //                return File(
        //        //                    bytes,
        //        //                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        //        //                    "TestCases.xlsx"
        //        //                );
        //        //            }
        //        //            catch (Exception ex)
        //        //            {
        //        //                return StatusCode(500, new
        //        //                {
        //        //                    message = "Error generating test cases.",
        //        //                    error = ex.Message
        //        //                });
        //        //            }
        //        //        }
        //        //    }

        //        //    // ====================== Generator Implementation ======================
        //        //    private AIResponseReplacement GenerateAllTestCases(List<ApiInfo> endpoints)
        //        //    {
        //        //        var response = new AIResponseReplacement();
        //        //        foreach (var ep in endpoints)
        //        //        {
        //        //            var per = new PerEndpointResult
        //        //            {
        //        //                endpoint = $"{ep.url} {ep.method}"
        //        //            };

        //        //            // 1) Add a "Valid Payload" baseline
        //        //            var validPayload = BuildPayload(ep.parameters, scenario: "valid");
        //        //            per.testcases.Add(new GeneratedTestCase
        //        //            {
        //        //                TestCaseName = "Valid Payload",
        //        //                InputPayload = validPayload,
        //        //                PayloadType = DeterminePayloadType(ep.parameters),
        //        //                ExpectedStatus = 200,
        //        //                ExpectedResponse = new { message = "Success" },
        //        //                Endpoint = ep.url,
        //        //                Method = ep.method
        //        //            });

        //        //            // 2) For each parameter generate targeted scenarios
        //        //            foreach (var param in ep.parameters)
        //        //            {
        //        //                // Missing Required Field
        //        //                var missing = BuildPayload(ep.parameters, scenario: "missing", targetParam: param.name);
        //        //                per.testcases.Add(new GeneratedTestCase
        //        //                {
        //        //                    TestCaseName = $"Missing Required Field - {param.name}",
        //        //                    InputPayload = missing,
        //        //                    PayloadType = DeterminePayloadType(ep.parameters),
        //        //                    ExpectedStatus = 400,
        //        //                    ExpectedResponse = new { error = $"{param.name} is required" },
        //        //                    Endpoint = ep.url,
        //        //                    Method = ep.method
        //        //                });

        //        //                // Invalid Type
        //        //                var invalidType = BuildPayload(ep.parameters, scenario: "invalid_type", targetParam: param.name);
        //        //                per.testcases.Add(new GeneratedTestCase
        //        //                {
        //        //                    TestCaseName = $"Invalid Type - {param.name}",
        //        //                    InputPayload = invalidType,
        //        //                    PayloadType = DeterminePayloadType(ep.parameters),
        //        //                    ExpectedStatus = 400,
        //        //                    ExpectedResponse = new { error = $"Invalid type for {param.name}" },
        //        //                    Endpoint = ep.url,
        //        //                    Method = ep.method
        //        //                });

        //        //                // Empty string (only meaningful for string-like fields)
        //        //                var emptyStr = BuildPayload(ep.parameters, scenario: "empty_string", targetParam: param.name);
        //        //                per.testcases.Add(new GeneratedTestCase
        //        //                {
        //        //                    TestCaseName = $"Empty String - {param.name}",
        //        //                    InputPayload = emptyStr,
        //        //                    PayloadType = DeterminePayloadType(ep.parameters),
        //        //                    ExpectedStatus = 400,
        //        //                    ExpectedResponse = new { error = $"{param.name} cannot be empty" },
        //        //                    Endpoint = ep.url,
        //        //                    Method = ep.method
        //        //                });

        //        //                // Very long string (if applicable)
        //        //                var longStr = BuildPayload(ep.parameters, scenario: "long_string", targetParam: param.name);
        //        //                per.testcases.Add(new GeneratedTestCase
        //        //                {
        //        //                    TestCaseName = $"Very Long String - {param.name}",
        //        //                    InputPayload = longStr,
        //        //                    PayloadType = DeterminePayloadType(ep.parameters),
        //        //                    ExpectedStatus = 413,
        //        //                    ExpectedResponse = new { error = $"{param.name} too long" },
        //        //                    Endpoint = ep.url,
        //        //                    Method = ep.method
        //        //                });

        //        //                // Boundary Numbers (if numeric)
        //        //                var boundary = BuildPayload(ep.parameters, scenario: "boundary", targetParam: param.name);
        //        //                if (boundary != null)
        //        //                {
        //        //                    per.testcases.Add(new GeneratedTestCase
        //        //                    {
        //        //                        TestCaseName = $"Boundary Values - {param.name}",
        //        //                        InputPayload = boundary,
        //        //                        PayloadType = DeterminePayloadType(ep.parameters),
        //        //                        ExpectedStatus = 400,
        //        //                        ExpectedResponse = new { error = $"Out of range value for {param.name}" },
        //        //                        Endpoint = ep.url,
        //        //                        Method = ep.method
        //        //                    });
        //        //                }

        //        //                // Null Value
        //        //                var nullVal = BuildPayload(ep.parameters, scenario: "null_value", targetParam: param.name);
        //        //                per.testcases.Add(new GeneratedTestCase
        //        //                {
        //        //                    TestCaseName = $"Null Value - {param.name}",
        //        //                    InputPayload = nullVal,
        //        //                    PayloadType = DeterminePayloadType(ep.parameters),
        //        //                    ExpectedStatus = 400,
        //        //                    ExpectedResponse = new { error = $"{param.name} cannot be null" },
        //        //                    Endpoint = ep.url,
        //        //                    Method = ep.method
        //        //                });
        //        //            }

        //        //            response.per_endpoint.Add(per);
        //        //        }

        //        //        // (Optional) Add global scenario tests here if needed (empty currently)
        //        //        response.scenario_tests = new List<GeneratedTestCase>();
        //        //        return response;
        //        //    }

        //        //    // Build payload object depending on scenario
        //        //    // scenario: valid, missing, invalid_type, empty_string, long_string, boundary, null_value
        //        //    private object BuildPayload(List<ApiParameterDto> parameters, string scenario, string targetParam = null)
        //        //    {
        //        //        if (parameters == null || parameters.Count == 0) return new { };

        //        //        // Use JObject to easily omit or set values
        //        //        var obj = new JObject();

        //        //        foreach (var p in parameters)
        //        //        {
        //        //            // All parameters are considered required by user instruction.
        //        //            bool isTarget = string.Equals(p.name, targetParam, StringComparison.OrdinalIgnoreCase);

        //        //            switch (scenario)
        //        //            {
        //        //                case "valid":
        //        //                    obj[p.name] = JToken.FromObject(GenerateRandomValueForType(p.type));
        //        //                    break;

        //        //                case "missing":
        //        //                    if (!isTarget)
        //        //                        obj[p.name] = JToken.FromObject(GenerateRandomValueForType(p.type));
        //        //                    // else omit property
        //        //                    break;

        //        //                case "invalid_type":
        //        //                    if (isTarget)
        //        //                        obj[p.name] = JToken.FromObject(GenerateInvalidTypeValue(p.type));
        //        //                    else
        //        //                        obj[p.name] = JToken.FromObject(GenerateRandomValueForType(p.type));
        //        //                    break;

        //        //                case "empty_string":
        //        //                    if (IsStringType(p.type))
        //        //                    {
        //        //                        if (isTarget)
        //        //                            obj[p.name] = "";
        //        //                        else
        //        //                            obj[p.name] = JToken.FromObject(GenerateRandomValueForType(p.type));
        //        //                    }
        //        //                    else
        //        //                    {
        //        //                        // for non-strings keep valid value
        //        //                        obj[p.name] = JToken.FromObject(GenerateRandomValueForType(p.type));
        //        //                    }
        //        //                    break;

        //        //                case "long_string":
        //        //                    if (IsStringType(p.type))
        //        //                    {
        //        //                        if (isTarget)
        //        //                            obj[p.name] = new string('A', 1024); // 1KB string
        //        //                        else
        //        //                            obj[p.name] = JToken.FromObject(GenerateRandomValueForType(p.type));
        //        //                    }
        //        //                    else
        //        //                    {
        //        //                        obj[p.name] = JToken.FromObject(GenerateRandomValueForType(p.type));
        //        //                    }
        //        //                    break;

        //        //                case "boundary":
        //        //                    if (isTarget && IsNumericType(p.type))
        //        //                    {
        //        //                        // create an array of boundary values to show extreme cases (0,-1,max)
        //        //                        var arr = new JArray();
        //        //                        arr.Add(0);
        //        //                        arr.Add(-1);
        //        //                        if (p.type.Contains("Int") || p.type.ToLower().Contains("int"))
        //        //                            arr.Add(int.MaxValue);
        //        //                        else if (p.type.ToLower().Contains("long"))
        //        //                            arr.Add(long.MaxValue);
        //        //                        else
        //        //                            arr.Add(double.MaxValue);
        //        //                        obj[p.name] = arr;
        //        //                    }
        //        //                    else
        //        //                    {
        //        //                        obj[p.name] = JToken.FromObject(GenerateRandomValueForType(p.type));
        //        //                    }
        //        //                    break;

        //        //                case "null_value":
        //        //                    if (isTarget)
        //        //                        obj[p.name] = JValue.CreateNull();
        //        //                    else
        //        //                        obj[p.name] = JToken.FromObject(GenerateRandomValueForType(p.type));
        //        //                    break;

        //        //                default:
        //        //                    obj[p.name] = JToken.FromObject(GenerateRandomValueForType(p.type));
        //        //                    break;
        //        //            }
        //        //        }

        //        //        return obj;
        //        //    }

        //        //    // Determine the payload type (simple heuristic)
        //        //    private string DeterminePayloadType(List<ApiParameterDto> parameters)
        //        //    {
        //        //        // If any parameter has source FormFile, prefer formfile
        //        //        if (parameters.Any(p => string.Equals(p.source, "FormFile", StringComparison.OrdinalIgnoreCase)))
        //        //            return "formfile";

        //        //        // Default to body for POST/PUT payloads
        //        //        return "body";
        //        //    }

        //        //    // Helpers to check types
        //        //    private bool IsStringType(string type)
        //        //    {
        //        //        if (string.IsNullOrWhiteSpace(type)) return true;
        //        //        var lower = type.ToLower();
        //        //        return lower.Contains("string") || lower.Contains("char") || lower.Contains("guid") || lower.Contains("date") || lower.Contains("email");
        //        //    }

        //        //    private bool IsNumericType(string type)
        //        //    {
        //        //        if (string.IsNullOrWhiteSpace(type)) return false;
        //        //        var lower = type.ToLower();
        //        //        return lower.Contains("int") || lower.Contains("long") || lower.Contains("double") || lower.Contains("float") || lower.Contains("decimal");
        //        //    }

        //        //    // Generate random valid values for a parameter type
        //        //    private object GenerateRandomValueForType(string type)
        //        //    {
        //        //        var rnd = new Random(Guid.NewGuid().GetHashCode());
        //        //        if (string.IsNullOrWhiteSpace(type)) return "string_" + rnd.Next(1000, 9999);

        //        //        var lower = type.ToLower();
        //        //        try
        //        //        {
        //        //            if (lower.Contains("string") || lower.Contains("char"))
        //        //                return "str_" + rnd.Next(1000, 9999);

        //        //            if (lower.Contains("guid"))
        //        //                return Guid.NewGuid().ToString();

        //        //            if (lower.Contains("bool") || lower == "boolean")
        //        //                return rnd.Next(0, 2) == 1;

        //        //            if (lower.Contains("datetime") || lower.Contains("date"))
        //        //                return DateTime.UtcNow.AddMinutes(-rnd.Next(0, 10000)).ToString("o");

        //        //            if (lower.Contains("int") || lower == "number")
        //        //                return rnd.Next(1, 1000);

        //        //            if (lower.Contains("long"))
        //        //                return (long)rnd.Next(1, 1000);

        //        //            if (lower.Contains("double") || lower.Contains("float") || lower.Contains("decimal"))
        //        //                return (rnd.NextDouble() * 1000);

        //        //            if (lower.Contains("email"))
        //        //                return $"user{rnd.Next(1, 9999)}@example.com";

        //        //            if (lower.Contains("file") || lower.Contains("formfile"))
        //        //                return "[BINARY_FILE_PLACEHOLDER]";

        //        //            // fallback
        //        //            return "str_" + rnd.Next(1000, 9999);
        //        //        }
        //        //        catch
        //        //        {
        //        //            return "str_" + rnd.Next(1000, 9999);
        //        //        }
        //        //    }

        //        //    // Generate intentionally wrong type value for invalid_type scenario
        //        //    private object GenerateInvalidTypeValue(string type)
        //        //    {
        //        //        var rnd = new Random(Guid.NewGuid().GetHashCode());
        //        //        var lower = (type ?? "").ToLower();

        //        //        // If expecting numeric, give string. If expecting string, give number. If expecting guid, give "not-a-guid".
        //        //        if (IsNumericType(type))
        //        //            return "invalid_number";

        //        //        if (IsStringType(type))
        //        //            return 12345;

        //        //        if (lower.Contains("bool"))
        //        //            return "notabool";

        //        //        if (lower.Contains("datetime") || lower.Contains("date"))
        //        //            return "32-13-2020";

        //        //        if (lower.Contains("guid"))
        //        //            return "0000-INVALID-GUID";

        //        //        // fallback
        //        //        return "INVALID";
        //        //    }

        //        //    // ====================== Your existing helpers ======================
        //        //    // try to parse method from returned endpoint string
        //        //    private static string ExtractMethodFromEndpointString(string ep)
        //        //    {
        //        //        if (string.IsNullOrWhiteSpace(ep)) return "";
        //        //        var tokens = ep.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        //        //        foreach (var t in tokens)
        //        //        {
        //        //            var up = t.ToUpperInvariant();
        //        //            if (new[] { "GET", "POST", "PUT", "DELETE", "PATCH" }.Contains(up))
        //        //                return up;
        //        //        }
        //        //        return "";
        //        //    }

        //        //    private static string ExtractMessage(object expectedResponse)
        //        //    {
        //        //        if (expectedResponse == null)
        //        //            return "";

        //        //        try
        //        //        {
        //        //            var json = JsonConvert.SerializeObject(expectedResponse);
        //        //            var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

        //        //            if (dict == null || dict.Count == 0)
        //        //                return json;

        //        //            var firstValue = dict.First().Value;
        //        //            return firstValue?.ToString() ?? "";
        //        //        }
        //        //        catch
        //        //        {
        //        //            return expectedResponse.ToString();
        //        //        }
        //        //    }

        //        //    private static string PrettyPrintJson(object payload)
        //        //    {
        //        //        if (payload == null) return "{}";
        //        //        string jsonStr = payload.ToString();
        //        //        if (string.IsNullOrEmpty(jsonStr)) return "{}";
        //        //        try
        //        //        {
        //        //            if (payload is not string)
        //        //            {
        //        //                return JsonConvert.SerializeObject(payload, Formatting.Indented);
        //        //            }
        //        //            var obj = JsonConvert.DeserializeObject(jsonStr);
        //        //            return JsonConvert.SerializeObject(obj, Formatting.Indented);
        //        //        }
        //        //        catch
        //        //        {
        //        //            return jsonStr;
        //        //        }
        //        //    }

        //        //    private static string MakeInputPayloadString(ApiInfo ep)
        //        //    {
        //        //        if (ep == null || ep.parameters == null || ep.parameters.Count == 0)
        //        //            return "{}";

        //        //        // Make a JSON-like preview string for lookup: { "name":"String", "parentId":"Nullable`1" }
        //        //        var pairs = ep.parameters.Select(p =>
        //        //        {
        //        //            var nameEscaped = p.name.Replace("\"", "\\\"");
        //        //            var typePreview = p.type?.Replace("\"", "\\\"") ?? "string";
        //        //            return $"\"{nameEscaped}\": \"{typePreview}\"";
        //        //        });
        //        //        return "{ " + string.Join(", ", pairs) + " }";
        //        //    }

        [HttpPost("generate-testcases-from-swagger")]
        public async Task<IActionResult> GenerateFromSwagger(
     [FromBody] SwaggerRequestDto request)
        {
            var endpoints = SwaggerToApiInfoMapper.Map(
                request.SwaggerJson,
                request.BaseUrl   // 🔥 USE PROVIDED BASE URL
            );

            return await GenerateTestCases(endpoints);
        }


        [HttpPost("generate-testcases")]
        public async Task<IActionResult> GenerateTestCases([FromBody] List<ApiInfo> endpoints)
        {
            if (endpoints == null || endpoints.Count == 0)
                return BadRequest("No endpoint data provided.");

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                try
                {
                    // TestCases sheet
                    var ws = package.Workbook.Worksheets.Add("TestCases");
                    string[] headers = {
                                "Endpoint", "Method", "Test Case",
                                "Input Payload", "Payload Type",
                                "Expected Status", "Expected Response",
                                "Actual Status", "Actual Response", "Test Result"
                            };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        ws.Cells[1, i + 1].Value = headers[i];
                        ws.Cells[1, i + 1].Style.Font.Bold = true;
                        ws.Column(i + 1).Width = 28;
                    }
                    ws.Column(4).Width = 55;
                    ws.Column(1).Width = 60;
                    int row = 2;

                    // Lookup sheet
                    var lookup = package.Workbook.Worksheets.Add("Lookup");
                    lookup.Cells[1, 1].Value = "EndpointUrl";
                    lookup.Cells[1, 2].Value = "Method";
                    lookup.Cells[1, 3].Value = "PayloadTypes";
                    lookup.Cells[1, 4].Value = "InputPayload";
                    int lookupRow = 2;
                    int baseTypeColumn = 10;
                    foreach (var ep in endpoints)
                    {
                        lookup.Cells[lookupRow, 1].Value = ep.url;
                        lookup.Cells[lookupRow, 2].Value = ep.method;
                        var payloadTypes = ep.parameters?
                            .Select(p => p.source)
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Distinct()
                            .ToList() ?? new List<string>();
                        lookup.Cells[lookupRow, 3].Value = string.Join(",", payloadTypes);
                        lookup.Cells[lookupRow, 4].Value = MakeInputPayloadString(ep);
                        int col = baseTypeColumn;
                        foreach (var t in payloadTypes)
                            lookup.Cells[lookupRow, col++].Value = t;
                        if (payloadTypes.Count > 0)
                        {
                            string name = $"PT_LIST_{lookupRow}";
                            var range = lookup.Cells[
                                lookupRow, baseTypeColumn,
                                lookupRow, baseTypeColumn + payloadTypes.Count - 1
                            ];
                            package.Workbook.Names.Add(name, range);
                        }
                        lookupRow++;
                    }
                    lookup.Hidden = eWorkSheetHidden.Hidden;

                    // Bulk AI Call
                    var aiService = new CopilotAIService("");
                    AIResponse aiResponse;
                    try
                    {
                        aiResponse = await aiService.GenerateAllTestCases(endpoints);
                    }
                    catch (Exception ex)
                    {
                        // If bulk generation fails, fallback to an error-only response so Excel still returns
                        aiResponse = new AIResponse
                        {
                            per_endpoint = endpoints.Select(e => new PerEndpoint
                            {
                                endpoint = $"{e.url} {e.method}",
                                testcases = new List<CopilotAIService.AIGeneratedTestCase>
                                        {
                                            new CopilotAIService.AIGeneratedTestCase
                                            {
                                                TestCaseName = "AI Generation Failed",
                                                InputPayload = "{}",
                                                PayloadType = "N/A",
                                                ExpectedStatus = 500,
                                                ExpectedResponse = new { error = ex.Message }
                                            }
                                        }
                            }).ToList(),
                            scenario_tests = new List<CopilotAIService.AIGeneratedTestCase>()
                        };
                    }

                    Console.WriteLine(aiResponse.per_endpoint);
                    foreach (var epResult in aiResponse.per_endpoint)
                    {
                        var matchedApi = endpoints.FirstOrDefault(e =>
                            string.Equals(e.url, epResult.endpoint, StringComparison.OrdinalIgnoreCase)
                            || string.Equals($"{e.url} {e.method}", epResult.endpoint, StringComparison.OrdinalIgnoreCase)
                            || string.Equals($"{e.method} {e.url}", epResult.endpoint, StringComparison.OrdinalIgnoreCase)
                        );

                        foreach (var tc in epResult.testcases)
                        {
                            ws.Cells[row, 1].Value = matchedApi?.url ?? epResult.endpoint;
                            ws.Cells[row, 2].Value = matchedApi?.method ?? ExtractMethodFromEndpointString(epResult.endpoint);
                            ws.Cells[row, 3].Value = tc.TestCaseName;
                            ws.Cells[row, 4].Value = PrettyPrintJson(tc.InputPayload);
                            ws.Cells[row, 5].Value = tc.PayloadType ?? (matchedApi?.parameters?.FirstOrDefault()?.source ?? "");
                            ws.Cells[row, 6].Value = tc.ExpectedStatus;
                            ws.Cells[row, 7].Value = ExtractMessage(tc.ExpectedResponse);
                            row++;
                        }
                    }

                    //Append scenario-based test cases
                    if (aiResponse.scenario_tests != null && aiResponse.scenario_tests.Count > 0)
                    {
                        
  

                        foreach (var sc in aiResponse.scenario_tests)
                        {
                            ws.Cells[row, 1].Value = sc.Endpoint;
                            ws.Cells[row, 2].Value = sc.Method;
                            ws.Cells[row, 3].Value = sc.TestCaseName;
                            ws.Cells[row, 4].Value = PrettyPrintJson(sc.InputPayload);
                            ws.Cells[row, 5].Value = sc.PayloadType;
                            ws.Cells[row, 6].Value = sc.ExpectedStatus;
                            ws.Cells[row, 7].Value = ExtractMessage(sc.ExpectedResponse);
                            row++;
                        }
                    }

                    // Manual 150 rows (unchanged) — adjust start from 'row'
                    int dynamicStart = row;
                    int dynamicEnd = dynamicStart + 150;
                    for (int r = dynamicStart; r <= dynamicEnd; r++)
                    {
                        var dvEndpoint = ws.DataValidations.AddListValidation($"A{r}");
                        dvEndpoint.Formula.ExcelFormula = $"=Lookup!$A$2:$A${lookupRow - 1}";
                        ws.Cells[r, 2].Formula =
                            $"=IF($A{r}=\"\",\"\",IFERROR(VLOOKUP($A{r},Lookup!$A$2:$D${lookupRow - 1},2,false),\"\"))";
                        ws.Cells[r, 4].Formula =
                            $"=IFERROR(VLOOKUP($A{r},Lookup!$A$2:$D${lookupRow - 1},4,false),\"\")";
                        ws.Cells[r, 4].Style.Locked = false;
                        ws.Cells[r, 5].Formula =
                            $"=IF($A{r}=\"\",\"\",INDEX(INDIRECT(\"PT_LIST_\" & MATCH($A{r},Lookup!$A$2:$A${lookupRow - 1},0)+1),1))";
                        var dvPayload = ws.DataValidations.AddListValidation($"E{r}");
                        dvPayload.Formula.ExcelFormula =
                            $"=INDIRECT(\"PT_LIST_\" & MATCH($A{r},Lookup!$A$2:$A${lookupRow - 1},0)+1)";
                    }

                    ws.View.FreezePanes(2, 1);
                    ws.Cells.Style.WrapText = true;

                    // Return Excel
                    var bytes = package.GetAsByteArray();
                    return File(
                        bytes,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "TestCases.xlsx"
                    );
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new
                    {
                        message = "Error generating test cases.",
                        error = ex.Message
                    });
                }
            }
        }

        // Helper: try to parse method from returned endpoint string
        private static string ExtractMethodFromEndpointString(string ep)
        {
            if (string.IsNullOrWhiteSpace(ep)) return "";
            var tokens = ep.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var t in tokens)
            {
                var up = t.ToUpperInvariant();
                if (new[] { "GET", "POST", "PUT", "DELETE", "PATCH" }.Contains(up))
                    return up;
            }
            return "";
        }

        private static string ExtractMessage(object expectedResponse)
        {
            if (expectedResponse == null)
                return "";

            try
            {
                var json = JsonConvert.SerializeObject(expectedResponse);
                var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                if (dict == null || dict.Count == 0)
                    return json;

                var firstValue = dict.First().Value;
                return firstValue?.ToString() ?? "";
            }
            catch
            {
                return expectedResponse.ToString();
            }
        }

        private static string PrettyPrintJson(object payload)
        {
            if (payload == null) return "{}";
            string jsonStr = payload.ToString();
            if (string.IsNullOrEmpty(jsonStr)) return "{}";
            try
            {
                if (payload is not string)
                {
                    return JsonConvert.SerializeObject(payload, Formatting.Indented);
                }
                var obj = JsonConvert.DeserializeObject(jsonStr);
                return JsonConvert.SerializeObject(obj, Formatting.Indented);
            }
            catch
            {
                return jsonStr;
            }
        }

        private static string MakeInputPayloadString(ApiInfo ep)
        {
            if (ep == null || ep.parameters == null || ep.parameters.Count == 0)
                return "{}";

            var pairs = ep.parameters.Select(p =>
            {
                var nameEscaped = p.name.Replace("\"", "\\\"");
                var typePreview = p.type?.Replace("\"", "\\\"") ?? "string";
                return $"\"{nameEscaped}\": \"{typePreview}\"";
            });

            return "{ " + string.Join(", ", pairs) + " }";
        }


    }


    public class ApiInfo
    {
        public string method { get; set; }
        public string route { get; set; }
        public string url { get; set; }

        public InputPayloadType InputPayloadType { get; set; } = InputPayloadType.none;

        public List<ApiParameterDto> parameters { get; set; } = new();

        // 🔥 Only for BODY payloads
        public object SwaggerPayloadTemplate { get; set; }
    }

    public enum InputPayloadType
    {
        file,
        body,
        path,
        query,
        none
    }




    public class SwaggerRequestDto
    {
        public string SwaggerJson { get; set; }
        public string BaseUrl { get; set; }
    }

    public class ApiParameterDto
    {
        public string name { get; set; }
        public string type { get; set; }
        public string source { get; set; } // query, path, body
    }

    public class AIResponse
    {
        public List<PerEndpoint> per_endpoint { get; set; }
        public List<CopilotAIService.AIGeneratedTestCase> scenario_tests { get; set; }
    }

    public class PerEndpoint
    {
        public string endpoint { get; set; }
        public List<CopilotAIService.AIGeneratedTestCase> testcases { get; set; }
    }



}


