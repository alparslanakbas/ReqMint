using ReqMint.Core.Importing;
using ReqMint.Core.Requests;

namespace ReqMint.Core.Tests;

public sealed class OpenApiDocumentImporterTests
{
    [Fact]
    public void Import_MapsJsonOperationsParametersBodiesAndSecurity()
    {
        const string json = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Pet API", "version": "1.0.0" },
          "servers": [{ "url": "https://api.example.com/{version}", "variables": { "version": { "default": "v1" } } }],
          "security": [{ "BearerAuth": [] }],
          "components": {
            "securitySchemes": {
              "BearerAuth": { "type": "http", "scheme": "bearer" }
            }
          },
          "paths": {
            "/pets/{id}": {
              "parameters": [{ "name": "id", "in": "path", "schema": { "type": "integer", "example": 42 } }],
              "get": {
                "summary": "Get pet",
                "tags": ["Pets"],
                "parameters": [
                  { "name": "limit", "in": "query", "schema": { "type": "integer", "default": 10 } },
                  { "name": "X-Trace", "in": "header", "example": "reqmint" }
                ],
                "responses": { "200": { "description": "OK" } }
              }
            },
            "/pets": {
              "post": {
                "summary": "Create pet",
                "tags": ["Pets"],
                "requestBody": {
                  "content": {
                    "application/json": {
                      "example": { "name": "Mint", "password": "literal-secret" }
                    }
                  }
                },
                "responses": { "201": { "description": "Created" } }
              }
            }
          }
        }
        """;

        var result = new OpenApiDocumentImporter().Import(json);

        var collection = Assert.Single(result.Collections);
        Assert.Equal("Pet API — Pets", collection.Name);
        Assert.Equal(2, collection.Requests.Count);
        var get = collection.Requests[0];
        Assert.Equal("https://api.example.com/v1/pets/42", get.Url);
        Assert.Equal(new RequestField("limit", "10"), Assert.Single(get.QueryParameters));
        Assert.Equal(new RequestField("X-Trace", "reqmint"), Assert.Single(get.Headers));
        Assert.Equal(RequestAuthenticationType.Bearer, get.Authentication?.Type);
        Assert.Equal("{{BEARERAUTH_TOKEN}}", get.Authentication?.BearerToken);

        var post = collection.Requests[1];
        Assert.Equal("application/json", post.Body?.ContentType);
        Assert.Contains("\"name\": \"Mint\"", post.Body?.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("literal-secret", post.Body?.Content, StringComparison.Ordinal);
        Assert.Contains("{{PASSWORD}}", post.Body?.Content, StringComparison.Ordinal);
        Assert.Contains(result.Warnings, warning =>
            warning.Kind == OpenApiImportWarningKind.AuthenticationEnvironmentRequired);
        Assert.Contains(result.Warnings, warning =>
            warning.Kind == OpenApiImportWarningKind.SensitiveExampleOmitted);
    }

    [Fact]
    public void Import_MapsYamlMultipartFilesAndRelativeServers()
    {
        const string yaml = """
        openapi: 3.0.3
        info:
          title: Upload API
          version: 1.0.0
        servers:
          - url: /v2
        paths:
          /assets:
            post:
              summary: Upload asset
              requestBody:
                content:
                  multipart/form-data:
                    schema:
                      type: object
                      properties:
                        description:
                          type: string
                          example: ReqMint
                        file:
                          type: string
                          format: binary
              responses:
                '200':
                  description: OK
        """;

        var result = new OpenApiDocumentImporter().Import(yaml);

        var request = Assert.Single(Assert.Single(result.Collections).Requests);
        Assert.Equal("https://api.example.com/v2/assets", request.Url);
        Assert.Equal("multipart/form-data", request.Body?.ContentType);
        Assert.Equal(new RequestField("description", "ReqMint"), Assert.Single(request.Body!.FormFields));
        var file = Assert.Single(request.Body.FileFields);
        Assert.Equal("file", file.Name);
        Assert.Null(file.LocalPath);
        Assert.Contains(result.Warnings, warning =>
            warning.Kind == OpenApiImportWarningKind.UploadFileMustBeSelected);
        Assert.Contains(result.Warnings, warning =>
            warning.Kind == OpenApiImportWarningKind.DefaultServerUsed);
    }

    [Fact]
    public void Import_RejectsNonOpenApiDocuments()
    {
        var exception = Assert.Throws<OpenApiImportException>(() =>
            new OpenApiDocumentImporter().Import("{\"name\":\"not-openapi\"}"));

        Assert.Contains("OpenAPI 3.0 or 3.1", exception.Message, StringComparison.Ordinal);
    }
}
