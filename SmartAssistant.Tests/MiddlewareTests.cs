using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using SmartAssistant.API.Middlewares;
using Xunit;

namespace SmartAssistant.Tests
{
    public class MiddlewareTests
    {
        [Fact]
        public async Task GlobalExceptionMiddleware_ShouldReturnInternalServerError_WhenExceptionThrown()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<GlobalExceptionMiddleware>>();
            var envMock = new Mock<IHostEnvironment>();
            envMock.Setup(e => e.EnvironmentName).Returns("Development");

            RequestDelegate next = (HttpContext context) => throw new Exception("Simüle edilen sistem hatasý");

            var middleware = new GlobalExceptionMiddleware(next, loggerMock.Object, envMock.Object);
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
            Assert.Equal("application/json", context.Response.ContentType);
        }
    }
}