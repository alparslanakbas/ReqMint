using ReqMint.Core.Workspaces;

namespace ReqMint.Core.Tests;

public sealed class RequestAssertionValidatorTests
{
    [Fact]
    public void GetValidationError_AcceptsSupportedAssertionShapes()
    {
        RequestAssertion[] assertions =
        [
            new()
            {
                Kind = RequestAssertionKind.StatusCodeEquals,
                ExpectedStatusCode = 201,
            },
            new()
            {
                Kind = RequestAssertionKind.MaximumDuration,
                MaximumDurationMilliseconds = 500,
            },
            new()
            {
                Kind = RequestAssertionKind.JsonPointerExists,
                JsonPointer = "/data/a~1b/~0metadata",
            },
        ];

        Assert.Null(RequestAssertionValidator.GetValidationError(assertions));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(600)]
    public void GetValidationError_RejectsInvalidStatusCodes(int statusCode)
    {
        var assertion = new RequestAssertion
        {
            Kind = RequestAssertionKind.StatusCodeEquals,
            ExpectedStatusCode = statusCode,
        };

        Assert.NotNull(RequestAssertionValidator.GetValidationError([assertion]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("data/id")]
    [InlineData("/data/~2id")]
    [InlineData("/data/~")]
    public void GetValidationError_RejectsInvalidJsonPointers(string pointer)
    {
        var assertion = new RequestAssertion
        {
            Kind = RequestAssertionKind.JsonPointerExists,
            JsonPointer = pointer,
        };

        Assert.NotNull(RequestAssertionValidator.GetValidationError([assertion]));
    }
}
