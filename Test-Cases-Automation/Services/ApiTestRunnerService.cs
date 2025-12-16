using Newtonsoft.Json.Linq;
using OfficeOpenXml;
using RestSharp;
using dotenv.net;


namespace Test_Cases_Automation.Services
    {
    public class ApiTestRunnerService
    {
        //private readonly string _baseUrl = "https://localhost:7036";

        public async Task<byte[]> ProcessExcelLive(byte[] excelBytes, HttpResponse response)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var ms = new MemoryStream(excelBytes);
            using var package = new ExcelPackage(ms);

            var sheet = package.Workbook.Worksheets[0];

            int total = 0;
            int r = 2;
            int done = 0;

            for (int i = 2; i <= sheet.Dimension.End.Row; i++)
            {
                if (string.IsNullOrWhiteSpace(sheet.Cells[i, 10].Text) || (sheet.Cells[i, 10].Text).Equals("FAIL", StringComparison.Ordinal))
                {
                    if (!string.IsNullOrWhiteSpace(sheet.Cells[i,1].Text))
                    total++;
                    
                }
                
            }

            Console.WriteLine("totalNumber of Rows:" + total);

            var token = await LoginAndGetToken();

            if (token == null)
            {
                sheet.Cells[2, 8].Value = "LOGIN_FAILED";
                sheet.Cells[2, 9].Value = "Unable to authenticate. Check credentials or Auth API.";
                sheet.Cells[2, 10].Value = "FAIL";

                return package.GetAsByteArray();
            }


            while (!string.IsNullOrWhiteSpace(sheet.Cells[r, 1].Text))
            {

                if (string.IsNullOrWhiteSpace(sheet.Cells[r, 10].Text) || (sheet.Cells[r, 10].Text).Equals("FAIL", StringComparison.Ordinal))
                {
                    var endpoint = sheet.Cells[r, 1].Text;
                    Console.WriteLine(endpoint);
                    var method = sheet.Cells[r, 2].Text;
                    Console.WriteLine(method);
                    var bodyJson = sheet.Cells[r, 4].Text;
                    var payloadType = sheet.Cells[r, 5].Text;
                    Console.WriteLine(payloadType);
                    var expectedStatus = int.Parse(sheet.Cells[r, 6].Text);

                    var result = await CallApi(endpoint, method, bodyJson, token, payloadType);

                    // Write result to Excel
                    sheet.Cells[r, 8].Value = result.StatusCode;
                    sheet.Cells[r, 9].Value = result.ResponseBody;
                    sheet.Cells[r, 10].Value = result.StatusCode == expectedStatus ? "PASS" : "FAIL";

                    // Live SSE
                    done++;
                    
                    Console.WriteLine("Done:"+done);
                    var rowResponse = new
                    {
                        index = r - 2,
                        method,
                        endpoint,
                        testCase = sheet.Cells[r, 3].Text,
                        payload = bodyJson,
                        type = payloadType,
                        expectedStatus,
                        expectedResponse = sheet.Cells[r, 7].Text,
                        actualStatus = result.StatusCode,
                        result = result.StatusCode == expectedStatus ? "PASS" : "FAIL",
                        responseBody = result.ResponseBody,
                        progress = Math.Round((double)done / total * 100)
                    };

                    await response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(rowResponse)}\n\n");
                    await response.Body.FlushAsync();

                    await Task.Delay(500);

                    r++;
                }
                else
                    r++;
            }

            return package.GetAsByteArray();
        }

        // Login Function
       public async Task<string?> LoginAndGetToken()
        {
        try
        {
            // Load .env file
            DotEnv.Load();

            string? username = Environment.GetEnvironmentVariable("USERNAME");
            string? password = Environment.GetEnvironmentVariable("PASSWORD");
            string? loginUrl = Environment.GetEnvironmentVariable("LOGIN_URL");

            // Validation
            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(loginUrl))
            {
                Console.WriteLine("Missing .env credentials or LOGIN_URL.");
                return null;
            }

            var client = new RestClient();
            var request = new RestRequest(loginUrl, Method.Post);

            // JSON Body from .env values
            var body = new
            {
                username = username,
                password = password
            };

            request.AddJsonBody(body);

            var response = await client.ExecuteAsync(request);

            // Server unreachable
            if (response.StatusCode == 0)
            {
                Console.WriteLine("Login unreachable: " + response.ErrorMessage);
                return null;
            }

            if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
            {
                Console.WriteLine("Login failed with status: " + response.StatusCode);
                return null;
            }

            var json = JObject.Parse(response.Content);

            string? token = json["accessToken"]?.ToString();

            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("Token missing in response.");
                return null;
            }

            return token;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Login Exception: " + ex.Message);
            return null;
        }
    }


        // API Call Function
        public async Task<(int StatusCode, string ResponseBody)> CallApi(
            string endpoint, string method, string jsonBody, string token, string payloadType)
        {
            try
            {
                var client = new RestClient();
                var request = new RestRequest(endpoint);

                request.AddHeader("Authorization", "Bearer " + token);

                if (payloadType.Equals("file", StringComparison.OrdinalIgnoreCase) || payloadType.Equals("formfile", StringComparison.OrdinalIgnoreCase))
                {
                    // Body contains file path
                    string filePath = jsonBody
                        .Replace("\"", "")
                        .Replace("{", "")
                        .Replace("}", "")
                        .Trim();

                    if (!File.Exists(filePath))
                        return (0, "FILE NOT FOUND: " + filePath);

                    request.AddFile("file", filePath);

                    // For multipart upload
                    request.AlwaysMultipartFormData = true;
                }

                // Handle body parameter
                else if (payloadType.Equals("body", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(jsonBody))
                    {
                        string sanitized = FixJson(jsonBody);
                        request.AddStringBody(sanitized, ContentType.Json);
                    }
                }
                else
                {
                    // QUERY Parameter
                    if (!string.IsNullOrWhiteSpace(jsonBody) && jsonBody != "(none)")
                    {
                        string normalized = jsonBody.Replace("'", "\"");
                        var jObj = JObject.Parse(normalized);

                        foreach (var prop in jObj.Properties())
                        {
                            request.AddQueryParameter(prop.Name, prop.Value?.ToString() ?? "");
                        }
                    }
                }


                request.Method = method.ToUpper() switch
                {
                    "GET" => Method.Get,
                    "POST" => Method.Post,
                    "PUT" => Method.Put,
                    "DELETE" => Method.Delete,
                    _ => Method.Get
                };

                var response = await client.ExecuteAsync(request);

                if (response.StatusCode == 0)
                {
                    return (0,
                        "Unreachable endpoint. Network error: "
                        + (response.ErrorMessage ?? response.ErrorException?.Message)
                    );
                }

                return ((int)response.StatusCode, response.Content ?? "");
            }
            catch (Exception ex)
            {
                return (0, "Exception occurred: " + ex.Message);
            }
        }

        // Fix JSON Input
        public string FixJson(string input)
        {
            input = input.Replace("'", "\"");

            input = System.Text.RegularExpressions.Regex.Replace(
                input,
                @"(?<={|,)\s*(\w+)\s*:",
                "\"$1\":"
            );

            return input;
        }
    }
}

