using System.Collections.Generic;
using System.Threading.Tasks;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Core.Tools;

/// <summary>
/// A toolset that provides computer control capabilities to an agent.
/// </summary>
public sealed class ComputerUseToolset : BaseToolset
{
    private readonly IComputerDriver _driver;
    private readonly (int Width, int Height)? _virtualScreenSize;

    public ComputerUseToolset(IComputerDriver driver, (int Width, int Height)? virtualScreenSize = null)
    {
        _driver = driver;
        _virtualScreenSize = virtualScreenSize;
    }

    public override Task<IReadOnlyList<BaseTool>> GetToolsAsync(AgentContext? context = null)
    {
        var list = new List<BaseTool> { new ComputerUseTool(_driver, _virtualScreenSize) };
        return Task.FromResult<IReadOnlyList<BaseTool>>(list);
    }
}
