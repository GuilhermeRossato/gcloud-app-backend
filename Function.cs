using Google.Cloud.Functions.Framework;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace HelloHttp;


public class Function : IHttpFunction
{
  private readonly ILogger _logger;
  private readonly StorageClient _storageClient;
  private const string BucketName = "received-data";
  private const string IndexHtmlPath = "index.html";

  private static string _cachedHtmlContent;
  private static DateTime _lastModifiedTime = DateTime.MinValue;
  private static readonly object _cacheLock = new object();

  public Function(ILogger<Function> logger)
  {
    _logger = logger;
    _storageClient = StorageClient.Create();
  }

    public async Task HandleAsync(HttpContext context)
    {
      HttpRequest request = context.Request;

      // Check if this is a POST request with raw binary data
      if (request.Method == "POST" && request.ContentType != null &&
          request.ContentType.Contains("application/octet-stream"))
      {
        await HandleBinaryUploadAsync(context);
        return;
      }

      // For GET requests, serve the index.html file
      if (request.Method == "GET")
      {
        await ServeIndexHtmlAsync(context);
        return;
      }

      // Check URL parameters for "name" field
      // "world" is the default value
      string name = ((string) request.Query["name"]) ?? "world";

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

    private async Task ServeIndexHtmlAsync(HttpContext context)
    {
      try
      {
        string htmlContent = await GetCachedHtmlContentAsync();

        context.Response.ContentType = "text/html";
        context.Response.StatusCode = 200;
        await context.Response.WriteAsync(htmlContent);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error serving index.html");
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync("Error loading page");
      }
    }

    private async Task<string> GetCachedHtmlContentAsync()
    {
      if (!File.Exists(IndexHtmlPath))
      {
        throw new FileNotFoundException($"File not found: {IndexHtmlPath}");
      }

      var currentModifiedTime = File.GetLastWriteTimeUtc(IndexHtmlPath);

      lock (_cacheLock)
      {
        // Check if we need to reload the file
        if (_cachedHtmlContent == null || currentModifiedTime > _lastModifiedTime)
        {
          _logger.LogInformation($"Loading index.html (Last modified: {currentModifiedTime})");
          _cachedHtmlContent = File.ReadAllText(IndexHtmlPath);
          _lastModifiedTime = currentModifiedTime;
        }
        else
        {
          _logger.LogInformation("Returning cached index.html content");
        }

        return _cachedHtmlContent;
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
