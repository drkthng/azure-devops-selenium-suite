# azure-devops-selenium-suite

Scheduled Azure DevOps pipeline for headless Selenium tests, plus a one-click browser dashboard to trigger runs manually.

## What's in here

| File | Purpose |
|---|---|
| `pipelines/build.yml` | Build pipeline — triggers on every push to `master` |
| `pipelines/test.yml` | Test pipeline — runs the Selenium suite on a schedule (Tue & Fri) or on demand |
| `trigger-dashboard/index.html` | Single-page app with a big "Run Test Suite" button for non-technical team members |

## Pipeline setup

### 1. Build pipeline (`pipelines/build.yml`)

Create a new pipeline in Azure DevOps, point it at `pipelines/build.yml`. It will:

- Trigger on every push to `master`
- Build the solution
- Publish the build artifact as `drop`

### 2. Test pipeline (`pipelines/test.yml`)

Create a second pipeline, point it at `pipelines/test.yml`. Before using it:

1. Replace `<BUILD_PIPELINE_ID>` with the numeric ID of your build pipeline (visible in the pipeline URL as `definitionId`)
2. Adjust the cron schedule if needed (default: Tuesday and Friday at 06:00 UTC)
3. Adapt the `dotnet test` command to match your test project structure

This pipeline will:

- Download the latest successful build artifact
- Run the Selenium tests headlessly on the hosted agent
- Publish test results to the Azure DevOps Tests tab

### ChromeOptions (important for hosted agents)

Make sure your test code includes these flags:

```csharp
var options = new ChromeOptions();
options.AddArgument("--headless=new");
options.AddArgument("--no-sandbox");
options.AddArgument("--disable-dev-shm-usage");
options.AddArgument("--disable-gpu");
```

## Trigger dashboard

Open `trigger-dashboard/index.html` in any browser. On first use, click **⚙ Configuration** and enter:

- **Organization** — your Azure DevOps org name
- **Project** — the project name
- **Pipeline ID** — numeric ID of the *test* pipeline
- **PAT** — a Personal Access Token with `Build > Read & Execute` permission

Settings are saved in the browser's localStorage. Share the file via SharePoint, a file share, or let people open it locally.

## License

MIT
