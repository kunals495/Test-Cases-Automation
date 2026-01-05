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

                    bool hasQueryParams = false;
                    bool hasPathParams = false;
                    bool hasBodyParams = false;
                    bool hasFormFileParams = false;

                    // Parameters (query, path, header)
                    foreach (var p in method.Value["parameters"] ?? new JArray())
                    {
                        var paramIn = p["in"]?.ToString()?.ToLower();
                        var paramType = p["schema"]?["type"]?.ToString() ?? "string";
                        var paramFormat = p["schema"]?["format"]?.ToString();

                        api.parameters.Add(new ApiParameterDto
                        {
                            name = p["name"]?.ToString(),
                            type = paramType,
                            source = paramIn // query, path, header
                        });

                        if (paramIn == "query") hasQueryParams = true;
                        if (paramIn == "path") hasPathParams = true;
                    }

                    // Request Body → Check for JSON body or multipart/form-data (file upload)
                    var reqBody = method.Value["requestBody"];
                    if (reqBody != null)
                    {
                        var content = reqBody["content"] as JObject;
                        if (content != null)
                        {
                            // Check for multipart/form-data (file upload)
                            if (content["multipart/form-data"] != null)
                            {
                                hasFormFileParams = true;
                                var formSchema = content["multipart/form-data"]?["schema"];
                                var formProperties = formSchema?["properties"] as JObject;

                                if (formProperties != null)
                                {
                                    foreach (var prop in formProperties.Properties())
                                    {
                                        var propFormat = prop.Value["format"]?.ToString();
                                        api.parameters.Add(new ApiParameterDto
                                        {
                                            name = prop.Name,
                                            type = propFormat == "binary" ? "IFormFile" : prop.Value["type"]?.ToString(),
                                            source = "formfile"
                                        });
                                    }
                                }

                                api.InputPayloadType = InputPayloadType.file;
                            }
                            // Check for application/json (body)
                            else if (content["application/json"] != null)
                            {
                                hasBodyParams = true;
                                var schema = content["application/json"]?["schema"];

                                if (schema?["$ref"] != null)
                                {
                                    // Build template from schema reference
                                    api.SwaggerPayloadTemplate = builder.BuildFromSchemaRef(schema["$ref"].ToString());

                                    // Extract properties for parameters list
                                    var schemaName = schema["$ref"].ToString().Replace("#/components/schemas/", "");
                                    var schemaObj = swagger["components"]?["schemas"]?[schemaName];
                                    var properties = schemaObj?["properties"] as JObject;

                                    if (properties != null)
                                    {
                                        foreach (var prop in properties.Properties())
                                        {
                                            var propType = prop.Value["type"]?.ToString() ?? "string";
                                            api.parameters.Add(new ApiParameterDto
                                            {
                                                name = prop.Name,
                                                type = propType,
                                                source = "body"
                                            });
                                        }
                                    }
                                }
                                else if (schema?["properties"] != null)
                                {
                                    // Inline schema
                                    var properties = schema["properties"] as JObject;
                                    if (properties != null)
                                    {
                                        foreach (var prop in properties.Properties())
                                        {
                                            var propType = prop.Value["type"]?.ToString() ?? "string";
                                            api.parameters.Add(new ApiParameterDto
                                            {
                                                name = prop.Name,
                                                type = propType,
                                                source = "body"
                                            });
                                        }
                                    }
                                }

                                api.InputPayloadType = InputPayloadType.body;
                            }
                        }
                    }

                    // Determine overall InputPayloadType based on priority
                    if (api.InputPayloadType == InputPayloadType.none)
                    {
                        if (hasFormFileParams)
                            api.InputPayloadType = InputPayloadType.file;
                        else if (hasBodyParams)
                            api.InputPayloadType = InputPayloadType.body;
                        else if (hasQueryParams)
                            api.InputPayloadType = InputPayloadType.query;
                        else if (hasPathParams)
                            api.InputPayloadType = InputPayloadType.path;
                        else
                            api.InputPayloadType = InputPayloadType.none;
                    }

                    list.Add(api);
                }
            }

            return list;
        }
    }
}