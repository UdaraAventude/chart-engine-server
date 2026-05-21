using ChartEngine.Application.DTOs;
using ChartEngine.Domain.ValueObjects;
using ChartEngine.Infrastructure.Analytics;

namespace ChartEngine.Tests;

public class TreeNavigatorTests
{
    private static TreeNode BuildSampleTree()
    {
        return new TreeNode
        {
            Name = "root",
            Children = new List<TreeNode>
            {
                new()
                {
                    Name = "Engineering",
                    Children = new List<TreeNode>
                    {
                        new() { Name = "Junior", Children = new List<TreeNode>() },
                        new() { Name = "Senior", Children = new List<TreeNode>() },
                    },
                },
                new()
                {
                    Name = "Sales",
                    Children = new List<TreeNode>
                    {
                        new() { Name = "East", Children = new List<TreeNode>() },
                    },
                },
            },
        };
    }

    [Fact]
    public void NavigateToPath_FollowsNamedChildren()
    {
        var root = BuildSampleTree();
        var path = new List<DrillPathStepDto>
        {
            new() { Column = "department", Value = "Engineering" },
            new() { Column = "level", Value = "Senior" },
        };

        var node = TreeNavigator.NavigateToPath(root, path);

        Assert.Equal("Senior", node.Name);
    }

    [Fact]
    public void NavigateToPath_InvalidValue_StopsAtLastValid()
    {
        var root = BuildSampleTree();
        var path = new List<DrillPathStepDto>
        {
            new() { Column = "department", Value = "Engineering" },
            new() { Column = "level", Value = "NonExistent" },
        };

        var node = TreeNavigator.NavigateToPath(root, path);

        Assert.Equal("Engineering", node.Name);
    }

    [Fact]
    public void ParseDrillPathJson_ParsesArray()
    {
        var json = """[{"column":"dept","value":"Sales"}]""";
        var steps = TreeNavigator.ParseDrillPathJson(json);

        Assert.Single(steps);
        Assert.Equal("dept", steps[0].Column);
        Assert.Equal("Sales", steps[0].Value);
    }

    [Fact]
    public void NavigateToLevel_FirstChildEachLevel()
    {
        var root = BuildSampleTree();
        var node = TreeNavigator.NavigateToLevel(root, 2);

        Assert.Equal("Junior", node.Name);
    }
}
