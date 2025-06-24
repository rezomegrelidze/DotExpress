using System;
using System.Collections.Generic;
using System.Net;

namespace DotExpress.Routing;

public class Router
{
    private readonly Dictionary<string, Func<HttpListenerRequest, dynamic>> routes = new();

    public void AddRoute(string path, Func<HttpListenerRequest, dynamic> handler)
    {
        routes[path] = handler;
    }

    public dynamic MatchRoute(HttpListenerRequest request)
    {
        if (routes.TryGetValue(request.Url.AbsolutePath, out var handler))
        {
            return handler(request);
        }

        return null; // or handle not found
    }
}