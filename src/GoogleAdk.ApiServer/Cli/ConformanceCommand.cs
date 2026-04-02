using System.CommandLine;

namespace GoogleAdk.ApiServer.Cli;

public static class ConformanceCommand
{
    public static Command Create()
    {
        var command = new Command("conformance", "Conformance tooling.");

        var record = new Command("record", "Record conformance fixtures.");
        record.SetAction(_ => Console.WriteLine("Conformance record is not yet implemented."));

        var test = new Command("test", "Run conformance tests.");
        test.SetAction(_ => Console.WriteLine("Conformance test is not yet implemented."));

        command.Subcommands.Add(record);
        command.Subcommands.Add(test);
        return command;
    }
}
