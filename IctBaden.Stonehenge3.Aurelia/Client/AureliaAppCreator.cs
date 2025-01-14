﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using IctBaden.Stonehenge3.Core;
using IctBaden.Stonehenge3.Hosting;
using IctBaden.Stonehenge3.Resources;
using IctBaden.Stonehenge3.ViewModel;

namespace IctBaden.Stonehenge3.Aurelia.Client
{
    internal class AureliaAppCreator
    {
        private readonly string _appTitle;
        private readonly string _rootPage;
        private readonly StonehengeHostOptions _options;
        private readonly Dictionary<string, Resource> _aureliaContent;

        private static readonly string ControllerTemplate = LoadResourceText("IctBaden.Stonehenge3.Aurelia.Client.stonehengeController.js");
        private static readonly string ElementTemplate = LoadResourceText("IctBaden.Stonehenge3.Aurelia.Client.stonehengeElement.js");

        public AureliaAppCreator(string appTitle, string rootPage, StonehengeHostOptions options, Dictionary<string, Resource> aureliaContent)
        {
            _appTitle = appTitle;
            _rootPage = rootPage;
            _options = options;
            _aureliaContent = aureliaContent;
        }

        private static string LoadResourceText(string resourceName)
        {
            return LoadResourceText(Assembly.GetAssembly(typeof(AureliaAppCreator)), resourceName);
        }

        private static string LoadResourceText(Assembly assembly, string resourceName)
        {
            var resourceText = string.Empty;

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
              if (stream == null) return resourceText;
              using (var reader = new StreamReader(stream))
              {
                resourceText = reader.ReadToEnd();
              }
            }

            return resourceText;
        }

        public void CreateApplication()
        {
            var applicationJs = LoadResourceText("IctBaden.Stonehenge3.Aurelia.Client.stonehengeApp.js");
            applicationJs = InsertRoutes(applicationJs);

            var resource = new Resource("src.app.js", "AureliaResourceProvider", ResourceType.Html, applicationJs, Resource.Cache.Revalidate);
            _aureliaContent.Add("src.app.js", resource);
        }

        private string InsertRoutes(string pageText)
        {
            const string routesInsertPoint = "//stonehengeAppRoutes";
            const string stonehengeAppTitleInsertPoint = "stonehengeAppTitle";
            const string pageTemplate = "{{ route: [{0}'{1}'], name: '{1}', moduleId: './{2}', title:'{3}', nav: {4} }}";
            
            var pages = _aureliaContent
                .Select(res => new {  res.Value.Name, Vm = res.Value.ViewModel })
                .OrderBy(route => route.Vm.SortIndex)
                .Select(route => string.Format(pageTemplate,
                                            route.Name == _rootPage ? "''," : "",
                                            route.Name,
                                            route.Name,
                                            route.Vm.Title,
                                            route.Vm.Visible ? "true" : "false" ));

            var routes = string.Join("," + Environment.NewLine, pages);
            pageText = pageText
                .Replace(routesInsertPoint, routes)
                .Replace(stonehengeAppTitleInsertPoint, _appTitle);

            return pageText;
        }


        public void CreateControllers()
        {
            var viewModels = _aureliaContent
                .Where(res => res.Value.ViewModel?.VmName != null)
                .Select(res => res.Value)
                .Distinct()
                .ToList();

            foreach (var viewModel in viewModels)
            {
                var controllerJs = GetController(viewModel.ViewModel.VmName);
                if (!string.IsNullOrEmpty(controllerJs))
                {
                    if (string.IsNullOrEmpty(viewModel.ViewModel.VmName))
                    {
                        Trace.TraceError($"AureliaAppCreator.CreateControllers: <UNKNOWN VM> => src.{viewModel.Name}.js");
                    }
                    else
                    {
                        Trace.TraceInformation($"AureliaAppCreator.CreateControllers: {viewModel.ViewModel.VmName} => src.{viewModel.Name}.js");

                        var assembly = Assembly.GetEntryAssembly();
                        var userJs = LoadResourceText(assembly, $"{assembly.GetName().Name}.app.{viewModel.Name}_user.js");
                        if (!string.IsNullOrWhiteSpace(userJs))
                        {
                            controllerJs += userJs;
                        }

                        var resource = new Resource($"src.{viewModel.Name}.js", "AureliaResourceProvider", ResourceType.Js, controllerJs, Resource.Cache.Revalidate);
                        _aureliaContent.Add(resource.Name, resource);
                    }
                }
            }
        }

