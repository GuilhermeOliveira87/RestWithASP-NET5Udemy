using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Timers;

namespace RestWithASPNETUdemy {
    public class Program {
        public static void Main(string[] args) {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .ConfigureAppConfiguration((hostingContext, config) => {
                    var env = hostingContext.HostingEnvironment;

                    // load up configuration files
                    string[] jsonFiles = Directory.GetFiles(env.ContentRootPath, "*.json");
                    foreach (string jFile in jsonFiles) {
                        string name = Path.GetFileNameWithoutExtension(jFile);
                        if (name.Contains('.'))
                            continue;

                        config.AddJsonFile(jFile, true, true);
                        config.AddJsonFile($"{name}.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
                    }

                    if (env.IsDevelopment()) // different providers in dev
                    {
                        var appAssembly = Assembly.Load(new AssemblyName(env.ApplicationName));
                        if (appAssembly != null) {
                            config.AddUserSecrets(appAssembly, optional: true);
                        }
                    }

                    config.AddEnvironmentVariables(); // overwrites previous values

                    if (args != null) {
                        config.AddCommandLine(args);
                    }
                });
    }
}