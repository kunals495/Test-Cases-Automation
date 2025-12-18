using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Test_Cases_Automation.Services
{
    public class SwaggerPayloadBuilder
    {
        private readonly JObject _swagger;

        public SwaggerPayloadBuilder(string swaggerJson)
        {
            _swagger = JObject.Parse(swaggerJson);
        }

        public object BuildFromSchemaRef(string schemaRef)
        {
            var name = schemaRef.Replace("#/components/schemas/", "");
            var schema = _swagger["components"]?["schemas"]?[name];
            return BuildObject(schema);
        }

        private object BuildObject(JToken schema)
        {
            if (schema == null)
                return null;

            // Handle object
            if (schema["type"]?.ToString() == "object")
            {
                var result = new Dictionary<string, object>();

                var properties = schema["properties"] as JObject;
                if (properties == null)
                    return result;

                foreach (var prop in properties.Properties())
                {
                    var propSchema = prop.Value;

                    // $ref
                    if (propSchema["$ref"] != null)
                    {
                        result[prop.Name] =
                            BuildFromSchemaRef(propSchema["$ref"]!.ToString());
                    }
                    // array
                    else if (propSchema["type"]?.ToString() == "array")
                    {
                        var items = propSchema["items"];

                        result[prop.Name] = new[]
                        {
                            items?["$ref"] != null
                                ? BuildFromSchemaRef(items["$ref"]!.ToString())
                                : Primitive(items)
                        };
                    }
                    // primitive
                    else
                    {
                        result[prop.Name] = Primitive(propSchema);
                    }
                }

                return result;
            }

            // Primitive schema
            return Primitive(schema);
        }

        private object Primitive(JToken schema)
        {
            return schema?["type"]?.ToString() switch
            {
                "string" => "string",
                "integer" => 1,
                "number" => 1.0,
                "boolean" => true,
                _ => null
            };
        }
    }
}
