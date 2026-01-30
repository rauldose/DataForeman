# Scatter Charts - Quick Start Guide

## What Are Scatter Charts?

Scatter charts show the **relationship between two variables** by plotting data points as markers on an X-Y coordinate system.

### Key Concept

**Time-Series Charts** (Line/Area):
```
Time → Value
(How does temperature change over time?)
```

**Scatter Charts**:
```
Variable X → Variable Y  
(How does pressure relate to temperature?)
```

## When to Use Scatter Charts

✅ **Use Scatter Charts When**:
- Finding correlations between two variables
- Identifying cause-and-effect relationships
- Detecting outliers or anomalies
- Quality control analysis
- Optimizing process parameters

❌ **Don't Use Scatter Charts When**:
- Viewing trends over time (use Line chart)
- Comparing categories (use Bar chart)
- Showing composition (use Pie chart)

## How to Create a Scatter Chart

### Step 1: Select Chart Type
```
Chart Type: [Scatter ▼]
```

### Step 2: Choose X-Axis Variable
```
X-Axis Tag: [Tank 1 Temperature ▼]
```
This becomes your independent variable (horizontal axis)

### Step 3: Add Y-Axis Variable(s)
```
Series 1:
  Y-Axis Tag: [Tank 1 Pressure ▼]
  Color: Blue
  Marker: Circle, Size 6
```
This becomes your dependent variable (vertical axis)

### Step 4: Customize Appearance
- **Marker Shape**: Circle, Diamond, Triangle, Square, Cross, Plus
- **Marker Size**: 3-12 pixels
- **Color**: Choose from palette
- **Multiple Series**: Add more Y variables to compare

### Step 5: View Results
The chart plots each data point where both X and Y values exist, showing their correlation.

## Real-World Examples

### Example 1: Temperature vs Pressure
**Question**: Does temperature affect pressure?

**Setup**:
- X-Axis: Tank 1 Temperature
- Y-Axis: Tank 1 Pressure

**Expected Result**: Positive correlation
```
Pressure
  │      ●●●
  │    ●●●
  │  ●●●
  └────────── Temperature
```
Higher temperature → Higher pressure

### Example 2: Motor Speed vs Flow Rate
**Question**: How does motor speed affect flow?

**Setup**:
- X-Axis: Motor 1 Speed (RPM)
- Y-Axis: Flow Rate (L/min)

**Expected Result**: Linear relationship
```
Flow
  │        ●
  │      ●
  │    ●
  │  ●
  └────────── Speed
```
Faster motor → Higher flow

### Example 3: Production vs Quality
**Question**: What's the optimal production rate?

**Setup**:
- X-Axis: Production Rate (units/hr)
- Y-Axis: Quality Index (%)

**Expected Result**: Optimal range
```
Quality
100%│     ●●●●
    │   ●●●●●●●
 90%│ ●●●     ●●
    └────────────── Production Rate
      50  80  100
```
Sweet spot: 70-90 units/hr

### Example 4: Multi-Variable Comparison
**Question**: How does temperature affect multiple outputs?

**Setup**:
- X-Axis: Temperature
- Y-Series 1: Pressure (blue)
- Y-Series 2: Level (red)

**Expected Result**: Different correlations
```
Value
  │   ● Blue (Pressure - positive)
  │  ●●
  │ ●  ○ Red (Level - negative)
  │   ○○
  └────────── Temperature
```

## Understanding the Results

### Positive Correlation
Points trend upward: As X increases, Y increases
```
  │       ●
  │     ●
  │   ●
  └─────
```
**Example**: Speed ↑ → Flow ↑

### Negative Correlation
Points trend downward: As X increases, Y decreases
```
  │ ●
  │   ●
  │     ●
  └─────
```
**Example**: Temperature ↑ → Viscosity ↓

### No Correlation
Points scattered randomly: X and Y are independent
```
  │  ● ●
  │ ● ●●
  │ ●  ●
  └─────
```
**Example**: Day of week vs temperature

### Outliers
Points far from the main cluster: Anomalies or errors
```
  │      ●
  │   ●●●●  ← Main cluster
  │  ●●●●
  │
  │ ●        ← Outlier
  └─────
```
**Action**: Investigate outlier causes

## Tips for Better Scatter Charts

### 1. Choose Related Variables
✅ Good: Temperature vs Pressure (physically related)
❌ Bad: Tank Level vs Day of Week (unrelated)

### 2. Use Appropriate Time Range
- **Too short**: Not enough points to see pattern
- **Too long**: May include different operating modes
- **Recommended**: 1 day to 1 week of stable operation

### 3. Consider Sampling Rates
- X and Y tags should have similar sampling rates
- System automatically matches timestamps (±5 seconds)
- Different rates may result in fewer data points

### 4. Marker Size Matters
- **Small (3-4px)**: Good for many points (>500)
- **Medium (6-8px)**: General purpose (100-500 points)
- **Large (10-12px)**: Good for few points (<100)

### 5. Multiple Series
Add multiple Y series to compare relationships:
```
X: Temperature
Y1: Pressure (blue circles)
Y2: Flow (red triangles)
Y3: Level (green diamonds)
```

## Common Issues

### Issue: No Data Points
**Cause**: No matching timestamps between X and Y tags
**Solution**: Check that both tags have data in selected time range

### Issue: Very Few Points
**Cause**: Different sampling rates or missing data
**Solution**: Increase time range or use tags with similar rates

### Issue: Vertical Line of Points
**Cause**: X variable not changing (constant)
**Solution**: Choose X variable that varies in your data

### Issue: Horizontal Line of Points
**Cause**: Y variable not changing (constant)
**Solution**: Choose Y variable that varies in your data

## Advanced Features

### Marker Shapes
Choose shapes to distinguish series:
- ● Circle - Default, clean look
- ◆ Diamond - Angular data
- ▲ Triangle - Directional indicators
- ■ Square - Solid, distinct
- ✕ Cross - Sparse data
- + Plus - Overlapping series

### Color Coding
Use colors strategically:
- **Blue**: Cool processes (temperature, cooling)
- **Red**: Hot processes (heating, alarms)
- **Green**: Normal operations (efficiency, quality)
- **Orange**: Warnings (approach limits)

### Time Range Selection
- **1 hour**: Real-time correlation check
- **1 day**: Daily pattern analysis
- **1 week**: Weekly trend correlation
- **Custom**: Specific event analysis

## Next Steps

1. **Try It**: Create your first scatter chart
2. **Experiment**: Try different X-Y combinations
3. **Analyze**: Look for correlations in your data
4. **Optimize**: Find optimal operating points
5. **Monitor**: Set up alerts for outliers

## Additional Resources

- **SCATTER_CHARTS.md** - Detailed technical documentation
- **CHART_IMPLEMENTATION.md** - API integration guide
- **FLOW_EXAMPLES.md** - Use scatter data in flows

---

**Quick Reference Card**

| Task | Action |
|------|--------|
| Create scatter chart | Chart Type → Scatter |
| Set X variable | X-Axis Tag → Select tag |
| Add Y variable | Add Y Series → Select tag |
| Change marker | Marker Shape dropdown |
| Adjust size | Marker Size slider |
| Add comparison | + Add Another Y Series |
| Find outliers | Look for distant points |
| Check correlation | Observe point trend |

**Remember**: Scatter shows relationships, not trends over time!
