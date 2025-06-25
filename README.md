# DotExpress

A minimal, Express.js-inspired web framework for .NET, using dynamic request/response objects and middleware.

## Features

- Express-style routing: `Get`, `Post`, `Put`, `Delete`, `Patch`
- Middleware support: `Use`
- Static file serving: `UseStatic`
- JSON body parsing: `UseJson`
- Dynamic `req` and `res` objects
- Response helpers: `json`, `send`, `status`, `setHeader`, `redirect`, `sendStatus`, `type`, `cookie`
- Query, route, header, and cookie parsing
- Dependency Injection with per-request scope

## Example

```csharp
var app = new DotExpress.Server.HttpServer("http://localhost:5000/");

app.Use((req, res, next) => {
    Console.WriteLine($"{req.method} {req.url}");
    next();
});

app.UseJson();
app.UseStatic("wwwroot");

app.Get("/hello", (req, res) => {
    res.send("Hello, world!");
});

app.Start();
Console.ReadLine();
```

## License

MIT