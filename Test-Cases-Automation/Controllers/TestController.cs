using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
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


        //public class ApiParameterDto
        //{
        //    public string name { get; set; }
        //    public string type { get; set; }
        //    public string source { get; set; } // Query / Body / Path / FormFile
        //}

        //public class ApiInfo
        //{
        //    public string method { get; set; }
        //    public string route { get; set; }
        //    public string url { get; set; }
        //    public List<ApiParameterDto> parameters { get; set; } = new List<ApiParameterDto>();
        //}

        //[HttpPost("generate-testcases")]
        //public async Task<IActionResult> GenerateTestCases([FromBody] List<ApiInfo> endpoints)
        //{
        //    if (endpoints == null || endpoints.Count == 0)
        //        return BadRequest("No endpoint data provided.");
        //    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        //    using (var package = new ExcelPackage())
        //    {
        //        try
        //        {
        //            // TestCases sheet
        //            var ws = package.Workbook.Worksheets.Add("TestCases");
        //            string[] headers = {
        //        "Endpoint", "Method", "Test Case",
        //        "Input Payload", "Payload Type",
        //        "Expected Status", "Expected Response",
        //        "Actual Status", "Actual Response", "Test Result"
        //    };
        //            for (int i = 0; i < headers.Length; i++)
        //            {
        //                ws.Cells[1, i + 1].Value = headers[i];
        //                ws.Cells[1, i + 1].Style.Font.Bold = true;
        //                ws.Column(i + 1).Width = 28;
        //            }
        //            ws.Column(4).Width = 55;
        //            ws.Column(1).Width = 60;
        //            int row = 2;
        //            // Lookup sheet
        //            var lookup = package.Workbook.Worksheets.Add("Lookup");
        //            lookup.Cells[1, 1].Value = "EndpointUrl";
        //            lookup.Cells[1, 2].Value = "Method";
        //            lookup.Cells[1, 3].Value = "PayloadTypes";
        //            lookup.Cells[1, 4].Value = "InputPayload";
        //            int lookupRow = 2;
        //            int baseTypeColumn = 10;
        //            foreach (var ep in endpoints)
        //            {
        //                lookup.Cells[lookupRow, 1].Value = ep.url;
        //                lookup.Cells[lookupRow, 2].Value = ep.method;
        //                var payloadTypes = ep.parameters?
        //                    .Select(p => p.source)
        //                    .Where(s => !string.IsNullOrWhiteSpace(s))
        //                    .Distinct()
        //                    .ToList() ?? new List<string>();
        //                lookup.Cells[lookupRow, 3].Value = string.Join(",", payloadTypes);
        //                lookup.Cells[lookupRow, 4].Value = MakeInputPayloadString(ep);  // Updated helper below
        //                int col = baseTypeColumn;
        //                foreach (var t in payloadTypes)
        //                    lookup.Cells[lookupRow, col++].Value = t;
        //                if (payloadTypes.Count > 0)
        //                {
        //                    string name = $"PT_LIST_{lookupRow}";
        //                    var range = lookup.Cells[
        //                        lookupRow, baseTypeColumn,
        //                        lookupRow, baseTypeColumn + payloadTypes.Count - 1
        //                    ];
        //                    package.Workbook.Names.Add(name, range);
        //                }
        //                lookupRow++;
        //            }
        //            lookup.Hidden = eWorkSheetHidden.Hidden;
        //            // Call AI
        //            var aiService = new CopilotAIService("AIzaSyCehU50iXOQuhHoblHRtj4HLKiQqir6yhY");  // TODO: Move to config
        //            foreach (var ep in endpoints)
        //            {
        //                List<CopilotAIService.AIGeneratedTestCase> aiCases;
        //                try
        //                {
        //                    aiCases = await aiService.GenerateTestCases(ep);
        //                }
        //                catch (Exception ex)
        //                {
        //                    aiCases = new List<CopilotAIService.AIGeneratedTestCase>
        //            {
        //                new CopilotAIService.AIGeneratedTestCase
        //                {
        //                    TestCaseName = "AI Generation Failed",
        //                    InputPayload = "{}", 
        //                    PayloadType = "N/A",
        //                    ExpectedStatus = 500,
        //                    ExpectedResponse = new { error = ex.Message }
        //                }
        //            };
        //                }
        //                foreach (var tc in aiCases)
        //                {
        //                    ws.Cells[row, 1].Value = ep.url;
        //                    ws.Cells[row, 2].Value = ep.method;
        //                    ws.Cells[row, 3].Value = tc.TestCaseName;
        //                    // Pretty-print InputPayload (handles string JSON from AI)
        //                    ws.Cells[row, 4].Value = PrettyPrintJson(tc.InputPayload);
        //                    // SET PAYLOAD TYPE FROM ENDPOINT METADATA (unchanged)
        //                    var firstPayloadType = ep.parameters?.FirstOrDefault()?.source ?? "";
        //                    ws.Cells[row, 5].Value = firstPayloadType;
        //                    ws.Cells[row, 6].Value = tc.ExpectedStatus;
        //                    ws.Cells[row, 7].Value = ExtractMessage(tc.ExpectedResponse);
        //                    row++;
        //                }
        //            }
        //            // Manual 150 rows (unchanged)
        //            int dynamicStart = row;
        //            int dynamicEnd = dynamicStart + 150;
        //            for (int r = dynamicStart; r <= dynamicEnd; r++)
        //            {
        //                var dvEndpoint = ws.DataValidations.AddListValidation($"A{r}");
        //                dvEndpoint.Formula.ExcelFormula = $"=Lookup!$A$2:$A${lookupRow - 1}";
        //                ws.Cells[r, 2].Formula =
        //                    $"=IF($A{r}=\"\",\"\",IFERROR(VLOOKUP($A{r},Lookup!$A$2:$D${lookupRow - 1},2,false),\"\"))";
        //                ws.Cells[r, 4].Formula =
        //                    $"=IFERROR(VLOOKUP($A{r},Lookup!$A$2:$D${lookupRow - 1},4,false),\"\")";
        //                ws.Cells[r, 4].Style.Locked = false;
        //                ws.Cells[r, 5].Formula =
        //                    $"=IF($A{r}=\"\",\"\",INDEX(INDIRECT(\"PT_LIST_\" & MATCH($A{r},Lookup!$A$2:$A${lookupRow - 1},0)+1),1))";
        //                var dvPayload = ws.DataValidations.AddListValidation($"E{r}");
        //                dvPayload.Formula.ExcelFormula =
        //                    $"=INDIRECT(\"PT_LIST_\" & MATCH($A{r},Lookup!$A$2:$A${lookupRow - 1},0)+1)";
        //            }
        //            ws.View.FreezePanes(2, 1);
        //            ws.Cells.Style.WrapText = true;
        //            // Return Excel (unchanged)
        //            var bytes = package.GetAsByteArray();
        //            return File(
        //                bytes,
        //                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        //                "TestCases.xlsx"
        //            );
        //        }
        //        catch (Exception ex)
        //        {
        //            return StatusCode(500, new
        //            {
        //                message = "Error generating test cases.",
        //                error = ex.Message
        //            });
        //        }
        //    }
        //}

        //private static string ExtractMessage(object expectedResponse)
        //{
        //    if (expectedResponse == null)
        //        return "";

        //    try
        //    {
        //        // Convert to JSON string
        //        var json = JsonConvert.SerializeObject(expectedResponse);

        //        // Convert to dictionary
        //        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

        //        if (dict == null || dict.Count == 0)
        //            return json;

        //        // return FIRST value from the object
        //        var firstValue = dict.First().Value;

        //        return firstValue?.ToString() ?? "";
        //    }
        //    catch
        //    {
        //        return expectedResponse.ToString();
        //    }
        //}


        //private static string PrettyPrintJson(object payload)
        //{
        //    if (payload == null) return "{}";
        //    string jsonStr = payload.ToString();
        //    if (string.IsNullOrEmpty(jsonStr)) return "{}";
        //    try
        //    {
        //        // If it's already an object/JObject, serialize directly
        //        if (payload is not string)
        //        {
        //            return JsonConvert.SerializeObject(payload, Formatting.Indented);
        //        }
        //        // Parse string (escaped JSON from AI) and re-serialize
        //        var obj = JsonConvert.DeserializeObject(jsonStr);
        //        return JsonConvert.SerializeObject(obj, Formatting.Indented);
        //    }
        //    catch
        //    {
        //        return jsonStr;
        //    }
        //}

        //private static string MakeInputPayloadString(ApiInfo ep)
        //{
        //    if (ep == null || ep.parameters == null || ep.parameters.Count == 0)
        //        return "{}";
        //    var pairs = ep.parameters.Select(p =>
        //        $"{p.name}: \"{p.type}\""  
        //    );
        //    return "{ " + string.Join(", ", pairs) + " }";
        //}

        public class ApiParameterDto
        {
            public string name { get; set; }
            public string type { get; set; }
            public string source { get; set; } // Query / Body / Path / FormFile
        }

        public class ApiInfo
        {
            public string method { get; set; }
            public string route { get; set; }
            public string url { get; set; }
            public List<ApiParameterDto> parameters { get; set; } = new List<ApiParameterDto>();
        }


        public class AIResponse
        {
            public List<PerEndpoint> per_endpoint { get; set; } = new List<PerEndpoint>();
            public List<CopilotAIService.AIGeneratedTestCase> scenario_tests { get; set; } = new List<CopilotAIService.AIGeneratedTestCase>();
        }

        public class PerEndpoint
        {
            public string endpoint { get; set; } // we will use "url method" or just url depending on prompt
            public List<CopilotAIService.AIGeneratedTestCase> testcases { get; set; } = new List<CopilotAIService.AIGeneratedTestCase>();
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
                    var aiService = new CopilotAIService("AIzaSyDrxcQCw6HyX0R3T6qnfj_gyJYzoXnhrOg"); 
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
                        // Optional: add a header/separator row before scenarios
                        ws.Cells[row, 1].Value = "SCENARIO TESTS";
                        ws.Cells[row, 1, row, headers.Length].Merge = true;
                        ws.Cells[row, 1].Style.Font.Bold = true;
                        row++;

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

            // Make a JSON-like preview string for lookup: { "name":"String", "parentId":"Nullable`1" }
            var pairs = ep.parameters.Select(p =>
            {
                var nameEscaped = p.name.Replace("\"", "\\\"");
                var typePreview = p.type?.Replace("\"", "\\\"") ?? "string";
                return $"\"{nameEscaped}\": \"{typePreview}\"";
            });
            return "{ " + string.Join(", ", pairs) + " }";
        }
    }
}