        private static Type[] GetAssemblyTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (Exception)
            {
                return new Type[0];
            }
        }

        private static Type GetVmType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetAssemblyTypes)
                .FirstOrDefault(type => type.Name == name);
        }

        private string GetController(string vmName)
        {
            var vmType = GetVmType(vmName);
            if (vmType == null)
            {
                Trace.TraceError($"No VM for type {vmName} defined.");
                Debug.Assert(false, $"No VM for type {vmName} defined.");
                // ReSharper disable once HeuristicUnreachableCode
                return null;
            }

            var text = ControllerTemplate
                .Replace("stonehengeViewModelName", vmName)
                .Replace("stonehengePollDelay", _options.GetPollDelayMs().ToString());

            var postBackPropNames = GetPostBackPropNames(vmType).Select(name => "'" + name + "'");
            text = text.Replace("'propNames'", string.Join(",", postBackPropNames));

            // supply functions for action methods
            const string methodTemplate = @"this.stonehengeMethodName = function({paramNames}) { this.StonehengePost(this, '/ViewModel/stonehengeViewModelName/stonehengeMethodName{paramValues}'); }";

            var actionMethods = new StringBuilder();
            foreach (var methodInfo in vmType.GetMethods().Where(methodInfo => methodInfo.GetCustomAttributes(false).OfType<ActionMethodAttribute>().Any()))
            {
                //var method = (methodInfo.GetParameters().Length > 0)
                //  ? "%method%: function (data, event, param) { if(!IsLoading()) post_ViewModelName_Data(self, event.currentTarget, '%method%', param); },".Replace("%method%", methodInfo.Name)
                //  : "%method%: function (data, event) { if(!IsLoading()) post_ViewModelName_Data(self, event.currentTarget, '%method%', null); },".Replace("%method%", methodInfo.Name);

                var paramNames = methodInfo.GetParameters().Select(p => p.Name).ToArray();
                var paramValues = paramNames.Any()
                ? "?" + string.Join("&", paramNames.Select(n => string.Format("{0}='+encodeURIComponent({0})+'", n)))
                : string.Empty;

                var method = methodTemplate
                    .Replace("stonehengeViewModelName", vmName)
                    .Replace("stonehengeMethodName", methodInfo.Name)
                    .Replace("stonehengePollDelay", _options.GetPollDelayMs().ToString())
                    .Replace("{paramNames}", string.Join(",", paramNames))
                    .Replace("{paramValues}", paramValues)
                    .Replace("+''", string.Empty);

                actionMethods.AppendLine(method);
            }


            return text.Replace("/*commands*/", actionMethods.ToString());
        }

        private static List<string> GetPostBackPropNames(Type vmType)
        {
            var postBackPropNames = new List<string>();

            // properties
            var vmProps = new List<PropertyDescriptor>();
            var sessionCtor = vmType.GetConstructors().FirstOrDefault(ctor => ctor.GetParameters().Length == 1);
            var session = new AppSession();
            object viewModel;
            try
            {
                viewModel = (sessionCtor != null) ? Activator.CreateInstance(vmType, session) : Activator.CreateInstance(vmType);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Failed to create ViewModel '{vmType.Name}' : " + ex.Message);
                return postBackPropNames;
            }
            var activeVm = viewModel as ActiveViewModel;
            if (activeVm != null)
            {
                vmProps.AddRange(from PropertyDescriptor prop in activeVm.GetProperties() select prop);
            }
            else
            {
                vmProps.AddRange(TypeDescriptor.GetProperties(viewModel, true).Cast<PropertyDescriptor>());
            }
            var disposeVm = viewModel as IDisposable;
            disposeVm?.Dispose();

            var assignPropNames = (from prop in vmProps
                                   let bindable = prop.Attributes.OfType<BindableAttribute>().ToArray()
                                   where (bindable.Length <= 0) || bindable[0].Bindable
                                   select prop.Name).ToList();

            // do not send ReadOnly or OneWay bound properties back
            foreach (var propName in assignPropNames)
            {
                var prop = vmType.GetProperty(propName);
                if (prop == null)
                {
                    if (activeVm == null)
                        continue;
                    prop = activeVm.GetPropertyInfo(propName);
                    if ((prop != null) && activeVm.IsPropertyReadOnly(propName))
                        continue;
                }

                if (prop?.GetSetMethod(false) == null) // not public writable
                    continue;
                var bindable = prop.GetCustomAttributes(typeof(BindableAttribute), true);
                if ((bindable.Length > 0) && ((BindableAttribute)bindable[0]).Direction == BindingDirection.OneWay)
                    continue;
                postBackPropNames.Add(propName);
            }

            return postBackPropNames;
        }

        public void CreateElements()
        {
            var customElements = _aureliaContent
               .Where(res => res.Value.ViewModel?.ElementName != null)
               .Select(res => res.Value)
               .Distinct()
               .ToList();

            foreach (var element in customElements)
            {
                var elementJs = ElementTemplate.Replace("stonehengeCustomElementClass", element.ViewModel.ElementName);
                elementJs = elementJs.Replace("stonehengeCustomElementName", element.Name.Replace("_", "-"));

                var bindings = element.ViewModel?.Bindings?.Select(b => $"@bindable('{b}')") ?? new List<string>() { string.Empty };
                elementJs = elementJs.Replace("//@bindable", string.Join(Environment.NewLine, bindings));

                var resource = new Resource($"src.{element.Name}.js", "AureliaResourceProvider", ResourceType.Js, elementJs, Resource.Cache.Revalidate);
                _aureliaContent.Add(resource.Name, resource);
            }
        }
    }
}