using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Dynamic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace DotExpress.Server
{
    public class HttpServer
    {
        private readonly HttpListener _listener;
        private readonly List<Route> _routes = new();
        private bool _useJson = false;

        private readonly List<Action<dynamic, dynamic, Action>> _middleware = new();
        public bool IsRunning => _listener.IsListening;

        public void Use(Action<dynamic, dynamic, Action> middleware)
        {
            _middleware.Add(middleware);
        }   

        private readonly IServiceProvider? _rootProvider;

        public HttpServer(string prefix, IServiceProvider? provider = null)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _rootProvider = provider;
        }

        public void UseJson()
        {
            _useJson = true;
        }

        public void Get(string path, Action<dynamic, dynamic> handler) => AddRoute("GET", path, handler);
        public void Post(string path, Action<dynamic, dynamic> handler) => AddRoute("POST", path, handler);
        public void Delete(string path, Action<dynamic, dynamic> handler) => AddRoute("DELETE", path, handler);
        public void Put(string path, Action<dynamic, dynamic> handler) => AddRoute("PUT", path, handler);
        public void Patch(string path, Action<dynamic, dynamic> handler) => AddRoute("PATCH", path, handler);

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


                    IServiceScope? scope = null;
                    if (_rootProvider != null)
                    {
                        var scopeFactory = _rootProvider.GetService(typeof(IServiceScopeFactory)) as IServiceScopeFactory;
                        scope = scopeFactory?.CreateScope();
                        req.services = scope?.ServiceProvider ?? _rootProvider;
                    }
                    else
                    {
                        req.services = null;
                    }


                    req.query = context.Request.QueryString.AllKeys
    .Where(k => k != null)
    .ToDictionary(k => k!, k => context.Request.QueryString[k]!);
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

                    res.send = (Action<object>)(obj =>
                    {
                        string output;
                        string contentType;
                        if (obj is string str)
                        {
                            output = str;
                            contentType = "text/plain";
                        }
                        else
                        {
                            output = JsonSerializer.Serialize(obj);
                            contentType = "application/json";
                        }
                        var buffer = Encoding.UTF8.GetBytes(output);
                        context.Response.ContentType = contentType;
                        context.Response.ContentLength64 = buffer.Length;
                        context.Response.StatusCode = 200;
                        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        context.Response.OutputStream.Close();
                    });

                    // res.sendStatus: sets status and sends empty response
                    res.sendStatus = (Action<int>)(code =>
                    {
                        context.Response.StatusCode = code;
                        context.Response.OutputStream.Close();
                    });

                    // res.type: sets Content-Type header
                    res.type = (Action<string>)(mime =>
                    {
                        context.Response.ContentType = mime;
                    });

                    res.status = (Func<int, dynamic>)(code =>
                    {
                        context.Response.StatusCode = code;
                        return res;
                    });

                    res.setHeader = (Action<string, string>)((name, value) =>
                    {
                        context.Response.Headers[name] = value;
                    });

                    res.redirect = (Action<string>)(url =>
                    {
                        context.Response.Redirect(url);
                        context.Response.OutputStream.Close();
                    });

                    // req.headers: expose request headers as a dictionary
                    req.headers = context.Request.Headers.AllKeys
    .Where(k => k != null)
    .ToDictionary(k => k!, k => context.Request.Headers[k]!);

                    // req.cookies: parse cookies
                    req.cookies = context.Request.Cookies.Select(c => c.Name)
    .Where(k => k != null)
    .ToDictionary(k => k!, k => context.Request.Cookies[k]!);

                    // res.cookie: set a cookie
                    res.cookie = (Action<string, string>)((name, value) =>
                    {
                        context.Response.Cookies.Add(new Cookie(name, value));
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

                    // Check for static file
                    if (_staticDir != null)
                    {
                        var filePath = Path.Combine(_staticDir, context.Request.Url.AbsolutePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(filePath))
                        {
                            var bytes = await File.ReadAllBytesAsync(filePath);
                            context.Response.ContentType = GetMimeType(filePath);
                            context.Response.ContentLength64 = bytes.Length;
                            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                            context.Response.OutputStream.Close();
                            return;
                        }
                    }

                    // Compose middleware and route handler into a pipeline
                    int index = -1;
                    Action next = null;
                    next = () =>
                    {
                        index++;
                        if (index < _middleware.Count)
                        {
                            _middleware[index](req, res, next);
                        }
                        else
                        {
                            route.Handler(req, res);
                        }
                    };
                    next();
                    
                    scope?.Dispose();
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

        private string? _staticDir = null;

        public void UseStatic(string directory)
        {
            _staticDir = directory;
        }

        private static string GetMimeType(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".html" => "text/html",
                ".htm" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
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