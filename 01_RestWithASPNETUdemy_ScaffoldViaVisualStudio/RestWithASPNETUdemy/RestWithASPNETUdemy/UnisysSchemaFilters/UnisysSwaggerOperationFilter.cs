using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Unisys.Common.Shared;

namespace RestWithASPNETUdemy {
    /// <summary>
    /// This class includes swagger schema based on DataMember Attribute
    /// </summary>
    public class UnisysSwaggerOperationFilter : IOperationFilter {
        // Swagger open api version
        bool openAPIVersion_SerializeAsV2 = false;


        public UnisysSwaggerOperationFilter(bool openAPIVersion_SerializeAsV2) {
            this.openAPIVersion_SerializeAsV2 = openAPIVersion_SerializeAsV2;
        }

        public void Apply(OpenApiOperation operation, OperationFilterContext context) {
            try {
                RemoveNonDataMemberFields(operation, context);

                // Set all-of for openapi schema
                SetAnyOfSchema(operation, context);

            } catch (Exception ex) {
                Logging.WriteLogs(Logging.LogLevel.Error, ex.Message);
            }

        }

        #region Private Methods

        /// <summary>
        /// RemoveNonDataMemberFields
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="context"></param>
        private void RemoveNonDataMemberFields(OpenApiOperation operation, OperationFilterContext context) {
            RemoveHTTPParamsNonDataMemberFields(operation, context);
            RemoveHTTPSchemaNonDataMemberFields(operation, context);
        }

        /// <summary>
        /// RemoveHTTPParamsNonDataMemberFields
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="context"></param>
        private void RemoveHTTPParamsNonDataMemberFields(OpenApiOperation operation, OperationFilterContext context) {

            // Action method C# model parameter
            ParameterInfo[] actionparams = context.MethodInfo.GetParameters();

            // When operation contains parameters
            if (actionparams.Length > 0) {
                //Skip if Action Paramters are not of type unisys.Message
                if (!actionparams[0].ParameterType.BaseType.Equals(typeof(Unisys.Message))) {
                    return;
                }

                // Get all [DataMemberAttribute] of simple fields
                List<PropertyInfo> tobeIncludedClientMessageFields = GetDataMemberFields(actionparams);

                // Remove Message Type, Discriminator
                List<string> tobeRemobedProperies = new List<string> { "MessageType" };

                //Remove message class name for Open API version V3 and later
                if (!openAPIVersion_SerializeAsV2) {
                    tobeRemobedProperies.Add("MessageClassName");
                }

                tobeIncludedClientMessageFields = tobeIncludedClientMessageFields.Where(property => !tobeRemobedProperies.Contains(property.Name)).ToList();

                // Take a copy of HTTP parameters
                List<OpenApiParameter> httpActionParameters = operation.Parameters.ToList();

                // Iterate all http request parameters 
                foreach (var item in httpActionParameters) {
                    // Check if HTTP Request parameter name is exist in the data mebmer list or not
                    List<PropertyInfo> dataMemberList = tobeIncludedClientMessageFields.Where(prop =>
                      (item.Name.LastIndexOf(".") > 0 ?
                       prop.Name.Equals(item.Name.Remove(0, item.Name.LastIndexOf(".") + 1)) : prop.Name.Equals(item.Name))).ToList();

                    if (dataMemberList.Count == 0) {
                        operation.Parameters.Remove(item);
                    }
                }
            }
        }

        /// <summary>
        /// RemoveHTTPSchemaNonDataMemberFields
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="context"></param>
        private void RemoveHTTPSchemaNonDataMemberFields(OpenApiOperation operation, OperationFilterContext context) {

            // Action method C# model parameter
            ParameterInfo[] actionparams = context.MethodInfo.GetParameters();

            // When operation contains parameters
            if (actionparams.Length > 0) {
                //Skip if Action Paramters are not of type unisys.Message
                if (!actionparams[0].ParameterType.BaseType.Equals(typeof(Unisys.Message))) {
                    return;
                }
                // Get Request body
                OpenApiRequestBody openApiRequestBody = operation.RequestBody;

                if (operation.RequestBody != null) {
                    // Schema of HTTP Content
                    OpenApiSchema openApiSchema = null;

                    // Copy of Request body content
                    IDictionary<string, OpenApiMediaType> openApiMediaType = openApiRequestBody.Content;

                    if (openApiMediaType.Count > 0) {
                        // Take schema 
                        foreach (var item in openApiMediaType) {
                            openApiSchema = item.Value.Schema;
                            break;
                        }
                    }

                    // Take a copy of FromBody Schema 
                    var openAPIParameters = openApiSchema.Properties;


                    // Get all [DataMemberAttribute] of fields
                    List<PropertyInfo> tobeIncludedClientMessageFields = GetDataMemberFields(actionparams);

                    // Remove Message Type, Discriminator
                    List<string> tobeRemobedProperies = new List<string> { "MessageType" };

                    //Remove message class name for Open API version V3 and later
                    if (!openAPIVersion_SerializeAsV2) {
                        tobeRemobedProperies.Add("MessageClassName");
                    }

                    tobeIncludedClientMessageFields = tobeIncludedClientMessageFields.Where(property => !tobeRemobedProperies.Contains(property.Name)).ToList();

                    // Iterate all http request parameters 
                    foreach (var item in openAPIParameters) {
                        // Check if HTTP Request parameter name is exist in the data mebmer list or not
                        List<PropertyInfo> dataMemberList = tobeIncludedClientMessageFields.Where(prop =>
                          (item.Key.LastIndexOf(".") > 0 ?
                           prop.Name.Equals(item.Key.Remove(0, item.Key.LastIndexOf(".") + 1)) : prop.Name.Equals(item.Key))).ToList();

                        if (dataMemberList.Count == 0) {
                            openApiSchema.Properties.Remove(item);
                        }
                    }
                    var fromFormParameter = context.MethodInfo.GetParameters()
                    .FirstOrDefault(p => p.IsDefined(typeof(Microsoft.AspNetCore.Mvc.FromFormAttribute), true));
                    if (fromFormParameter != null) {
                        var FormMediaType = new OpenApiMediaType();
                        FormMediaType.Schema = openApiSchema;
                        FormMediaType.Schema.Properties = openApiSchema.Properties;
                        operation.RequestBody = new OpenApiRequestBody {
                            Content =
                                    {
                                        ["multipart/form-data"] = FormMediaType
                                    }
                        };
                    }

                }

            }
        }

