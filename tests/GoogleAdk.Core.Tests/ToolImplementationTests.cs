using GoogleAdk.Core.Tools;

namespace GoogleAdk.Core.Tests;

public class ToolImplementationTests
{
    [Fact]
    public void BigQueryQueryTool_HasCorrectDeclaration()
    {
        var tool = new BigQueryQueryTool();
        var decl = tool.GetDeclaration();
        Assert.NotNull(decl);
        Assert.Equal("bigquery_query", decl!.Name);
    }

    [Fact]
    public void BigQueryMetadataTool_HasCorrectDeclaration()
    {
        var tool = new BigQueryMetadataTool();
        var decl = tool.GetDeclaration();
        Assert.NotNull(decl);
        Assert.Equal("bigquery_metadata", decl!.Name);
    }

    [Fact]
    public void SpannerQueryTool_HasCorrectDeclaration()
    {
        var tool = new SpannerQueryTool();
        var decl = tool.GetDeclaration();
        Assert.NotNull(decl);
        Assert.Equal("spanner_query", decl!.Name);
    }

    [Fact]
    public void BigtableQueryTool_HasCorrectDeclaration()
    {
        var tool = new BigtableQueryTool();
        var decl = tool.GetDeclaration();
        Assert.NotNull(decl);
        Assert.Equal("bigtable_query", decl!.Name);
    }

    [Fact]
    public void PubSubMessageTool_HasCorrectDeclaration()
    {
        var tool = new PubSubMessageTool();
        var decl = tool.GetDeclaration();
        Assert.NotNull(decl);
        Assert.Equal("pubsub_publish", decl!.Name);
    }

    [Fact]
    public void GoogleApiTool_HasCorrectDeclaration()
    {
        var tool = new GoogleApiTool();
        var decl = tool.GetDeclaration();
        Assert.NotNull(decl);
        Assert.Equal("google_api_call", decl!.Name);
    }

    [Fact]
    public void ApiHubTool_HasCorrectDeclaration()
    {
        var tool = new ApiHubTool();
        var decl = tool.GetDeclaration();
        Assert.NotNull(decl);
        Assert.Equal("apihub_search", decl!.Name);
    }
}
