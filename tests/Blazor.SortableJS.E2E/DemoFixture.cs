using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Playwright;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Blazor.SortableJS.E2E;

[CollectionDefinition("BlazingStory demo")]
public sealed class DemoCollectionDefinition : ICollectionFixture<DemoFixture>
{
    public const string Name = "BlazingStory demo";
}

public sealed class DemoFixture : IAsyncLifetime
{
    private readonly ConcurrentQueue<string> _serverOutput = new();
    private Process? _demoProcess;
    private IPlaywright? _playwright;

    public string BaseUrl { get; private set; } = string.Empty;
    public IBrowser Browser { get; private set; } = null!;
    public IPage Page { get; private set; } = null!;

    /// <summary>
    /// Unhandled JavaScript errors and Blazor render exceptions seen since the last navigation.
    /// </summary>
    /// <remarks>
    /// Every DOM-reconciliation defect found in this library announced itself here and nowhere
    /// else: the model stayed correct, the drag still "worked", and only the console showed that
    /// Blazor had thrown. A suite that ignores the console cannot see them.
    /// </remarks>
    private readonly List<string> _jsErrors = new();

    public async ValueTask InitializeAsync()
    {
        var repositoryRoot = FindRepositoryRoot();
        var port = ReserveTcpPort();
        BaseUrl = $"http://127.0.0.1:{port}";

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(Path.Combine(repositoryRoot, "demo", "Blazor.SortableJS.Demo.csproj"));
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--no-launch-profile");
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add(BaseUrl);
        startInfo.Environment["DOTNET_NOLOGO"] = "1";

        _demoProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _demoProcess.OutputDataReceived += CaptureServerOutput;
        _demoProcess.ErrorDataReceived += CaptureServerOutput;
        if (!_demoProcess.Start())
        {
            throw new InvalidOperationException("Could not start the BlazingStory demo process.");
        }

        _demoProcess.BeginOutputReadLine();
        _demoProcess.BeginErrorReadLine();
        await WaitForDemoAsync(TimeSpan.FromMinutes(5));

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Channel = "chrome",
            Headless = true,
            Args = ["--disable-dev-shm-usage"]
        });
        await CreatePageAsync();
    }

    /// <summary>
    /// Replaces <see cref="Page"/> with a fresh one.
    /// </summary>
    /// <remarks>
    /// Tests share this fixture, and a page carries state a drag can trip over: the pointer's
    /// position, a half-finished SortableJS interaction, listeners from the previous story. One
    /// page per scenario removes that as a source of intermittent failures.
    /// </remarks>
    private async Task CreatePageAsync()
    {
        if (Page is not null)
        {
            await Page.CloseAsync();
        }

        Page = await Browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = 1440, Height = 1000 }
        });

        Page.Console += (_, message) =>
        {
            if (message.Type == "error" && !IsUnrelatedNoise(message.Text))
            {
                lock (_jsErrors) _jsErrors.Add(FirstLine(message.Text));
            }
        };
        Page.PageError += (_, error) =>
        {
            lock (_jsErrors) _jsErrors.Add(FirstLine(error));
        };
    }

    /// <summary>
    /// Fails the current test if the page logged an unhandled JavaScript or Blazor render error.
    /// </summary>
    public void AssertNoJsErrors()
    {
        string[] errors;
        lock (_jsErrors) errors = _jsErrors.Distinct().ToArray();
        Assert.True(
            errors.Length == 0,
            "The page reported unhandled errors:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    private static string FirstLine(string text)
    {
        var lines = text.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        return lines.Length == 0 ? text : lines[0];
    }

    /// <summary>Browser-extension chatter and asset noise that says nothing about this library.</summary>
    private static bool IsUnrelatedNoise(string text)
    {
        return text.Contains("contentscript")
            || text.Contains("Failed to load resource")
            || text.Contains("preloaded using link preload");
    }

    public async ValueTask DisposeAsync()
    {
        if (Page is not null)
        {
            await Page.CloseAsync();
        }

        if (Browser is not null)
        {
            await Browser.CloseAsync();
        }

        _playwright?.Dispose();

        if (_demoProcess is { HasExited: false })
        {
            _demoProcess.Kill(entireProcessTree: true);
            await _demoProcess.WaitForExitAsync();
        }

        _demoProcess?.Dispose();
    }

    /// <summary>
    /// Navigates straight to the story canvas page rather than the storybook shell.
    /// </summary>
    /// <remarks>
    /// BlazingStory's shell forwards args to the canvas only for values it recognises as
    /// registered story parameters, so a shell URL silently drops <c>ForceFallback</c>. The canvas
    /// page (<c>iframe.html</c> -&gt; <c>IFrame.razor</c> -&gt; <c>CanvasFrame</c>) parses args
    /// from its own query string, so addressing it directly is both reliable and faster - no
    /// sidebar, no panels, no cross-frame hop.
    /// </remarks>
    public async Task<ILocator> NavigateToStoryAsync(string storyId)
    {
        lock (_jsErrors) _jsErrors.Clear();
        var url = $"{BaseUrl}/iframe.html?viewMode=story&id={storyId}&args=ForceFallback:true&e2e={Guid.NewGuid():N}";
        await Page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

        var canvas = Page.Locator("body");
        await canvas.GetByTestId("model-state").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 60_000
        });

        // Proves the arg actually reached the story rather than the story silently running native
        // drag, which Playwright cannot drive - every drag below would then fail for the wrong reason.
        var fallbackValue = await canvas.Locator("[data-fallback-forced]").First.GetAttributeAsync("data-fallback-forced");
        Assert.Equal("true", fallbackValue?.ToLowerInvariant());

        // The model panel renders before the component's OnAfterRenderAsync has imported the
        // interop module and constructed the SortableJS instances. A drag started in that window
        // is silently inert, which shows up later as an intermittent "model never changed".
        await Page.WaitForFunctionAsync(
            @"() => {
                if (!window.Sortable) return false;
                const lists = new Set(Array.from(document.querySelectorAll('[data-sortable-item]')).map(e => e.parentElement));
                return lists.size > 0 && Array.from(lists).every(l => !!window.Sortable.get(l));
            }",
            null,
            new PageWaitForFunctionOptions { Timeout = 30_000 });

        return canvas;
    }

    private async Task WaitForDemoAsync(TimeSpan timeout)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_demoProcess?.HasExited == true)
            {
                throw new InvalidOperationException($"The demo exited before becoming ready.{Environment.NewLine}{ServerLog()}");
            }

            try
            {
                using var response = await client.GetAsync(BaseUrl);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"The demo was not ready after {timeout}.{Environment.NewLine}{ServerLog()}");
    }

    private void CaptureServerOutput(object sender, DataReceivedEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.Data))
        {
            _serverOutput.Enqueue(args.Data);
        }
    }

    private string ServerLog() => string.Join(Environment.NewLine, _serverOutput.TakeLast(100));

    private static int ReserveTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "demo", "Blazor.SortableJS.Demo.csproj")) &&
                File.Exists(Path.Combine(directory.FullName, "src", "Blazor.SortableJS.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Blazor.SortableJS repository root.");
    }
}
