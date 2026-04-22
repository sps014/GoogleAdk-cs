using GoogleAdk.Core.Skills;

namespace GoogleAdk.Core.Tests.Skills;

public class SkillModelsTests
{
    [Fact]
    public void Frontmatter_ShouldSerializeAndDeserialize()
    {
        var json = """
        {
            "name": "test-skill",
            "description": "A test skill",
            "license": "MIT",
            "compatibility": "1.0",
            "allowed-tools": "tool1 tool2",
            "metadata": {
                "key": "value"
            }
        }
        """;

        var frontmatter = System.Text.Json.JsonSerializer.Deserialize<Frontmatter>(json);

        Assert.NotNull(frontmatter);
        Assert.Equal("test-skill", frontmatter.Name);
        Assert.Equal("A test skill", frontmatter.Description);
        Assert.Equal("MIT", frontmatter.License);
        Assert.Equal("1.0", frontmatter.Compatibility);
        Assert.Equal("tool1 tool2", frontmatter.AllowedTools);
        Assert.Equal("value", frontmatter.Metadata["key"].ToString());
    }

    [Fact]
    public void Resources_ShouldStoreAndRetrieveFiles()
    {
        var resources = new Resources();
        resources.References["doc.md"] = "markdown content";
        resources.Assets["image.png"] = new byte[] { 1, 2, 3 };
        resources.Scripts["script.sh"] = new Script { Src = "echo hello" };

        Assert.Equal("markdown content", resources.GetReference("doc.md"));
        Assert.Equal(new byte[] { 1, 2, 3 }, resources.GetAsset("image.png"));
        Assert.Equal("echo hello", resources.GetScript("script.sh")?.Src);
        Assert.Null(resources.GetReference("missing"));
    }
}