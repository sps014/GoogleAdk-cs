using GoogleAdk.Core.Tools;
using Xunit;
using System.Collections.Generic;

namespace GoogleAdk.Core.Tests;

public class SpannerToolTests
{
    [Fact]
    public void SpannerMetadataTool_HasCorrectDeclaration()
    {
        var tool = new SpannerMetadataTool();
        var decl = tool.GetDeclaration();
        
        Assert.NotNull(decl);
        Assert.Equal("spanner_metadata", decl!.Name);
        Assert.NotNull(decl.Parameters?.Properties);
        Assert.Contains("action", decl.Parameters!.Properties!.Keys);
    }

    [Fact]
    public void SpannerSearchTool_HasCorrectDeclaration()
    {
        var tool = new SpannerSearchTool();
        var decl = tool.GetDeclaration();
        
        Assert.NotNull(decl);
        Assert.Equal("spanner_search", decl!.Name);
        Assert.NotNull(decl.Parameters?.Properties);
        Assert.Contains("embeddingColumnName", decl.Parameters!.Properties!.Keys);
        Assert.Contains("modelName", decl.Parameters.Properties.Keys);
    }
}
