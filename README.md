# BrowserAgent

A .NET 10 console agent that uses the Microsoft Agent Framework, Azure OpenAI, and the Playwright MCP server to run natural-language browser automation tasks.

## Prerequisites

- .NET SDK 10.0 (preview) or newer
- Node.js 18+ (needed for the `@playwright/mcp` server)
- An Azure OpenAI resource with a chat completion deployment (for example `gpt-4o-mini`)

## Configuration

The agent reads configuration from `appsettings.json`, user secrets, environment variables, or command-line arguments. The following environment variables are the quickest way to get started:

| Variable | Required | Description |
| --- | --- | --- |
| `AZURE_OPENAI_ENDPOINT` | Yes | Endpoint of your Azure OpenAI resource, e.g. `https://contoso.openai.azure.com/`. |
| `AZURE_OPENAI_API_KEY` | Conditional | API key for the resource. Leave unset to use `DefaultAzureCredential`. |
| `AZURE_OPENAI_DEPLOYMENT_NAME` | No | Chat deployment name. Defaults to `gpt-4o-mini`. |
| `PLAYWRIGHT_MCP_COMMAND` | No | Command used to launch the MCP server. Defaults to `npx`. |
| `PLAYWRIGHT_MCP_ARGS` | No | Arguments passed to the MCP server command. Defaults to `@playwright/mcp@latest`. |
| `PLAYWRIGHT_MCP_NAME` | No | Friendly name for logging. Defaults to `playwright`. |
| `AGENT_INSTRUCTIONS` | No | Optional system instructions for the agent. |

> Tip: store secrets with `dotnet user-secrets set AZURE_OPENAI_API_KEY <key>` during development.

## Running the MCP Server

The agent launches the Playwright MCP server automatically via stdio, but you need Node.js installed. The default command is equivalent to:

```pwsh
npx @playwright/mcp@latest
```

Override `PLAYWRIGHT_MCP_COMMAND`/`PLAYWRIGHT_MCP_ARGS` if you have a custom installation or want to pin a version. The server downloads Playwright on first run, so expect a short delay.

## Running the Agent

```pwsh
$env:AZURE_OPENAI_ENDPOINT = "https://contoso.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY = "<key>"
$env:AZURE_OPENAI_DEPLOYMENT_NAME = "gpt-4o-mini"

cd c:/development/computer-use/BrowserAgent
Dotnet run --framework net10.0
```

After the warm-up, you will see `prompt>`—enter natural-language tasks such as:

```
prompt> Check https://www.microsoft.com and summarize the hero section.
```

Type `exit` or press `Ctrl+C` to quit.

## Troubleshooting

- **Authentication:** If no API key is provided the agent falls back to `DefaultAzureCredential`. Ensure the signed-in identity has access to the Azure OpenAI resource.
- **Missing Playwright tools:** Confirm Node.js is installed and `npx @playwright/mcp@latest` works manually.
- **Slow first run:** Playwright downloads browsers on the first invocation; subsequent runs are faster.
- **Verbose logging:** Adjust `Logging:LogLevel:Default` in `appsettings.json` if you need more verbosity.
