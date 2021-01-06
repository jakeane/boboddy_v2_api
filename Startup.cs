using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using boboddyv2_api.Models;

namespace boboddyv2_api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddControllers();
            services.AddMemoryCache();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "boboddyv2_api", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IMemoryCache cache)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "boboddyv2_api v1"));
            }
            else
            {
                app.UseHsts();
            }

            LoadCache(cache);

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private void LoadCache(IMemoryCache cache)
        {
            HashSet<string> DataSet = new HashSet<string> { "quotes", "shakespeare", "state_union", "inaugural", "bible", "all" };

            foreach (string data in DataSet)
            {
                Console.WriteLine($"Reading {data}");
                using (StreamReader r = new StreamReader($"./Data/{data}_data.json"))
                {
                    AcronymModel model = JsonConvert.DeserializeObject<AcronymModel>(r.ReadToEnd());
                    cache.Set(data, model);
                }
            }
        }
    }
}
