using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace GoogleAdk.Models.Meai
{
    public class AdkAIFunction : AIFunction
    {
        public AdkAIFunction(string name, string description, JsonElement schema)
        {
            Name = name;
            Description = description;
            JsonSchema = schema;
        }

        public override string Name { get; }
        public override string Description { get; }
        public override JsonElement JsonSchema { get; }

        protected override ValueTask<object?> InvokeCoreAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken)
        {
            return new ValueTask<object?>(Task.FromResult<object?>(null));
        }
    }
}