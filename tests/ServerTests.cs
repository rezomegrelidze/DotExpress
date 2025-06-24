using DotExpress.Server;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace DotExpress.Tests
{

    public class ServerTests
    {
        private readonly HttpServer _server;
        private const string Prefix = "http://localhost:8080/";

        public ServerTests()
        {
            _server = new HttpServer(Prefix);
        }

        [Fact]
        public void Server_Should_Start_And_Listen_For_Requests()
        {
            _server.Start();
            Assert.True(_server.IsRunning);
            _server.Stop();
        }

        [Fact]
        public void Server_Should_Stop_And_Not_Listen_For_Requests()
        {
            _server.Start();
            _server.Stop();
            Assert.False(_server.IsRunning);
        }

        [Fact]
        public async Task Server_Should_Handle_Get_Request()
        {
            _server.On("GET", "/test", async ctx =>
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                ctx.Response.Close();
                await Task.CompletedTask;
            });

            _server.Start();

            using var client = new HttpClient();
            var response = await client.GetAsync(Prefix + "test");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            _server.Stop();
        }

        [Fact]
        public async Task Server_Should_Return_NotFound_For_Invalid_Route()
        {
            _server.Start();

            using var client = new HttpClient();
            var response = await client.GetAsync(Prefix + "invalid");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            _server.Stop();
        }
    }
}