using System.Collections.Generic;
using System.Linq;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace BrowserAgent;

internal static class Program
{
    private const string ExitCommand = "exit";

    private static readonly string[] DefaultPlaywrightArgs =
        ["@playwright/mcp@latest"];

    public static async Task Main(string[] args)
    {
        IConfiguration configuration = BuildConfiguration(args);
        using ILoggerFactory loggerFactory = CreateLoggerFactory();
        ILogger logger = loggerFactory.CreateLogger("BrowserAgent");

        using CancellationTokenSource cts = new();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await RunAgentConsoleAsync(configuration, loggerFactory, logger, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Cancellation requested. Shutting down.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception while running the agent.");
        }
    }

    private static async Task RunAgentConsoleAsync(
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        IChatClient chatClient = CreateChatClient(configuration);
        PlaywrightMcpOptions mcpOptions = PlaywrightMcpOptions.Bind(configuration);
        string instructions = configuration["AGENT_INSTRUCTIONS"] ??
            "You are a browser operations specialist. Use the Playwright MCP tools to inspect pages, " +
            "fill forms, and report factual findings before responding.";

        await using McpClient mcpClient = await McpClient.CreateAsync(new StdioClientTransport(new()
        {
            Name = mcpOptions.Name,
            Command = mcpOptions.Command,
            Arguments = mcpOptions.Arguments.ToList(),
        })).ConfigureAwait(false);

        var mcpTools = await mcpClient.ListToolsAsync().ConfigureAwait(false);
        
        logger.LogInformation(
            "Connected to Playwright MCP server '{Name}' ({Command} {Arguments}) and discovered {ToolCount} tools.",
            mcpOptions.Name,
            mcpOptions.Command,
            string.Join(' ', mcpOptions.Arguments),
            mcpTools.Count);

        AIAgent agent = chatClient.CreateAIAgent(
            instructions: instructions,
            tools: [.. mcpTools.Cast<AITool>()],
            loggerFactory: loggerFactory);

        logger.LogInformation(
            "Ready. Type natural-language tasks (or '{ExitCommand}' to quit). The agent will call Playwright tools as needed.",
            ExitCommand);

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("prompt> ");
            string? prompt = Console.ReadLine();

            if (prompt is null)
            {
                break;
            }

            if (string.Equals(prompt, ExitCommand, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Exit requested. Goodbye!");
                break;
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                continue;
            }

            try
            {
                AgentRunResponse response = await agent.RunAsync(prompt, cancellationToken: cancellationToken).ConfigureAwait(false);
                Console.WriteLine(response.Text ?? "[no response]\n");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Agent run failed. Fix the issue and try again.");
            }
        }
    }

    private static IChatClient CreateChatClient(IConfiguration configuration)
    {
        string endpoint = configuration["AZURE_OPENAI_ENDPOINT"]
            ?? throw new InvalidOperationException("Set AZURE_OPENAI_ENDPOINT to your Azure OpenAI endpoint.");

        string deploymentName = configuration["AZURE_OPENAI_DEPLOYMENT_NAME"] ?? "gpt-4o";
        string? apiKey = configuration["AZURE_OPENAI_API_KEY"];

        Console.WriteLine($"Using Azure OpenAI deployment '{deploymentName}' at '{endpoint}'.\n");

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));

        ChatClient chatClient = client.GetChatClient(deploymentName);
        return chatClient.AsIChatClient();
    }

    private static IConfiguration BuildConfiguration(string[] args) =>
        new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            // .AddUserSecrets<UserSecretsMarker>(optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

    private static ILoggerFactory CreateLoggerFactory() =>
        LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
        });

    private sealed record PlaywrightMcpOptions(string Name, string Command, IReadOnlyList<string> Arguments)
    {
        public static PlaywrightMcpOptions Bind(IConfiguration configuration)
        {
            string name = configuration["PLAYWRIGHT_MCP_NAME"] ?? "playwright";
            string command = configuration["PLAYWRIGHT_MCP_COMMAND"] ?? "npx";
            string? rawArgs = configuration["PLAYWRIGHT_MCP_ARGS"];

            Console.WriteLine($"Using Playwright MCP server '{name}' ({command} {rawArgs}).\n");
            
            IReadOnlyList<string> arguments = string.IsNullOrWhiteSpace(rawArgs)
                ? DefaultPlaywrightArgs
                : ParseArguments(rawArgs);

            return new PlaywrightMcpOptions(name, command, arguments);
        }

        private static string[] ParseArguments(string value) => value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

}
