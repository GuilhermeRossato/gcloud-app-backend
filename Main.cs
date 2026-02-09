using Google.Cloud.Functions.Framework;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace HelloHttp;

public class Main : IHttpFunction
{
  private readonly ILogger _logger;
  private readonly StorageClient _storageClient;
  private readonly int _port; // Local variable for the port
  private const string BucketName = "received-data";

  public Main(ILogger<Main> logger)
  {
    _logger = logger;
    _storageClient = StorageClient.Create();

    // Read PORT environment variable
    string portEnv = Environment.GetEnvironmentVariable("PORT");
    if (string.IsNullOrWhiteSpace(portEnv) || portEnv == "0" || !int.TryParse(portEnv, out _port))
    {
      _port = 8080; // Fallback to default port
    }
    _logger.LogInformation($"Server will listen on port {_port}");

    SetupServer();
  }

  private void SetupServer()
  {
    try
    {
      HttpListener listener = new HttpListener();
      // Additional setup logic here
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error setting up server");
    }
  }

  public async Task HandleAsync(HttpContext context)
  {
    // Handle HTTP requests here
  }
}