using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;
using OfficeOpenXml.DataValidation;
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

        [HttpPost("generate-testcases")]
        public IActionResult GenerateTestCases([FromBody] List<ApiInfo> endpoints)
        {
            if (endpoints == null || endpoints.Count == 0)
                return BadRequest("No endpoint data provided.");

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage())
            {
                // TestCases Sheet
                var ws = package.Workbook.Worksheets.Add("TestCases");

                string[] headers = {
                    "Endpoint", "Method", "Test Case",
                    "Input Payload", "Payload Type",
                    "Expected Status", "Expected Response",
                    "Actual Status", "Actual Response",
                    "Test Result"
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


                // Lookup Sheet 
                var lookupSheet = package.Workbook.Worksheets.Add("Lookup");

                lookupSheet.Cells[1, 1].Value = "EndpointUrl";
                lookupSheet.Cells[1, 2].Value = "Method";
                lookupSheet.Cells[1, 3].Value = "PayloadTypes";
                lookupSheet.Cells[1, 4].Value = "InputPayload";

                int lookupRow = 2;
                int baseTypeColumn = 10; // J Column

                foreach (var ep in endpoints)
                {
                    lookupSheet.Cells[lookupRow, 1].Value = ep.url;
                    lookupSheet.Cells[lookupRow, 2].Value = ep.method;

                    var payloadTypes = ep.parameters?
                        .Select(p => p.source)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct()
                        .ToList() ?? new List<string>();

                    lookupSheet.Cells[lookupRow, 3].Value = string.Join(",", payloadTypes);
                    lookupSheet.Cells[lookupRow, 4].Value = MakeInputPayloadString(ep);

                    int col = baseTypeColumn;
                    foreach (var t in payloadTypes)
                    {
                        lookupSheet.Cells[lookupRow, col].Value = t;
                        col++;
                    }

                    if (payloadTypes.Count > 0)
                    {
                        string name = $"PT_LIST_{lookupRow}";
                        var range = lookupSheet.Cells[
                            lookupRow,
                            baseTypeColumn,
                            lookupRow,
                            baseTypeColumn + payloadTypes.Count - 1
                        ];
                        package.Workbook.Names.Add(name, range);
                    }

                    lookupRow++;
                }

                lookupSheet.Hidden = eWorkSheetHidden.Hidden;

                // Auto-Generate Test Cases
                foreach (var ep in endpoints)
                {
                    string endpoint = ep.url;
                    string method = ep.method;

                    var parameters = ep.parameters;

                    // Valid Payload Test
                    ws.Cells[row, 1].Value = endpoint;
                    ws.Cells[row, 2].Value = method;
                    ws.Cells[row, 3].Value = "Valid Payload";
                    ws.Cells[row, 4].Value = MakeInputPayloadString(ep);

                    string firstPayloadType = parameters?
                        .Select(p => p.source)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct()
                        .FirstOrDefault() ?? "";

                    ws.Cells[row, 5].Value = firstPayloadType;
                    ws.Cells[row, 6].Value = 200;
                    ws.Cells[row, 7].Value = "Success expected";

                    row++;

                    // Missing Parameter Tests
                    if (parameters != null)
                    {
                        foreach (var p in parameters)
                        {
                            ws.Cells[row, 1].Value = endpoint;
                            ws.Cells[row, 2].Value = method;
                            ws.Cells[row, 3].Value = $"Missing parameter: {p.name}";
                            ws.Cells[row, 4].Value = MakePayloadMissingParameter(ep, p);
                            ws.Cells[row, 5].Value = p.source;
                            ws.Cells[row, 6].Value = 400;
                            ws.Cells[row, 7].Value = "Validation error expected";
                            row++;
                        }
                    }

                    // Invalid Type Tests
                    if (parameters != null)
                    {
                        foreach (var p in parameters)
                        {
                            ws.Cells[row, 1].Value = endpoint;
                            ws.Cells[row, 2].Value = method;
                            ws.Cells[row, 3].Value = $"Invalid type for: {p.name}";
                            ws.Cells[row, 4].Value = MakePayloadInvalidType(ep, p);
                            ws.Cells[row, 5].Value = p.source;
                            ws.Cells[row, 6].Value = 400;
                            ws.Cells[row, 7].Value = "Invalid type expected";
                            row++;
                        }
                    }
                }

                // Manual Dropdown Section
                int dynamicStartRow = row;
                int dynamicEndRow = dynamicStartRow + 150;

                for (int r = dynamicStartRow; r <= dynamicEndRow; r++)
                {
                    // Endpoint Dropdown
                    var dv1 = ws.DataValidations.AddListValidation($"A{r}");
                    dv1.Formula.ExcelFormula = $"=Lookup!$A$2:$A${lookupRow - 1}";
                    dv1.ShowErrorMessage = true;

                    // Auto Method
                    ws.Cells[r, 2].Formula =
                        $"=IF($A{r}=\"\",\"\",IFERROR(VLOOKUP($A{r},Lookup!$A$2:$D${lookupRow - 1},2,false),\"\"))";

                    // Auto Input Payload (editable)
                    ws.Cells[r, 4].Formula =
                        $"=IFERROR(VLOOKUP($A{r},Lookup!$A$2:$D${lookupRow - 1},4,false),\"\")";
                    ws.Cells[r, 4].Style.Locked = false;

                    // Auto Payload Type
                    ws.Cells[r, 5].Formula =
                        $"=IF($A{r}=\"\",\"\",INDEX(INDIRECT(\"PT_LIST_\" & MATCH($A{r},Lookup!$A$2:$A${lookupRow - 1},0)+1),1))";

                    // Payload Type Dropdown
                    var dv2 = ws.DataValidations.AddListValidation($"E{r}");
                    dv2.Formula.ExcelFormula =
                        $"=INDIRECT(\"PT_LIST_\" & MATCH($A{r},Lookup!$A$2:$A${lookupRow - 1},0)+1)";
                    dv2.ShowErrorMessage = true;
                }


                ws.View.FreezePanes(2, 1);
                ws.Cells.Style.WrapText = true;


                // Return File
                var bytes = package.GetAsByteArray();
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "TestCases.xlsx");
            }
        }



        private static string MakeInputPayloadString(ApiInfo ep)
        {
            if (ep == null || ep.parameters == null || ep.parameters.Count == 0)
                return "{}";

            var pairs = ep.parameters.Select(p =>
                $"{p.name}:'{p.type}'"
            );

            return "{ " + string.Join(", ", pairs) + " }";
        }

        private static string MakePayloadMissingParameter(ApiInfo ep, ApiParameterDto missing)
        {
            var parts = ep.parameters
                .Where(p => p.name != missing.name)
                .Select(p => $"{p.name}:'{p.type}'");

            return "{ " + string.Join(", ", parts) + " }";
        }

        private static string MakePayloadInvalidType(ApiInfo ep, ApiParameterDto invalidParam)
        {
            var parts = ep.parameters.Select(p =>
            {
                if (p.name == invalidParam.name)
                    return $"{p.name}:'INVALID'";
                return $"{p.name}:'{p.type}'";
            });

            return "{ " + string.Join(", ", parts) + " }";
        }





    }
}

