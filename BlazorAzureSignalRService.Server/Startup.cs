using Microsoft.AspNetCore.Blazor.Builder;
using Microsoft.AspNetCore.Blazor.Hosting;
using Microsoft.AspNetCore.Blazor.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Azure.SignalR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Reflection;

namespace BlazorAzureSignalRService.Server
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            // Adds the Server-Side Blazor services, and those registered by the app project's startup.
            services.AddServerSideBlazor<App.Startup>();
            services.AddSignalR().AddAzureSignalR();

            services.AddResponseCompression(options =>
            {
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
                {
                    MediaTypeNames.Application.Octet,
                    WasmMediaTypeNames.Application.Wasm,
                });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseResponseCompression();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Use component registrations and static files from the app project.
            // app.UseServerSideBlazor<App.Startup>();

            var startupType = typeof(App.Startup);
            var startup = app.ApplicationServices.GetRequiredService(startupType);

            // var wrapper = new ConventionBasedStartup(startup);
            var blazorBrowserAssembly = Assembly.GetAssembly(typeof(BlazorWebAssemblyHost));
            var conventionBasedStartupType = blazorBrowserAssembly.GetType("Microsoft.AspNetCore.Blazor.Hosting.ConventionBasedStartup");
            var wrapper = Activator.CreateInstance(conventionBasedStartupType, startup);

            // Action<IBlazorApplicationBuilder> configure = (b) =>
            // {
            //     wrapper.Configure(b, b.Services);
            // };

            var configureMethod = conventionBasedStartupType.GetMethod("Configure");
            Action<IBlazorApplicationBuilder> configure = (b) =>
            {
                configureMethod.Invoke(wrapper, new object[] { b, b.Services });
            };

            // UseServerSideBlazorCore(builder, configure);
            var endpoint = "/_blazor";

            // var factory = (DefaultCircuitFactory)builder.ApplicationServices.GetRequiredService<CircuitFactory>();
            var blazorServerAssembly = Assembly.GetAssembly(typeof(BlazorOptions));
            var circuitFactoryType = blazorServerAssembly.GetType("Microsoft.AspNetCore.Blazor.Server.Circuits.CircuitFactory");
            var factory = app.ApplicationServices.GetRequiredService(circuitFactoryType);

            // factory.StartupActions.Add(endpoint, configure);
            var startupActionsProperty = factory.GetType().GetRuntimeProperty("StartupActions");
            var startupActions = (Dictionary<PathString, Action<IBlazorApplicationBuilder>>) startupActionsProperty.GetValue(factory);
            startupActions.Add(endpoint, configure);

            // builder.UseSignalR(route => route.MapHub<BlazorHub>(endpoint));
            var mapHubMethodInfo = typeof(ServiceRouteBuilder)
                .GetMethod("MapHub", new[] { typeof(string) })
                .MakeGenericMethod(blazorServerAssembly.GetType("Microsoft.AspNetCore.Blazor.Server.Circuits.BlazorHub"));

            app.UseAzureSignalR(route => mapHubMethodInfo.Invoke(route, new[] { endpoint }));

            app.UseBlazor(new BlazorOptions()
            {
                ClientAssemblyPath = startupType.Assembly.Location,
            });
        }
    }
}
