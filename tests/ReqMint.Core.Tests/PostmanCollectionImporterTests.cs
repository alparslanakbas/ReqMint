using ReqMint.Core.Importing;
using ReqMint.Core.Requests;

namespace ReqMint.Core.Tests;

public sealed class PostmanCollectionImporterTests
{
    [Fact]
    public void Import_MapsFoldersRequestsBodiesAuthenticationAndFiles()
    {
        const string json = """
        {
          "info": {
            "name": "Commerce API",
            "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
          },
          "item": [
            {
              "name": "Create order",
              "request": {
                "method": "POST",
                "header": [
                  { "key": "Content-Type", "value": "application/json" },
                  { "key": "X-Client", "value": "ReqMint" }
                ],
                "auth": {
                  "type": "bearer",
                  "bearer": [{ "key": "token", "value": "{{TOKEN}}" }]
                },
                "body": {
                  "mode": "raw",
                  "raw": "{\"name\":\"Mint\"}",
                  "options": { "raw": { "language": "json" } }
                },
                "url": {
                  "raw": "{{BASE_URL}}/orders?include=items",
                  "query": [{ "key": "include", "value": "items" }]
                }
              }
            },
            {
              "name": "Media",
              "item": [
                {
                  "name": "Upload asset",
                  "request": {
                    "method": "POST",
                    "body": {
                      "mode": "formdata",
                      "formdata": [
                        { "key": "description", "value": "ReqMint", "type": "text" },
                        { "key": "file", "type": "file", "src": "C:\\private\\asset.png" }
                      ]
                    },
                    "url": "https://example.com/upload"
                  }
                }
              ]
            }
          ]
        }
        """;

        var result = new PostmanCollectionImporter().Import(json);

        Assert.Equal(2, result.Collections.Count);
        Assert.Equal(2, result.RequestCount);
        var create = Assert.Single(result.Collections[0].Requests);
        Assert.Equal("Commerce API", result.Collections[0].Name);
        Assert.Equal("{{BASE_URL}}/orders", create.Url);
        Assert.Equal(new RequestField("include", "items"), Assert.Single(create.QueryParameters));
        Assert.Equal(new RequestField("X-Client", "ReqMint"), Assert.Single(create.Headers));
        Assert.Equal("application/json", create.Body?.ContentType);
        Assert.Equal(RequestAuthenticationType.Bearer, create.Authentication?.Type);
        Assert.Equal("{{TOKEN}}", create.Authentication?.BearerToken);

        var upload = Assert.Single(result.Collections[1].Requests);
        Assert.Equal("Commerce API — Media", result.Collections[1].Name);
        Assert.Equal("multipart/form-data", upload.Body?.ContentType);
        Assert.Equal("description", Assert.Single(upload.Body!.FormFields).Name);
        var file = Assert.Single(upload.Body.FileFields);
        Assert.Equal("asset.png", file.FileName);
        Assert.Null(file.LocalPath);
        Assert.Contains(result.Warnings, warning =>
            warning.Kind == PostmanImportWarningKind.FileMustBeReselected);
    }

    [Fact]
    public void Import_OmitsLiteralSecretsAndReportsUnsupportedFeatures()
    {
        const string json = """
        {
          "info": {
            "name": "Unsafe",
            "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
          },
          "event": [{ "listen": "prerequest" }],
          "variable": [{ "key": "BASE_URL", "value": "https://example.com" }],
          "item": [{
            "name": "Secret request",
            "request": {
              "method": "GET",
              "header": [{ "key": "Authorization", "value": "Bearer literal-secret" }],
              "auth": {
                "type": "bearer",
                "bearer": [{ "key": "token", "value": "literal-secret" }]
              },
              "url": "https://example.com"
            }
          }]
        }
        """;

        var result = new PostmanCollectionImporter().Import(json);
        var request = Assert.Single(Assert.Single(result.Collections).Requests);

        Assert.Empty(request.Headers);
        Assert.Null(request.Authentication);
        Assert.Contains(result.Warnings, warning =>
            warning.Kind == PostmanImportWarningKind.SensitiveValueOmitted);
        Assert.Contains(result.Warnings, warning =>
            warning.Kind == PostmanImportWarningKind.ScriptOmitted);
        Assert.Contains(result.Warnings, warning =>
            warning.Kind == PostmanImportWarningKind.CollectionVariablesOmitted);
    }

    [Fact]
    public void Import_RejectsOtherJsonDocuments()
    {
        var exception = Assert.Throws<PostmanImportException>(() =>
            new PostmanCollectionImporter().Import("{\"info\":{\"name\":\"Not Postman\"}}"));

        Assert.Contains("v2.1", exception.Message, StringComparison.Ordinal);
    }
}
