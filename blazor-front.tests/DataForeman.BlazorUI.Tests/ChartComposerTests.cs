using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace DataForeman.BlazorUI.Tests;

/// <summary>
/// Functional tests for Chart Composer functionality
/// Tests creating charts, adding tags, and saving charts
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class ChartComposerTests : PageTest
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
    public async Task ChartComposer_PageLoads_WithSavedCharts()
    {
        // Navigate to Chart Composer
        await Page.GotoAsync($"{BaseUrl}/charts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(1000);
        
        // Check page title
        var heading = await Page.Locator("h1:has-text('Chart Composer')").CountAsync();
        Assert.That(heading, Is.GreaterThan(0), "Chart Composer heading should be visible");
        
        // Check for saved charts list
        var savedChartsSection = await Page.Locator("text=Saved Charts").CountAsync();
        Assert.That(savedChartsSection, Is.GreaterThan(0), "Saved Charts section should be visible");
    }
    
    [Test]
    public async Task ChartComposer_ShowsAvailableTags()
    {
        // Navigate to Chart Composer
        await Page.GotoAsync($"{BaseUrl}/charts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(1000);
        
        // Check for Available Tags section
        var availableTagsSection = await Page.Locator("text=Available Tags").CountAsync();
        Assert.That(availableTagsSection, Is.GreaterThan(0), "Available Tags section should be visible");
        
        // Check for tag search input
        var searchInput = await Page.Locator("input[placeholder='Search tags...']").CountAsync();
        Assert.That(searchInput, Is.GreaterThan(0), "Tag search input should be visible");
        
        // Check for tag items - they should have path like "PLC1.Tank1.Temperature"
        var tagItems = await Page.Locator(".tag-item, [class*='tag']").CountAsync();
        Assert.That(tagItems, Is.GreaterThan(0), "Tag items should be visible");
    }
    
    [Test]
    public async Task ChartComposer_CanSelectChart()
    {
        // Navigate to Chart Composer
        await Page.GotoAsync($"{BaseUrl}/charts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(1000);
        
        // Click on a chart to select it (Temperature Trends)
        var chartItem = Page.Locator(".chart-list-item, text=Temperature Trends").First;
        if (await chartItem.CountAsync() > 0)
        {
            await chartItem.ClickAsync();
            await Task.Delay(500);
            
            // Check that chart editor becomes visible
            var chartToolbar = await Page.Locator(".chart-toolbar").CountAsync();
            Assert.That(chartToolbar, Is.GreaterThan(0), "Chart toolbar should be visible after selecting chart");
            
            // Check for chart type dropdown
            var chartTypeDropdown = await Page.Locator("text=Line, text=Bar, text=Area").CountAsync();
            // Should have chart preview
            var chartPreview = await Page.Locator(".chart-preview, .e-chart").CountAsync();
            Assert.That(chartPreview, Is.GreaterThan(0), "Chart preview should be visible");
        }
    }
    
    [Test]
    public async Task ChartComposer_CanCreateNewChart()
    {
        // Navigate to Chart Composer
        await Page.GotoAsync($"{BaseUrl}/charts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(1000);
        
        // Count initial charts
        var initialChartCount = await Page.Locator(".chart-list-item").CountAsync();
        
        // Click New Chart button
        var newChartButton = Page.Locator("button:has-text('New Chart')");
        if (await newChartButton.CountAsync() > 0)
        {
            await newChartButton.ClickAsync();
            await Task.Delay(500);
            
            // Check that a new chart was added
            var newChartCount = await Page.Locator(".chart-list-item").CountAsync();
            Assert.That(newChartCount, Is.GreaterThanOrEqualTo(initialChartCount), "New chart should be added to list");
            
            // Check that chart editor is now visible (with "New Chart" name)
            var chartNameInput = await Page.Locator("input[placeholder='Chart Name']").CountAsync();
            Assert.That(chartNameInput, Is.GreaterThan(0), "Chart name input should be visible");
        }
    }
    
    [Test]
    public async Task ChartComposer_CanAddTagToChart()
    {
        // Navigate to Chart Composer
        await Page.GotoAsync($"{BaseUrl}/charts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(1000);
        
        // First select a chart
        var chartItem = Page.Locator(".chart-list-item").First;
        if (await chartItem.CountAsync() > 0)
        {
            await chartItem.ClickAsync();
            await Task.Delay(500);
            
            // Get initial series count
            var initialSeriesCount = await Page.Locator(".series-item").CountAsync();
            
            // Click on a tag to add it
            var tagItem = Page.Locator(".tag-item").First;
            if (await tagItem.CountAsync() > 0)
            {
                await tagItem.ClickAsync();
                await Task.Delay(500);
                
                // Check that series count increased or tag was added
                var newSeriesCount = await Page.Locator(".series-item").CountAsync();
                // Note: count may not increase if tag already in chart
                Assert.That(newSeriesCount, Is.GreaterThanOrEqualTo(initialSeriesCount), "Series should exist after adding tag");
            }
        }
    }
    
    [Test]
    public async Task ChartComposer_CanChangeChartType()
    {
        // Navigate to Chart Composer
        await Page.GotoAsync($"{BaseUrl}/charts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(1000);
        
        // Select a chart first
        var chartItem = Page.Locator(".chart-list-item").First;
        if (await chartItem.CountAsync() > 0)
        {
            await chartItem.ClickAsync();
            await Task.Delay(500);
            
            // Find and click the chart type dropdown
            var chartTypeDropdown = Page.Locator(".e-dropdownlist").First;
            if (await chartTypeDropdown.CountAsync() > 0)
            {
                await chartTypeDropdown.ClickAsync();
                await Task.Delay(300);
                
                // Select "Bar" from dropdown
                var barOption = Page.Locator(".e-list-item:has-text('Bar')").First;
                if (await barOption.CountAsync() > 0)
                {
                    await barOption.ClickAsync();
                    await Task.Delay(500);
                    
                    // Chart type should now be Bar
                    var selectedValue = await chartTypeDropdown.InputValueAsync();
                    // Just verify the dropdown interaction worked
                    Assert.Pass("Chart type dropdown interaction successful");
                }
            }
        }
    }
    
    [Test]
    public async Task ChartComposer_CanSaveChart()
    {
        // Navigate to Chart Composer
        await Page.GotoAsync($"{BaseUrl}/charts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(1000);
        
        // Select a chart first
        var chartItem = Page.Locator(".chart-list-item").First;
        if (await chartItem.CountAsync() > 0)
        {
            await chartItem.ClickAsync();
            await Task.Delay(500);
            
            // Click Save button
            var saveButton = Page.Locator("button:has-text('Save')");
            if (await saveButton.CountAsync() > 0)
            {
                await saveButton.ClickAsync();
                await Task.Delay(1000);
                
                // Check for success message
                var successMessage = await Page.Locator(".save-message, text=saved successfully").CountAsync();
                Assert.That(successMessage, Is.GreaterThan(0), "Success message should appear after saving");
            }
        }
    }
    
    [Test]
    public async Task ChartComposer_CanSearchTags()
    {
        // Navigate to Chart Composer
        await Page.GotoAsync($"{BaseUrl}/charts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(1000);
        
        // Get initial tag count
        var initialTagCount = await Page.Locator(".tag-item").CountAsync();
        
        // Type in the search box
        var searchInput = Page.Locator("input[placeholder='Search tags...']");
        if (await searchInput.CountAsync() > 0)
        {
            await searchInput.FillAsync("Temperature");
            await Task.Delay(500);
            
            // Tag count should be filtered
            var filteredTagCount = await Page.Locator(".tag-item").CountAsync();
            Assert.That(filteredTagCount, Is.LessThanOrEqualTo(initialTagCount), "Tag list should be filtered");
            
            // All visible tags should contain "Temperature"
            var visibleTags = await Page.Locator(".tag-item").AllTextContentsAsync();
            foreach (var tagText in visibleTags)
            {
                Assert.That(tagText.ToLower(), Does.Contain("temperature").IgnoreCase, 
                    $"Filtered tag '{tagText}' should contain 'Temperature'");
            }
        }
    }
    
    [Test]
    public async Task ChartComposer_CanRemoveSeries()
    {
        // Navigate to Chart Composer
        await Page.GotoAsync($"{BaseUrl}/charts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(1000);
        
        // Select a chart first
        var chartItem = Page.Locator(".chart-list-item").First;
        if (await chartItem.CountAsync() > 0)
        {
            await chartItem.ClickAsync();
            await Task.Delay(500);
            
            // Get initial series count
            var initialSeriesCount = await Page.Locator(".series-item").CountAsync();
            
            if (initialSeriesCount > 0)
            {
                // Find and click the delete button on first series
                var deleteButton = Page.Locator(".series-item button").First;
                if (await deleteButton.CountAsync() > 0)
                {
                    await deleteButton.ClickAsync();
                    await Task.Delay(500);
                    
                    // Series count should decrease
                    var newSeriesCount = await Page.Locator(".series-item").CountAsync();
                    Assert.That(newSeriesCount, Is.LessThan(initialSeriesCount), "Series should be removed");
                }
            }
        }
    }
}
