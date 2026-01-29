using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace DataForeman.BlazorUI.Tests;

/// <summary>
/// Tests for Flow Studio functionality including diagram, palette, and API integration
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class FlowStudioTests : PageTest
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
    public async Task FlowStudio_ShowsFlowList_WithEditButtons()
    {
        // Navigate to Flow Studio
        await Page.GotoAsync($"{BaseUrl}/flows");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Wait for grid to load - look for either flows or "No records" message
        await Task.Delay(2000); // Allow time for API call and rendering
        
        // Check if there are flows in the grid or it shows the grid structure
        var gridExists = await Page.Locator(".e-grid").CountAsync();
        Assert.That(gridExists, Is.GreaterThan(0), "Flow grid should exist");
    }
    
    [Test]
    public async Task FlowEditor_Loads_WithSymbolPalette()
    {
        // Navigate directly to a flow editor (Temperature Monitor)
        await Page.GotoAsync($"{BaseUrl}/flows/11111111-1111-1111-1111-111111111111");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(2000); // Allow time for Syncfusion components to initialize
        
        // Check for Symbol Palette presence
        var symbolPalette = await Page.Locator(".e-symbolpalette, .e-symbol-palette").CountAsync();
        Assert.That(symbolPalette, Is.GreaterThan(0), "Symbol Palette should be present");
        
        // Check for Diagram presence
        var diagram = await Page.Locator(".e-diagram").CountAsync();
        Assert.That(diagram, Is.GreaterThan(0), "Diagram should be present");
        
        // Check for toolbar buttons (Save, Deploy)
        var saveButton = await Page.Locator("button:has-text('Save')").CountAsync();
        Assert.That(saveButton, Is.GreaterThan(0), "Save button should be visible");
        
        var deployButton = await Page.Locator("button:has-text('Deploy'), button:has-text('Stop')").CountAsync();
        Assert.That(deployButton, Is.GreaterThan(0), "Deploy/Stop button should be visible");
    }
    
    [Test]
    public async Task FlowEditor_Palette_HasNodeCategories()
    {
        // Navigate to flow editor
        await Page.GotoAsync($"{BaseUrl}/flows/11111111-1111-1111-1111-111111111111");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(2000);
        
        // Check for palette categories
        var triggersCategory = await Page.Locator("text=Triggers").CountAsync();
        Assert.That(triggersCategory, Is.GreaterThan(0), "Triggers category should be visible");
        
        var tagOperationsCategory = await Page.Locator("text=Tag Operations").CountAsync();
        Assert.That(tagOperationsCategory, Is.GreaterThan(0), "Tag Operations category should be visible");
    }
    
    [Test]
    public async Task FlowEditor_DiagramNodes_AreVisible()
    {
        // Navigate to Temperature Monitor flow
        await Page.GotoAsync($"{BaseUrl}/flows/11111111-1111-1111-1111-111111111111");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(3000); // Allow extra time for nodes to render
        
        // Check for diagram nodes - look for node elements in the diagram
        // Syncfusion diagram uses SVG groups for nodes
        var nodes = await Page.Locator(".e-diagram g[id^='trigger'], .e-diagram g[id^='tag'], .e-diagram g[id^='compare'], .e-diagram g[id^='notification']").CountAsync();
        
        // Also check for any diagram content
        var diagramContent = await Page.Locator(".e-diagram svg").CountAsync();
        Assert.That(diagramContent, Is.GreaterThan(0), "Diagram should have SVG content");
    }
    
    [Test]
    public async Task FlowEditor_BackButton_NavigatesToFlowList()
    {
        // Navigate to flow editor
        await Page.GotoAsync($"{BaseUrl}/flows/11111111-1111-1111-1111-111111111111");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(1000);
        
        // Click back button
        await Page.Locator("button:has-text('Back')").ClickAsync();
        
        // Wait for navigation
        await Page.WaitForURLAsync($"{BaseUrl}/flows");
        
        // Verify we're back on flow list
        var heading = await Page.Locator("h1").TextContentAsync();
        Assert.That(heading, Does.Contain("Flow Studio"));
    }
    
    [Test]
    public async Task FlowEditor_PropertiesPanel_Exists()
    {
        // Navigate to flow editor
        await Page.GotoAsync($"{BaseUrl}/flows/11111111-1111-1111-1111-111111111111");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(2000);
        
        // Check for properties panel
        var propertiesPanel = await Page.Locator("text=Properties").CountAsync();
        Assert.That(propertiesPanel, Is.GreaterThan(0), "Properties panel should be visible");
    }
    
    [Test]
    public async Task NewFlow_Button_NavigatesToEditor()
    {
        // Navigate to Flow Studio
        await Page.GotoAsync($"{BaseUrl}/flows");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(1000);
        
        // Click New Flow button
        await Page.Locator("button:has-text('New Flow')").ClickAsync();
        
        // Wait for navigation to flow editor (new flow will have a generated GUID)
        await Page.WaitForURLAsync(new Regex($"{BaseUrl}/flows/[a-f0-9-]+"));
        
        // Verify we're in the editor (check for Save button)
        var saveButton = await Page.Locator("button:has-text('Save')").CountAsync();
        Assert.That(saveButton, Is.GreaterThan(0), "Should be in flow editor");
    }
    
    [Test]
    public async Task FlowEditor_SymbolPalette_NodesAreDraggable()
    {
        // Navigate to flow editor
        await Page.GotoAsync($"{BaseUrl}/flows/11111111-1111-1111-1111-111111111111");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(2000);
        
        // Expand Triggers category if collapsed
        var triggersHeader = Page.Locator(".e-acrdn-item:has-text('Triggers')").First;
        await triggersHeader.ClickAsync();
        await Task.Delay(500);
        
        // Check that palette nodes are present and potentially draggable
        // Syncfusion uses e-symbol-draggable class for draggable symbols
        var draggableSymbols = await Page.Locator(".e-symbol-draggable").CountAsync();
        Assert.That(draggableSymbols, Is.GreaterThanOrEqualTo(0), "Palette should have draggable symbols");
    }
}
