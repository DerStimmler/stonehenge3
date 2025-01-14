﻿using System.Collections.Generic;
using IctBaden.Stonehenge3.Core;
using IctBaden.Stonehenge3.Hosting;
using IctBaden.Stonehenge3.Kestrel.Middleware;
using IctBaden.Stonehenge3.Resources;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace IctBaden.Stonehenge3.Kestrel
{
    public class Startup
    {
        private readonly string _appTitle;
        private readonly IStonehengeResourceProvider _resourceLoader;
        private readonly List<AppSession> _appSessions = new List<AppSession>();
        private readonly StonehengeHostOptions _options;

        // ReSharper disable once UnusedMember.Global
        public Startup(IConfiguration configuration, IStonehengeResourceProvider resourceLoader)
        {
            Configuration = configuration;
            _resourceLoader = resourceLoader;
            _appTitle = Configuration["AppTitle"];
            _options = JsonConvert.DeserializeObject<StonehengeHostOptions>(Configuration["HostOptions"]);
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // ReSharper disable once UnusedMember.Global
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseMiddleware<ServerExceptionLogger>();
            //TODO app.UseMiddleware<StonehengeAcme>();
            //TODO app.UseCompression();
            app.Use((context, next) =>
            {
                context.Items.Add("stonehenge.AppTitle", _appTitle);
                context.Items.Add("stonehenge.HostOptions", _options);
                context.Items.Add("stonehenge.ResourceLoader", _resourceLoader);
                context.Items.Add("stonehenge.AppSessions", _appSessions);
                return next.Invoke();
            });
            app.UseMiddleware<StonehengeSession>();
            app.UseMiddleware<StonehengeHeaders>();
            app.UseMiddleware<StonehengeRoot>();
            app.UseMiddleware<StonehengeContent>();
        }
    }
}
