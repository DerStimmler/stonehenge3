﻿using System;
using System.Diagnostics;
using System.Threading;
using IctBaden.Stonehenge3.Hosting;
using IctBaden.Stonehenge3.Kestrel;
using IctBaden.Stonehenge3.Resources;
using IctBaden.Stonehenge3.SimpleHttp;

namespace IctBaden.Stonehenge3.Aurelia.SampleCore
{
    internal static class Program
    {
        private static IStonehengeHost _server;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            Console.WriteLine(@"");
            Console.WriteLine(@"Stonehenge 3 sample");
            Console.WriteLine(@"");

            // Select client framework
            Console.WriteLine(@"Using client framework aurelia");
            var loader = StonehengeResourceLoader.CreateDefaultLoader(new AureliaResourceProvider());
            var options = new StonehengeHostOptions
            {
                Title = "Sample",
                StartPage = "start",
                SessionIdMode = SessionIdModes.CookiesOnly
            };

            // Select hosting technology
            var hosting = "kestrel";
            if (Environment.CommandLine.Contains("/Simple")) { hosting = "simple"; }

            switch (hosting)
            {
                case "kestrel":
                    Console.WriteLine(@"Using Kestrel hosting");
                    _server = new KestrelHost(loader, options);
                    break;
                case "simple":
                    Console.WriteLine(@"Using simple http hosting");
                    _server = new SimpleHttpHost(loader, options);
                    break;
            }

            Console.WriteLine(@"Starting server");
            var terminate = new AutoResetEvent(false);
            Console.CancelKeyPress += (sender, eventArgs) => { terminate.Set(); };

            var host = Environment.CommandLine.Contains("/localhost") ? "localhost" : "*";
            if (_server.Start(host, 32000))
            {
                Console.WriteLine(@"Started server on: " + _server.BaseUrl);

                var wnd = new HostWindow(_server);
                if (!wnd.Open())
                {
                    Trace.TraceError("Failed to open main window.");
                    terminate.WaitOne();                    
                }
                
                Console.WriteLine(@"Server terminated.");
            }
            else
            {
                Console.WriteLine(@"Failed to start server on: " + _server.BaseUrl);
            }

#pragma warning disable 0162
            // ReSharper disable once HeuristicUnreachableCode
            _server.Terminate();
            
            Environment.Exit(0);
        }
    }
}
