using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Unisys.Common.Shared;

namespace RestWithASPNETUdemy {
    public class UnisysSwaggerSchemaFilter : ISchemaFilter {
        // swagger open api version
        bool openAPIVersion_SerializeAsV2 = false;

        public UnisysSwaggerSchemaFilter(bool openAPIVersion_SerializeAsV2) {
            this.openAPIVersion_SerializeAsV2 = openAPIVersion_SerializeAsV2;
        }
        public void Apply(OpenApiSchema schema, SchemaFilterContext context) {
            try {
                //For content-type application/xml ,setting the root node of xml with Class name instead of FQDN

                if (schema.Properties.Count != 0) {
                    schema.Xml = new OpenApiXml {
                        Name = context.Type.Name
                    };
                }

                //Remove the keys with assembly Unisys.Common.EISConnectors
                var keys = context.SchemaRepository.Schemas.Keys;
                foreach (var key in keys) {
                    if (key.Contains("Unisys.Common.EISConnectors"))
                        context.SchemaRepository.Schemas.Remove(key);
                }

                // Get all [DataMemberAttribute] of fields
                List<PropertyInfo> tobeIncludedClientMessageFields = GetDataMemberFields(context.Type);

                // Take a copy of FromBody Schema properties
                var openAPIParameters = schema.Properties;

                // Iterate all http request parameters 
                foreach (var item in openAPIParameters) {
                    // Check if HTTP Request parameter name is exist in the data mebmer list or not
                    List<PropertyInfo> dataMemberList = tobeIncludedClientMessageFields.Where(prop => prop.Name.Equals(item.Key)).ToList();
                    if (dataMemberList.Count == 0) {
                        schema.Properties.Remove(item);
                    }
                }

                // add unisys extension properties
                var type = context.Type;
                var propertyMappings = type
                     .GetProperties()
                     .Join(
                      schema.Properties ?? new Dictionary<string, OpenApiSchema>(),
                         x => x.Name.ToLower(),
                         x => x.Key.ToLower(),
                         (x, y) => new KeyValuePair<PropertyInfo, KeyValuePair<string, OpenApiSchema>>(x, y))
                     .ToList();

                foreach (var propertyMapping in propertyMappings) {
                    var propertyInfo = propertyMapping.Key;
                    var propertyNameToSchemaKvp = propertyMapping.Value;

                    foreach (var attribute in propertyInfo.GetCustomAttributes()) {
                        SetSchemaDetails(schema, propertyNameToSchemaKvp, propertyInfo, attribute);
                    }
                }

                // Set All of for 3.0 Schema

                if (!openAPIVersion_SerializeAsV2) {
                    if (context.Type.IsSubclassOf(typeof(Unisys.Message))) {
                        var baseProperties = typeof(Unisys.Message).GetProperties().Where(property => !property.Name.ToLower().Equals("messageclassname")).ToArray();

                        foreach (var item in baseProperties) {
                            schema.Properties.Remove(item.Name);
                        }

                        // For backward compatibility, preserve MessageClassName property
                        if ((!openAPIVersion_SerializeAsV2 && schema.Properties.ContainsKey("MessageClassName"))) {
                            schema.Properties.Remove("MessageClassName");
                        }


                        var clonedSchema = new OpenApiSchema {
                            Properties = schema.Properties,
                            Type = schema.Type,
                            Required = schema.Required
                        };

                        schema.AllOf = new List<OpenApiSchema> {
                     new OpenApiSchema { Reference = new OpenApiReference { Id = typeof(Unisys.Message).FullName, Type = ReferenceType.Schema } },
                    clonedSchema
                 };
                        // reset properties for they are included in allOf, should be null but code does not handle it
                        schema.Properties = new Dictionary<string, OpenApiSchema>();
                    }
                }
            } catch (Exception ex) {
                Logging.WriteLogs(Logging.LogLevel.Error, ex.Message);
            }
        }

        private static List<PropertyInfo> GetDataMemberFields(Type type) {
            List<PropertyInfo> memberFields = new List<PropertyInfo>();
            foreach (var property in type.GetProperties()) {
                List<PropertyInfo> innermemberFields = GetDataMemberFields(property);
                memberFields.AddRange(innermemberFields);
            }
            return memberFields;
        }

