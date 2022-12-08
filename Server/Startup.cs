using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Buffers;
using System.Threading.Tasks;

namespace Server
{
    public class Startup
    {
        private bool EnableHack = false;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddControllers();
            services.AddHttpContextAccessor();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapGet("/blobAsync", async context =>
                {
                    // increase StreamPipeWriter buffer using reflection
                    if (EnableHack)
                    {
                        HackPipeWriterBufferSize(context);
                    }

                    await WriteAsync(context);
                });
                endpoints.MapGet("/blobSync", context =>
                {
                    // increase StreamPipeWriter buffer using reflection
                    if (EnableHack)
                    {
                        HackPipeWriterBufferSize(context);
                    }

                    return Write(context);
                });
            });
        }

        private async Task WriteAsync(HttpContext context)
        {
            int writeSize = ParseBufferSize(context.Request.Query.TryGetValue("writeSize", out var qv) ? qv[0] : "65536");
            byte[] buffer = new byte[writeSize];
            var rnd = new Random();
            rnd.NextBytes(buffer);
            var rom = new ReadOnlyMemory<byte>(buffer);
            await context.Response.BodyWriter.WriteAsync(rom);
            //return Task.CompletedTask;
        }

        private Task Write(HttpContext context)
        {
            int writeSize = ParseBufferSize(context.Request.Query.TryGetValue("writeSize", out var qv) ? qv[0] : "65536");
            byte[] buffer = new byte[writeSize];
            var rnd = new Random();
            rnd.NextBytes(buffer);
            var rom = new ReadOnlySpan<byte>(buffer);
            context.Response.BodyWriter.Write(rom);
            return Task.CompletedTask;
        }

        private static void HackPipeWriterBufferSize(HttpContext context)
        {
            var bindingflags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = context.Response.BodyWriter.GetType();
            var field = type.GetField("_minimumBufferSize", bindingflags);
            field.SetValue(context.Response.BodyWriter, 65536);
        }

        public static int ParseBufferSize(string s)
        {
            var factor = 1;
            if (s.EndsWith("k", StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring(0, s.Length - 1);
                factor = 1024;
            }
            else if (s.EndsWith("M", StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring(0, s.Length - 1);
                factor = 1024 * 1024;
            }
            return Int32.Parse(s) * factor;
        }
    }
}
