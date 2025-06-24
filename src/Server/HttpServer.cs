using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Dynamic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO;

namespace DotExpress.Server
{
    public class HttpServer
    {
        private readonly HttpListener _listener;
        private readonly List<Route> _routes = new();
        private bool _useJson = false;

        public bool IsRunning => _listener.IsListening;

        public HttpServer(string prefix)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
        }

        public void UseJson()
        {
            _useJson = true;
        }

        public void Get(string path, Action<dynamic, dynamic> handler) => AddRoute("GET", path, handler);
        public void Post(string path, Action<dynamic, dynamic> handler) => AddRoute("POST", path, handler);
        public void Delete(string path, Action<dynamic, dynamic> handler) => AddRoute("DELETE", path, handler);

        private void AddRoute(string method, string path, Action<dynamic, dynamic> handler)
        {
            _routes.Add(new Route(method.ToUpperInvariant(), path, handler));
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
                    break;
                }
            }
        }

        private async Task ProcessRequest(HttpListenerContext context)
        {
            var reqPath = context.Request.Url.AbsolutePath;
            var reqMethod = context.Request.HttpMethod.ToUpperInvariant();

            foreach (var route in _routes)
            {
                var match = route.Match(reqMethod, reqPath);
                if (match != null)
                {
                    dynamic req = new ExpandoObject();
                    dynamic res = new ExpandoObject();

                    req.query = context.Request.QueryString;
                    req.parameters = match;
                    req.body = null;

                    res.json = (Action<object>)(obj =>
                    {
                        var json = JsonSerializer.Serialize(obj);
                        var buffer = Encoding.UTF8.GetBytes(json);
                        context.Response.ContentType = "application/json";
                        context.Response.ContentLength64 = buffer.Length;
                        context.Response.StatusCode = 200;
                        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        context.Response.OutputStream.Close();
                    });

                    res.sendStatus = (Action<int>)(code =>
                    {
                        context.Response.StatusCode = code;
                        context.Response.OutputStream.Close();
                    });

                    // Parse JSON body if needed
                    if (_useJson && context.Request.HasEntityBody)
                    {
                        using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                        var bodyStr = await reader.ReadToEndAsync();
                        try
                        {
                            req.body = JsonSerializer.Deserialize<ExpandoObject>(bodyStr);
                        }
                        catch
                        {
                            req.body = null;
                        }
                    }

                    route.Handler(req, res);
                    return;
                }
            }

            // Not found
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            byte[] buffer = Encoding.UTF8.GetBytes("404 Not Found");
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }

        private class Route
        {
            public string Method { get; }
            public string Path { get; }
            public Action<dynamic, dynamic> Handler { get; }
            private readonly Regex _regex;
            private readonly List<string> _paramNames;

            public Route(string method, string path, Action<dynamic, dynamic> handler)
            {
                Method = method;
                Path = path;
                Handler = handler;
                (_regex, _paramNames) = BuildRegex(path);
            }

            public IDictionary<string, string>? Match(string method, string path)
            {
                if (!string.Equals(method, Method, StringComparison.OrdinalIgnoreCase))
                    return null;

                var match = _regex.Match(path);
                if (!match.Success) return null;

                var dict = new Dictionary<string, string>();
                for (int i = 0; i < _paramNames.Count; i++)
                {
                    dict[_paramNames[i]] = match.Groups[i + 1].Value;
                }
                return dict;
            }

            private static (Regex, List<string>) BuildRegex(string path)
            {
                var paramNames = new List<string>();
                var pattern = Regex.Replace(path, @":(\w+)", m =>
                {
                    paramNames.Add(m.Groups[1].Value);
                    return "([^/]+)";
                });
                pattern = "^" + pattern + "$";
                return (new Regex(pattern, RegexOptions.Compiled), paramNames);
            }
        }
    }
}