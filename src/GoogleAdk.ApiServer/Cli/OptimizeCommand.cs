using System.CommandLine;

namespace GoogleAdk.ApiServer.Cli;

public static class OptimizeCommand
{
    public static Command Create()
    {
        var command = new Command("optimize", "Run prompt optimization.");
        command.SetAction(_ =>
        {
            Console.WriteLine("Optimize command is not yet implemented.");
        });
        return command;
    }
}
