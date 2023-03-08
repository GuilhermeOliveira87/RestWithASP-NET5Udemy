using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using Unisys.Common.Shared;
using static Unisys.Common.Shared.Logging;

namespace RestWithASPNETUdemy {
    public class Startup {
        public Startup(IConfiguration configuration, IWebHostEnvironment env) {
            Configuration = configuration;
            Environment = env;

            // Initialize logs
            InitializeLogs();
        }

        public IWebHostEnvironment Environment { get; }

        public IConfiguration Configuration { get; }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        public void ConfigureServices(IServiceCollection services) {
            try {
                // Read session timeout from appsettings.json (default 600 seconds)
                int timeoutSeconds = int.Parse(Configuration.GetValue(typeof(string), "SessionTimeoutSeconds", "600").ToString());

                services.Configure<CookiePolicyOptions>(options => {
                    options.CheckConsentNeeded = context => false;
                    options.MinimumSameSitePolicy = SameSiteMode.None;
                });

                services.AddDistributedMemoryCache();
                services.AddMvc().AddNewtonsoftJson();

                services.AddSession(options => {
                    options.IdleTimeout = TimeSpan.FromMinutes(timeoutSeconds);
                    options.Cookie.IsEssential = true;
                    options.Cookie.HttpOnly = true;
                });

                services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
                services.AddMvc(options => {
                    options.EnableEndpointRouting = false;
                    options.ModelMetadataDetailsProviders.Add(new UnisysAdditionalMetadataProvider());
                }

              );
                services.AddMvc(options => {
                    var customXmlSerializerInputFormatter = new CustomInputFormatter(options);
                    customXmlSerializerInputFormatter.SupportedEncodings.Clear();
                    customXmlSerializerInputFormatter.SupportedEncodings.Add(Encoding.GetEncoding("ISO-8859-1"));
                    options.InputFormatters.Add(customXmlSerializerInputFormatter);
                    options.OutputFormatters.Add(new XmlDataContractSerializerOutputFormatter());
                });

                // If using Kestrel:
                services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options => {
                    options.AllowSynchronousIO = true;
                });

                // If using IIS:
                services.Configure<IISServerOptions>(options => {
                    options.AllowSynchronousIO = true;
                });

                Unisys.Session.TIMEOUT = timeoutSeconds;

                services.AddSwaggerGenNewtonsoftSupport();

                // Register the Swagger generator, defining 1 or more Swagger documents
                services.AddSwaggerGen(c => {
                    c.SwaggerDoc("v1", new OpenApiInfo {
                        Description = "Controller Description",
                        Contact = new OpenApiContact {
                            Email = "Admin@MyDomain.com",
                            Name = "Administrator",
                            Url = new Uri("https://MyDomain.com")
                        },
                        License = new OpenApiLicense {
                            Name = "MyLicense",
                            Url = new Uri("https://MyDomain.com/license"),
                        },
                        Title = "MyAPI",
                        Version = "v1"
                    });
                    c.CustomSchemaIds(i => i.FullName.Replace('+', '_'));

                    // Register Document Filter for customizing and displaying required ePortal schemas
                    c.DocumentFilter<UnisysSwaggerDocumentFilter<Unisys.Message>>(OpenAPISpecification_SerializeAsV2);

                    // Register Operation Filter for customizing FromQuery, FromRoute, FromForm parameters
                    c.OperationFilter<UnisysSwaggerOperationFilter>(OpenAPISpecification_SerializeAsV2);

                    // Register Operation Filter for customizing FromBody, FromForm
                    c.SchemaFilter<UnisysSwaggerSchemaFilter>(OpenAPISpecification_SerializeAsV2);

                    c.EnableAnnotations();

                    //Add XML Documentation files
                    AddXMLDocumentationFiles(c);
                });

                services.AddMvc().AddJsonOptions(jsonOptions => {
                    jsonOptions.JsonSerializerOptions.PropertyNamingPolicy = null;
                    jsonOptions.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(null, true));
                });

            } catch (Exception ex) {
                Logging.WriteLogs(Logging.LogLevel.Error, ex.Message);
            }
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
            try {
                if (env.IsDevelopment()) {
                    app.UseDeveloperExceptionPage();
                }

                app.UseCookiePolicy(new CookiePolicyOptions());
                app.UseSession();
                var basePath = "/";

                //Get the deployed application name from the ApplicationInfo.xml file 
                if (File.Exists("ApplicationInfo.xml")) {
                    XmlDocument appInfoDom = new XmlDocument();
                    appInfoDom.Load("ApplicationInfo.xml");

                    string deployedApplicationName = appInfoDom.SelectSingleNode("ApplicationInfo/Platform/DeployAs").InnerText;

                    // Set the basePath to the deployed application name 
                    basePath = "/" + deployedApplicationName;
                }
                // Enable middleware to serve generated Swagger as a JSON endpoint.
                app.UseSwagger(c => {
                    c.SerializeAsV2 = OpenAPISpecification_SerializeAsV2;
                    c.PreSerializeFilters.Add((swaggerDoc, httpReq) => {
                        swaggerDoc.Servers = new List<OpenApiServer> { new OpenApiServer { Url = $"{httpReq.Scheme}://{httpReq.Host.Value}{basePath}" } };

                    });
                });
                // Enable middleware to serve generated Swagger as a JSON endpoint.       
                app.UseSwaggerUI(c => {
                    string swaggerJsonBasePath = string.IsNullOrWhiteSpace(c.RoutePrefix) ? "." : "..";
                    c.SwaggerEndpoint($"{swaggerJsonBasePath}/swagger/v1/swagger.json", "My API V1");
                });

                app.UseMvc();
            } catch (Exception ex) {
                Logging.WriteLogs(Logging.LogLevel.Error, ex.Message);
            }

        }

        /// <summary>
        /// To generate Open API Specification 2.0 version, set OpenAPISpecification_SerializeAsV2 to true
        /// </summary>
        private bool OpenAPISpecification_SerializeAsV2 = false;

        /// <summary>
        /// AddXMLDocumentationFiles
        /// </summary>
        /// <param name="c"></param>
        private static void AddXMLDocumentationFiles(SwaggerGenOptions c) {
            try {
                var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath, true);

                // Get the DataSourceProject name from the ApplicationInfo.xml file 
                // And Add Data Source project XML documentation files
                if (File.Exists("ApplicationInfo.json")) {
                    var appInfoContent = File.ReadAllText("ApplicationInfo.json");
                    var jobject = JsonConvert.DeserializeObject<JObject>(appInfoContent);
                    string dataSourceProject = jobject["BuildOptions"][0]["ApplicationName"].ToString();
                    try {
                        Assembly dsAssembly = Assembly.LoadFile(Path.Combine(AppContext.BaseDirectory, dataSourceProject + ".dll"));
                        Stream xmlDocuemntStream = dsAssembly.GetManifestResourceStream(dataSourceProject + "_Doc.XML");
                        if (xmlDocuemntStream != null) {
                            Func<XPathDocument> xPathDoc = () => new XPathDocument(xmlDocuemntStream);
                            c.IncludeXmlComments(xPathDoc, true);
                        }
                    } catch (Exception ex) {
                        Logging.WriteLogs(Logging.LogLevel.Error, ex.Message);
                    }
                }
            } catch (Exception ex) {
                Logging.WriteLogs(Logging.LogLevel.Error, ex.Message);
            }
        }

        /// <summary>
        /// This method Initialize logs
        /// </summary>
        private static void InitializeLogs() {
            string logSourceName, logPath;
            int logLevel;

            if (File.Exists("ApplicationInfo.xml")) {
                XmlDocument appInfoDom = new XmlDocument();
                appInfoDom.Load("ApplicationInfo.xml");

                // Gather build options
                XmlNode logLevelNode = appInfoDom.SelectSingleNode("//BuildOptions/LogLevel");
                logLevel = logLevelNode?.FirstChild?.Value != null ? int.Parse(logLevelNode.FirstChild.Value) : (int)LogLevel.Verbose;

                XmlNode logSourceNode = appInfoDom.SelectSingleNode("//BuildOptions/LogSourceName");
                logSourceName = logSourceNode?.FirstChild?.Value ?? "RestWithASPNETUdemy";

                XmlNode logPathNode = appInfoDom.SelectSingleNode("//BuildOptions/LogPath");
                logPath = logPathNode?.FirstChild != null ? logPathNode.FirstChild.Value : string.Empty;
            } else {
                logSourceName = "RestWithASPNETUdemy";
                logLevel = (int)LogLevel.Verbose;
                logPath = null;
            }

            Logging.InitializeLogs(logSourceName, logLevel, logPath, new Logging.LogDelegate(LogWriter));
        }

        /// <summary>
        /// Implements the LogDelegate and is used to write log messages from log
        /// to the studio trace file.
        /// </summary>
        /// <param name="level">Log level</param>
        /// <param name="message">Message</param>
        /// <param name="args">args</param>
        private static void LogWriter(LogLevel level, string message, params object[] args) {
            string logMessage = string.Format(string.Concat(DateTime.Now, ":", message), args) + System.Environment.NewLine;
        }
    }
    public class CustomInputFormatter : XmlSerializerInputFormatter {
        private readonly MvcOptions _options;
        //AllowSynchronousIO = true;
        public CustomInputFormatter(MvcOptions options) : base(options) {
            _options = options;
        }
        public override Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding encoding) {

            using (var reader = new StreamReader(context.HttpContext.Request.Body, encoding)) {
                var serializer = GetCachedSerializer(context.ModelType);
                var result = serializer.Deserialize(reader);
                return InputFormatterResult.SuccessAsync(result);
            }
        }
    }
}