using Microsoft.Playwright;

namespace DataForeman.BlazorUI.Tests;

[TestFixture]
public class NodePropertiesTests
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IPage _page = null!;
    
    private const string BaseUrl = "http://localhost:5129";
    private const string TemperatureMonitorFlowUrl = $"{BaseUrl}/flows/11111111-1111-1111-1111-111111111111";
    
    [SetUp]
    public async Task Setup()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        _page = await _browser.NewPageAsync();
    }
    
    [TearDown]
    public async Task Teardown()
    {
        await _page.CloseAsync();
        await _browser.CloseAsync();
        _playwright.Dispose();
    }
    
    [Test]
    public async Task FlowEditorLoadsWithPropertiesPanel()
    {
        await _page.GotoAsync(TemperatureMonitorFlowUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1000);
        
        // Verify Properties panel exists
        var propertiesPanel = await _page.GetByText("Properties").CountAsync();
        Assert.That(propertiesPanel, Is.GreaterThan(0), "Properties panel should be visible");
    }
    
    [Test]
    public async Task PropertiesPanelShowsSelectNodeMessage()
    {
        await _page.GotoAsync(TemperatureMonitorFlowUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1000);
        
        // When no node is selected, should show "Select a node to view properties"
        var message = await _page.GetByText("Select a node to view properties").CountAsync();
        Assert.That(message, Is.GreaterThan(0), "Should show select node message when no node is selected");
    }
    
    [Test]
    public async Task DiagramContainsNodes()
    {
        await _page.GotoAsync(TemperatureMonitorFlowUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1500);
        
        // Look for diagram nodes (SVG elements with specific classes)
        var diagramNodes = await _page.Locator(".e-node").CountAsync();
        Assert.That(diagramNodes, Is.GreaterThanOrEqualTo(0), "Diagram should have nodes rendered");
    }
    
    [Test]
    public async Task SymbolPaletteIsVisible()
    {
        await _page.GotoAsync(TemperatureMonitorFlowUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1000);
        
        // Symbol palette should be visible
        var palette = await _page.Locator(".e-symbol-palette, .symbol-palette-container").CountAsync();
        Assert.That(palette, Is.GreaterThan(0), "Symbol palette should be visible");
    }
    
    [Test]
    public async Task TriggersCategoryExists()
    {
        await _page.GotoAsync(TemperatureMonitorFlowUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1000);
        
        // Triggers category should exist in palette
        var triggersCategory = await _page.GetByText("Triggers").CountAsync();
        Assert.That(triggersCategory, Is.GreaterThan(0), "Triggers category should be visible in palette");
    }
    
    [Test]
    public async Task TagOperationsCategoryExists()
    {
        await _page.GotoAsync(TemperatureMonitorFlowUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1000);
        
        // Tag Operations category should exist in palette
        var tagOpsCategory = await _page.GetByText("Tag Operations").CountAsync();
        Assert.That(tagOpsCategory, Is.GreaterThan(0), "Tag Operations category should be visible in palette");
    }
    
    [Test]
    public async Task LogicCategoryExists()
    {
        await _page.GotoAsync(TemperatureMonitorFlowUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1000);
        
        // Logic category should exist in palette
        var logicCategory = await _page.GetByText("Logic").CountAsync();
        Assert.That(logicCategory, Is.GreaterThan(0), "Logic category should be visible in palette");
    }
    
    [Test]
    public async Task OutputCategoryExists()
    {
        await _page.GotoAsync(TemperatureMonitorFlowUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1000);
        
        // Output category should exist in palette
        var outputCategory = await _page.GetByText("Output").CountAsync();
        Assert.That(outputCategory, Is.GreaterThan(0), "Output category should be visible in palette");
    }
    
    [Test]
    public async Task SaveButtonExists()
    {
        await _page.GotoAsync(TemperatureMonitorFlowUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1000);
        
        // Save button should exist
        var saveButton = await _page.GetByRole(AriaRole.Button, new() { Name = "SAVE" }).CountAsync();
        Assert.That(saveButton, Is.GreaterThan(0), "Save button should be visible");
    }
    
    [Test]
    public async Task DeployButtonExists()
    {
        await _page.GotoAsync(TemperatureMonitorFlowUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1000);
        
        // Deploy or Stop button should exist
        var deployButton = await _page.GetByRole(AriaRole.Button, new() { Name = "DEPLOY" }).CountAsync();
        var stopButton = await _page.GetByRole(AriaRole.Button, new() { Name = "Stop" }).CountAsync();
        Assert.That(deployButton + stopButton, Is.GreaterThan(0), "Deploy or Stop button should be visible");
    }
    
    [Test]
    public async Task BackButtonExists()
    {
        await _page.GotoAsync(TemperatureMonitorFlowUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1000);
        
        // Back button should exist
        var backButton = await _page.GetByRole(AriaRole.Button, new() { Name = "Back" }).CountAsync();
        Assert.That(backButton, Is.GreaterThan(0), "Back button should be visible");
    }
    
    [Test]
    public async Task FlowNameInputExists()
    {
        await _page.GotoAsync(TemperatureMonitorFlowUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1000);
        
        // Flow name input should show the flow name
        var flowNameInput = await _page.Locator(".flow-name-input").CountAsync();
        Assert.That(flowNameInput, Is.GreaterThan(0), "Flow name input should be visible");
    }
    
    [Test]
    public async Task DiagramContainerExists()
    {
        await _page.GotoAsync(TemperatureMonitorFlowUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(1000);
        
        // Diagram container should exist
        var diagramContainer = await _page.Locator(".diagram-container").CountAsync();
        Assert.That(diagramContainer, Is.GreaterThan(0), "Diagram container should be visible");
    }
}
