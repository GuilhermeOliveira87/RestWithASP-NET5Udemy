using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Linq;
using Unisys.Common.Shared;

namespace RestWithASPNETUdemy {
    /// <summary>
    /// A Swagger Document Filter.
    /// If the type T, (normally Unisys.Message) is in the Schema Registry,
    /// then add all client messages, so they can be refereneced by the client.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class UnisysSwaggerDocumentFilter<T> : IDocumentFilter {

        // Swagger open api version
        bool openAPIVersion_SerializeAsV2 = false;

        public UnisysSwaggerDocumentFilter(bool openAPIVersion_SerializeAsV2) {
            this.openAPIVersion_SerializeAsV2 = openAPIVersion_SerializeAsV2;
        }

        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context) {
            try {
                // check if type T, Unisys.Message, is referenced
                if (!context.SchemaRepository.Schemas.ContainsKey(typeof(T).FullName))
                    return;

                //register all client subclasses
                Type messageType = typeof(Unisys.Message);
                var derivedTypes = typeof(object).Assembly
                                               .GetTypes()
                                               .Where(x => messageType != x && messageType.IsAssignableFrom(x));

                foreach (var item in derivedTypes) {
                    if (item.FullName.Contains(".Client."))
                        context.SchemaGenerator.GenerateSchema(item, context.SchemaRepository);
                }

                #region Set Discriminator

                var schemaRepository = context.SchemaRepository.Schemas;
                var schemaGenerator = context.SchemaGenerator;

                if (!schemaRepository.TryGetValue(typeof(T).FullName, out OpenApiSchema parentSchema)) {
                    parentSchema = schemaGenerator.GenerateSchema(typeof(T), context.SchemaRepository);
                }

                if (!openAPIVersion_SerializeAsV2) {

                    const string discriminatorName = "MessageType";
                    // set up a discriminator property (it must be required)
                    parentSchema.Discriminator = new OpenApiDiscriminator { PropertyName = discriminatorName };
                } else {
                    // For Backward compatibility remove message message type from schema
                    if (parentSchema.Properties.ContainsKey("MessageType")) {
                        parentSchema.Properties.Remove("MessageType");
                    }
                }

                #endregion Set Discriminator
            } catch (Exception ex) {
                Logging.WriteLogs(Logging.LogLevel.Error, ex.Message);
            }

        }
    }
}
