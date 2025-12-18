using Newtonsoft.Json.Linq;
using Test_Cases_Automation.Controllers;

namespace Test_Cases_Automation.Services
{
    public static class SwaggerToApiInfoMapper
    {
        public static List<ApiInfo> Map(string swaggerJson, string baseUrl)
        {
            var swagger = JObject.Parse(swaggerJson);
            var builder = new SwaggerPayloadBuilder(swaggerJson);

            var list = new List<ApiInfo>();
            var paths = swagger["paths"] as JObject;

            foreach (var path in paths)
            {
                foreach (var method in path.Value.Children<JProperty>())
                {
                    var api = new ApiInfo
                    {
                        method = method.Name.ToUpper(),
                        route = path.Key,
                        url = baseUrl + path.Key
                    };

                    // Parameters
                    foreach (var p in method.Value["parameters"] ?? new JArray())
                    {
                        api.parameters.Add(new ApiParameterDto
                        {
                            name = p["name"]?.ToString(),
                            type = p["schema"]?["type"]?.ToString(),
                            source = p["in"]?.ToString()
                        });
                    }

                    // Request Body → Payload
                    var reqBody = method.Value["requestBody"]?["content"]?["application/json"]?["schema"];
                    if (reqBody?["$ref"] != null)
                    {
                        api.SwaggerPayloadTemplate =
                            builder.BuildFromSchemaRef(reqBody["$ref"].ToString());
                    }

                    list.Add(api);
                }
            }
            return list;
        }
    }
 }
