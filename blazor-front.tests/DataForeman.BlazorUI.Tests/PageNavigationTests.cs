using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace DataForeman.BlazorUI.Tests;

/// <summary>
/// Tests for page navigation and basic UI functionality
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class PageNavigationTests : PageTest
{
    private const string BaseUrl = "http://localhost:5129";
    
    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            IgnoreHTTPSErrors = true
        };
    }

    [Test]
    public async Task Dashboard_PageLoads_ShowsSystemOverview()
    {
        // Navigate to dashboard
        await Page.GotoAsync($"{BaseUrl}/");
        
        // Wait for page to load
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Check that the page title contains Dashboard
        var heading = await Page.Locator("h1").TextContentAsync();
        Assert.That(heading, Does.Contain("Dashboard"));
        
        // Check for system overview section
        var systemOverview = await Page.Locator("text=System Overview").CountAsync();
        Assert.That(systemOverview, Is.GreaterThan(0), "System Overview section should be visible");
    }
    
    [Test]
    public async Task FlowStudio_PageLoads_ShowsFlowsList()
    {
        // Navigate to Flow Studio
        await Page.GotoAsync($"{BaseUrl}/flows");
        
        // Wait for page to load
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Check for page header
        var heading = await Page.Locator("h1").TextContentAsync();
        Assert.That(heading, Does.Contain("Flow Studio"));
        
        // Check for New Flow button
        var newFlowButton = await Page.Locator("button:has-text('New Flow')").CountAsync();
        Assert.That(newFlowButton, Is.GreaterThan(0), "New Flow button should be visible");
    }
    
    [Test]
    public async Task ChartComposer_PageLoads_ShowsChartList()
    {
        // Navigate to Chart Composer
        await Page.GotoAsync($"{BaseUrl}/charts");
        
        // Wait for page to load
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Check for page header
        var heading = await Page.Locator("h1").TextContentAsync();
        Assert.That(heading, Does.Contain("Chart"));
    }
    
    [Test]
    public async Task Connectivity_PageLoads_ShowsTabs()
    {
        // Navigate to Connectivity
        await Page.GotoAsync($"{BaseUrl}/connectivity");
        
        // Wait for page to load
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Check for page header
        var heading = await Page.Locator("h1").TextContentAsync();
        Assert.That(heading, Does.Contain("Connectivity"));
        
        // Check for tabs
        var connectionsTab = await Page.Locator("text=CONNECTIONS").CountAsync();
        Assert.That(connectionsTab, Is.GreaterThan(0), "Connections tab should be visible");
    }
    
    [Test]
    public async Task Diagnostics_PageLoads_ShowsSystemInfo()
    {
        // Navigate to Diagnostics
        await Page.GotoAsync($"{BaseUrl}/diagnostics");
        
        // Wait for page to load
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Check for page header
        var heading = await Page.Locator("h1").TextContentAsync();
        Assert.That(heading, Does.Contain("Diagnostics"));
        
        // Check for system info section
        var systemInfo = await Page.Locator("text=CPU Usage").CountAsync();
        Assert.That(systemInfo, Is.GreaterThan(0), "CPU Usage should be visible");
    }
    
    [Test]
    public async Task Sidebar_NavigationWorks()
    {
        // Start at Dashboard
        await Page.GotoAsync($"{BaseUrl}/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Click on Flow Studio in sidebar
        await Page.Locator("a:has-text('Flow Studio')").First.ClickAsync();
        await Page.WaitForURLAsync($"{BaseUrl}/flows");
        
        // Verify we're on Flow Studio page
        var heading = await Page.Locator("h1").TextContentAsync();
        Assert.That(heading, Does.Contain("Flow Studio"));
        
        // Navigate to Chart Composer
        await Page.Locator("a:has-text('Chart Composer')").First.ClickAsync();
        await Page.WaitForURLAsync($"{BaseUrl}/charts");
        
        heading = await Page.Locator("h1").TextContentAsync();
        Assert.That(heading, Does.Contain("Chart"));
    }
}
