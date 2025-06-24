using System.Text;

var server = new DotExpress.Server.HttpServer("http://localhost:5000/");

server.On("GET", "/hello", async ctx =>
{
    var response = "<html><body>Hello, Express-style!</body></html>";
    var buffer = Encoding.UTF8.GetBytes(response);
    ctx.Response.ContentType = "text/html";
    ctx.Response.ContentLength64 = buffer.Length;
    await ctx.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
    ctx.Response.OutputStream.Close();
});

server.Start();