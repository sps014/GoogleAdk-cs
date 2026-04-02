using System.CommandLine;

namespace GoogleAdk.ApiServer.Cli;

public static class EvalSetCommand
{
    public static Command Create()
    {
        var command = new Command("eval_set", "Manage evaluation sets.");

        var create = new Command("create", "Create a new eval set.");
        create.SetAction(_ => Console.WriteLine("Eval set create is not yet implemented."));

        var add = new Command("add_eval_case", "Add a case to an eval set.");
        add.SetAction(_ => Console.WriteLine("Eval set add_eval_case is not yet implemented."));

        command.Subcommands.Add(create);
        command.Subcommands.Add(add);
        return command;
    }
}
