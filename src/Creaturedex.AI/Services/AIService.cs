using Microsoft.Extensions.AI;

namespace Creaturedex.AI.Services;

public class AIService(IChatClient chatClient)
{
    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt)
        };

        var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
        return response.Text ?? string.Empty;
    }

    public async IAsyncEnumerable<string> StreamAsync(string systemPrompt, string userPrompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt)
        };

        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, cancellationToken: ct))
        {
            if (update.Text is not null)
                yield return update.Text;
        }
    }
}
