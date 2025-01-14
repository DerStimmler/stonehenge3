﻿using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace IctBaden.Stonehenge3.Kestrel.Middleware
{
    public class ServerExceptionLogger
    {
        private readonly RequestDelegate _next;

        // ReSharper disable once UnusedMember.Global
        public ServerExceptionLogger(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next.Invoke(context);
            }
            catch (TaskCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                var message = ex.Message;
                if (ex.InnerException != null) message += "; " + ex.InnerException.Message;
                Trace.TraceError($"ServerExceptionHandler: {ex.GetType().Name}(HR=0x{ex.HResult:X8}): {message}" + Environment.NewLine + 
                                 $"ServerExceptionHandler: StackTrace: {ex.StackTrace}");
                return;
            }
            if (context.Response.StatusCode == (int) HttpStatusCode.InternalServerError)
            {
                using (var reader = new StreamReader(context.Response.Body))
                {
                    var message = reader.ReadToEnd();
                    Trace.TraceError($"ServerExceptionHandler: Message: {message}");
                }
            }
        }
    }
}