        private static List<PropertyInfo> GetDataMemberFields(PropertyInfo property) {
            List<PropertyInfo> memberFields = new List<PropertyInfo>();

            if (Attribute.IsDefined(property, typeof(DataMemberAttribute))) {
                memberFields.Add(property);
            }
            return memberFields;
        }

        private static void SetSchemaDetails(OpenApiSchema parentSchema, KeyValuePair<string, OpenApiSchema> propertyNameToSchemaKvp, PropertyInfo propertyInfo, object propertyAttribute) {
            try {

                var schema = propertyNameToSchemaKvp.Value;

                // Add enum type information once
                if (propertyInfo.PropertyType.IsEnum) {
                    if (!schema.Extensions.ContainsKey("x-ms-enum")) {
                        schema.Extensions.Add("x-ms-enum",
                        new Microsoft.OpenApi.Any.OpenApiObject {
                            ["name"] = new OpenApiString(propertyInfo.PropertyType.Name),
                            ["modelAsString"] = new OpenApiBoolean(false)
                        }
                        );
                    }
                }

                if (propertyAttribute is ReadOnlyAttribute) {
                    schema.ReadOnly = ((ReadOnlyAttribute)propertyAttribute).IsReadOnly;
                }

                if (propertyAttribute is EditableAttribute) {
                    schema.Extensions.Add("x-unisys-editable", new OpenApiBoolean(((EditableAttribute)propertyAttribute).AllowEdit));
                }

                if (propertyAttribute is UIHintAttribute) {
                    schema.Extensions.Add("x-unisys-uihint", new OpenApiString(((UIHintAttribute)propertyAttribute).UIHint.ToString()));
                    try {

                        int i = 1;
                        foreach (System.Collections.Generic.KeyValuePair<string, object> x in ((UIHintAttribute)propertyAttribute).ControlParameters) {
                            schema.Extensions.Add("x-unisys-cp-" + propertyInfo.Name + "-" + i.ToString(), new OpenApiString(x.Key + "," + x.Value));
                            i++;
                        }
                    } catch (Exception e) {
                        // Catch the error and log it into the schema
                        // The most likely cause for this is duplicate key values in the Control Parameter dictionary
                        schema.Extensions.Add("x-unisys-ControlParameter-Error", new OpenApiString(e.Message));
                    }
                }

                if (propertyAttribute is DisplayAttribute) {
                    string desc;
                    if (((DisplayAttribute)propertyAttribute).GetDescription() != null) {
                        desc = ((DisplayAttribute)propertyAttribute).GetDescription();
                        schema.Extensions.Add("x-unisys-description", new OpenApiString(desc));
                        schema.Description = desc;
                    }

                    string name;
                    if (((DisplayAttribute)propertyAttribute).GetName() != null) {
                        name = ((DisplayAttribute)propertyAttribute).GetName();
                        schema.Extensions.Add("x-unisys-name", new OpenApiString(name));
                    }

                    int order;
                    if (((DisplayAttribute)propertyAttribute).GetOrder() != null) {
                        order = ((DisplayAttribute)propertyAttribute).GetOrder().Value;
                        schema.Extensions.Add("x-unisys-order", new OpenApiInteger(order));
                    }
                }

                if (propertyAttribute.GetType().Name == "AdditionalMetadataAttribute") {
                    string name = (string)propertyAttribute.GetType().GetProperty("Name").GetValue(propertyAttribute, null);
                    string value = (string)propertyAttribute.GetType().GetProperty("Value").GetValue(propertyAttribute, null);

                    switch (name) {
                        // Annotations for Xamarin Forms
                        case "DataSourceId":
                        case "DataTextField":
                        case "DataValueField":
                            schema.Extensions.Add("x-unisys-" + name, new OpenApiString(value));

                            break;
                        // Other Annotations - not generated into Swagger spec.
                        case "AutoCapitalize":
                        case "AutoComplete":
                        case "AutoCorrect":
                        case "WhiteSpacePreserve":
                        case "GenAssociatedLabel":
                        case "AccessKey":
                        case "DefaultSelected":
                        case "DataMember":
                        case "RepeatDirection":
                        case "Rows":
                        case "ZeroSuppress":
                        case "RightJustified":
                        case "TextStyle":
                            break;
                        default:
                            break;
                    }
                }
            } catch (Exception ex) {
                Logging.WriteLogs(Logging.LogLevel.Error, ex.Message);
            }
        }

    }
}