using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DotExpress.Server
{
    public class HttpServer
    {
        private readonly HttpListener _listener;
        private readonly Dictionary<(string method, string path), 
                                    Func<HttpListenerContext, Task>> _routes =
                                    [];

        public bool IsRunning => _listener.IsListening;

        public HttpServer(string prefix)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
        }

        public void Start()
        {
            _listener.Start();
            Console.WriteLine("Server started...");
            Task.Run(() => HandleRequests());
        }

        public void Stop()
        {
            _listener.Stop();
            Console.WriteLine("Server stopped.");
        }

        // Register a route handler
        public void On(string method, string path, Func<HttpListenerContext, Task> handler)
        {
            _routes[(method.ToUpperInvariant(), path)] = handler;
        }

        private async Task HandleRequests()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => ProcessRequest(context));
                }
                catch (HttpListenerException)
                {
                    // Listener stopped
                    break;
                }
            }
        }

        private async Task ProcessRequest(HttpListenerContext context)
        {
            var key = (context.Request.HttpMethod.ToUpperInvariant(), context.Request.Url.AbsolutePath);
            if (_routes.TryGetValue(key, out var handler))
            {
                await handler(context);
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                byte[] buffer = Encoding.UTF8.GetBytes("404 Not Found");
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
            }
        }
    }
}