        /// <summary>
        /// GetDataMemberFields
        /// </summary>
        /// <param name="parameterList"></param>
        /// <returns></returns>
        private List<PropertyInfo> GetDataMemberFields(ParameterInfo[] parameterList) {
            List<PropertyInfo> memberFields = new List<PropertyInfo>();

            foreach (var paramInfo in parameterList) {
                // For each proprties check Datameber attribute
                foreach (var property in paramInfo.ParameterType.GetProperties()) {
                    memberFields.AddRange(GetDataMemberFields(property));
                }


            }
            return memberFields;
        }

        /// <summary>
        /// GetDataMemberFields
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        private List<PropertyInfo> GetDataMemberFields(PropertyInfo property) {
            List<PropertyInfo> memberFields = new List<PropertyInfo>();

            // Check property is set as Datameber attribute
            if (Attribute.IsDefined(property, typeof(DataMemberAttribute))) {
                // For Nested class, find its properties
                if (property.PropertyType.BaseType == typeof(Unisys.Message) ||
                    property.PropertyType.BaseType == typeof(Unisys.InnerMessage)
                    && !property.PropertyType.IsPrimitive) {
                    foreach (var prop in property.PropertyType.GetProperties()) {
                        List<PropertyInfo> innermemberFields = GetDataMemberFields(prop);
                        memberFields.AddRange(innermemberFields);
                    }
                } else {
                    memberFields.Add(property);
                }
            }
            return memberFields;
        }

        /// <summary>
        /// This method sets SetAnyOfSchema
        /// </summary>
        /// <param name="operation">operation</param>
        /// <param name="context">context</param>
        private void SetAnyOfSchema(OpenApiOperation operation, OperationFilterContext context) {
            try {
                // Set One-Off Schema except > V2 schema
                if (!openAPIVersion_SerializeAsV2) {
                    Dictionary<int, List<string>> responsetypes = new Dictionary<int, List<string>>();


                    // Read  ProducesResponseTypeAttributes controller level
                    var prodocuesTypeCollection = context.MethodInfo.ReflectedType.CustomAttributes.
                    Where(attribute => attribute.AttributeType.Equals(typeof(ProducesResponseTypeAttribute)));

                    // Add to collection based on response codes
                    foreach (var customAttributeData in prodocuesTypeCollection) {
                        List<string> types = new List<string>();

                        var statusCode = customAttributeData.ConstructorArguments[0].Value;
                        var type = customAttributeData.NamedArguments[0].TypedValue.Value.ToString();
                        type = type.Replace("+", "_");
                        int responseCode = (int)statusCode;
                        if (responsetypes.ContainsKey(responseCode)) {
                            types = responsetypes[responseCode];
                            types.Add(type);
                        } else {
                            types.Add(type);
                            responsetypes.Add(responseCode, types);
                        }

                    }

                    // Read  ProducesResponseTypeAttributes action method level
                    prodocuesTypeCollection = context.MethodInfo.CustomAttributes.
                    Where(attribute => attribute.AttributeType.Equals(typeof(ProducesResponseTypeAttribute)));

                    // Add to collection based on response codes
                    foreach (var customAttributeData in prodocuesTypeCollection) {
                        List<string> types = new List<string>();

                        var statusCode = customAttributeData.ConstructorArguments[0].Value;
                        var type = customAttributeData.NamedArguments[0].TypedValue.Value.ToString();

                        type = type.Replace("+", "_");
                        // Ignore System.Object
                        if (type.Equals("System.Object")) {
                            continue;
                        }

                        int responseCode = (int)statusCode;
                        if (responsetypes.ContainsKey(responseCode)) {
                            types = responsetypes[responseCode];
                            types.Add(type);
                        } else {
                            types.Add(type);
                            responsetypes.Add(responseCode, types);
                        }
                    }

                    // Set One-of if count > 1
                    foreach (var responses in responsetypes) {
                        List<string> types = responses.Value;
                        List<OpenApiSchema> oneOfList = new List<OpenApiSchema>();

                        if (types.Count > 1) {
                            foreach (var type in types) {
                                oneOfList.Add(
                                    new OpenApiSchema {
                                        Reference = new OpenApiReference { Id = type, Type = ReferenceType.Schema }
                                    });
                            }
                            OpenApiResponse openApiResponse = operation.Responses[responses.Key.ToString()];
                            foreach (var item in openApiResponse.Content) {
                                item.Value.Schema.OneOf = oneOfList;
                                // reset properties for they are included in allOf, should be null but code does not handle it
                                item.Value.Schema.Properties = new Dictionary<string, OpenApiSchema>();
                                item.Value.Schema.Reference = null;
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                Logging.WriteLogs(Logging.LogLevel.Error, ex.Message);
            }
        }
        #endregion
    }
}


