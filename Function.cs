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
      listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
      listener.Start();
      _logger.LogInformation($"Server started listening on http://127.0.0.1:{_port}/");
      Task.Run(async () =>
      {
        while (true)
        {
          try
          {
            var context = await listener.GetContextAsync();
            await HandleRequestAsync(context);
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Error handling request");
          }
        }
      });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, $"Failed to start server on http://127.0.0.1:{_port}/");
    }
  }

  private async Task HandleRequestAsync(HttpListenerContext context)
  {
    try
    {
      HttpContext httpContext = new DefaultHttpContext();
      httpContext.Request.Body = context.Request.InputStream;
      httpContext.Request.Method = context.Request.HttpMethod;
      httpContext.Response.Body = context.Response.OutputStream;

      await HandleAsync(httpContext);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing request");
      context.Response.StatusCode = 500;
      context.Response.ContentType = "text/plain";
      using var writer = new StreamWriter(context.Response.OutputStream);
      await writer.WriteAsync($"Error: {ex.Message}\n{ex.StackTrace}");
    }
  }

  public async Task HandleAsync(HttpContext context)
  {
    try
    {
      HttpRequest request = context.Request;

      // Check if this is a POST request with raw binary data
      if (request.Method == "POST" && request.ContentType != null &&
          request.ContentType.Contains("application/octet-stream"))
      {
        await HandleBinaryUploadAsync(context);
        return;
      }

      // Check URL parameters for "name" field
      // "world" is the default value
      string name = ((string)request.Query["name"]) ?? "world";

      // If there's a body, parse it as JSON and check for "name" field.
      using TextReader reader = new StreamReader(request.Body);
      string text = await reader.ReadToEndAsync();
      if (text.Length > 0)
      {
        try
        {
          JsonElement json = JsonSerializer.Deserialize<JsonElement>(text);
          if (json.TryGetProperty("name", out JsonElement nameElement) &&
            nameElement.ValueKind == JsonValueKind.String)
          {
            name = nameElement.GetString();
          }
        }
        catch (JsonException parseException)
        {
          _logger.LogError(parseException, "Error parsing JSON request");
        }
      }

      await context.Response.WriteAsync($"Hello {name}!");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error in HandleAsync");
      context.Response.StatusCode = 500;
      context.Response.ContentType = "text/plain";
      await context.Response.WriteAsync($"Error: {ex.Message}\n{ex.StackTrace}");
    }
  }

  private async Task HandleBinaryUploadAsync(HttpContext context)
  {
    byte[] buffer = null;
    try
    {
      // Read the binary data into a buffer
      using var memoryStream = new MemoryStream();
      await context.Request.Body.CopyToAsync(memoryStream);
      buffer = memoryStream.ToArray();

      if (buffer.Length == 0)
      {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("No data received");
        return;
      }

      // Generate filename with timestamp format: yyyy-mm-dd-hh-mm.i16
      var now = DateTime.UtcNow;
      string fileName = $"{now:yyyy-MM-dd-HH-mm}.i16";

      _logger.LogInformation($"Uploading {buffer.Length} bytes to {BucketName}/{fileName}");

      // Upload to Google Cloud Storage
      using var uploadStream = new MemoryStream(buffer);
      await _storageClient.UploadObjectAsync(
        BucketName,
        fileName,
        "application/octet-stream",
        uploadStream
      );

      _logger.LogInformation($"Successfully uploaded file: {fileName}");

      context.Response.StatusCode = 200;
      await context.Response.WriteAsync($"File uploaded successfully: {fileName}");

      // Clear buffer after successful upload
      buffer = null;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error uploading file to Cloud Storage");

      // Clear buffer on failure
      if (buffer != null)
      {
        Array.Clear(buffer, 0, buffer.Length);
        buffer = null;
      }

      context.Response.StatusCode = 500;
      await context.Response.WriteAsync("Error uploading file");
    }
  }
}
