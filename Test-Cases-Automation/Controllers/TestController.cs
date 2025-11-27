using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    }
}
