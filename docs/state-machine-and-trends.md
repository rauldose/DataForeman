# State Machine Builder and Trend Visualizer

## Overview

This document describes two new features added to the DataForeman application:
1. **State Machine Builder** - A visual tool for creating and managing state machines
2. **Trend Visualizer** - A component for visualizing time-series data trends

## State Machine Builder

### Purpose
The State Machine Builder allows users to model system behavior as a collection of states and transitions. This is useful for:
- Process automation and workflow management
- Equipment state tracking (e.g., running, stopped, maintenance)
- Alarm state management
- Sequential operation control

### Flow Node: State Machine

The **State Machine** flow node (`state-machine`) provides runtime state machine execution within Flow Studio:

- **Stateful execution**: Maintains current state across scan cycles via runtime state storage
- **Event-driven transitions**: Processes input events to move between defined states
- **Reset capability**: Dedicated reset input to return to initial state
- **Transition reporting**: Outputs both the current state and transition details for downstream processing

#### Quick Start
1. Add a **State Machine** node to your flow
2. Set the **Initial State** (e.g., `idle`)
3. Define **Transitions** (e.g., `idle:start->running,running:stop->idle`)
4. Connect an event source to the **Event** input
5. Connect the **Current State** output to downstream nodes

### Features
- **Visual State Design**: Define states with custom names, descriptions, and colors
- **Transition Management**: Create transitions between states with events and conditions
- **Enable/Disable Control**: Activate or deactivate state machines as needed
- **Initial and Final States**: Mark states as entry or exit points

### How to Use

#### Creating a State Machine
1. Navigate to **State Machines** in the top menu
2. Click **New State Machine**
3. Enter a name and description
4. Drag states from the palette to the canvas
5. Connect states with transitions
6. Save your state machine

#### State Configuration
Each state can have:
- **Name**: Descriptive identifier for the state
- **Description**: Optional detailed information
- **Color**: Visual indicator for the state
- **Position**: Location on the canvas
- **Initial Flag**: Marks this as the starting state
- **Final Flag**: Marks this as a terminal state
- **Metadata**: Additional custom properties

#### Transitions
Transitions connect states and have:
- **Event**: The trigger that causes the transition
- **Condition**: Optional expression that must be true
- **Action**: Optional code to execute during transition
- **Priority**: Order for evaluating multiple transitions

### Technical Details

#### Models
- **StateMachineConfig**: Main configuration object
- **MachineState**: Individual state definition
- **StateTransition**: Transition between states

#### Files
- `src/DataForeman.Shared/Models/StateMachineConfig.cs` - Data models (legacy .NET)
- `src/DataForeman.App/Components/Pages/StateMachines.razor` - UI page (legacy .NET)
- `src/DataForeman.Engine/Services/StateMachineExecutionService.cs` - Runtime execution (legacy .NET)
- `src/DataForeman.App/config/state-machines.json` - Configuration storage
- `core/src/nodes/logic/StateMachineNode.js` - Flow node implementation (Node.js)
- `core/test/nodes/logic/StateMachineNode.test.js` - Node tests

## Trend Visualizer

### Purpose
The Trend Visualizer provides a way to view and analyze time-series data from tags over various time periods. This is useful for:
- Historical data analysis
- Pattern recognition
- Performance monitoring
- Troubleshooting issues

### Flow Node: Timeline

The **Timeline** flow node (`timeline`) provides runtime value buffering and aggregation within Flow Studio:

- **Rolling buffer**: Maintains a configurable-size window of recent values
- **Time and count windowing**: Prune entries by age, count, or both
- **8 aggregation modes**: last, first, avg, min, max, sum, count, range
- **Full history output**: Second output provides the complete buffer for charting

#### Quick Start
1. Add a **Timeline** node to your flow
2. Set **Max Entries** (e.g., `100`) and optionally a **Time Window**
3. Choose an **Aggregation** mode (e.g., `avg` for rolling average)
4. Connect a data source to the **Value** input
5. Use the **Aggregated** output for processed values or **Buffer** output for chart data

### Features
- **Multiple Series**: Display multiple data series on one chart
- **Time Range Selection**: Choose from predefined ranges or custom periods
- **Chart Types**: Line, Area, Column, and Scatter visualizations
- **Auto-Refresh**: Continuously update data at configurable intervals
- **Interactive Controls**: Zoom, pan, and legend toggling
- **Color Customization**: Assign custom colors to each series

### How to Use

#### Creating a Trend
1. Navigate to **Trends** in the top menu
2. Click **New Trend**
3. Enter a name and description
4. Add data series:
   - Click **Add Series**
   - Enter series name
   - Specify tag path
   - Choose visualization type
   - Select color
5. Configure display options
6. Save your trend

#### Time Ranges
Available time ranges:
- Last 15 Minutes
- Last 1 Hour
- Last 4 Hours
- Last 12 Hours
- Last 24 Hours
- Last 7 Days
- Last 30 Days
- Custom (specify start and end dates)

#### Series Types
- **Line**: Continuous line connecting data points
- **Area**: Filled area below the line
- **Column**: Vertical bars for each data point
- **Scatter**: Individual data points without connections

#### Display Options
- **Show Legend**: Display/hide the series legend
- **Show Grid**: Display/hide the chart grid lines
- **Enable Zoom**: Allow zooming into the chart
- **Enable Pan**: Allow panning across the chart
- **Refresh Interval**: Auto-update frequency in seconds

### Technical Details

#### Models
- **TrendConfig**: Main trend configuration
- **TrendSeries**: Individual data series definition
- **TrendTimeRange**: Time period configuration
- **TrendDisplayOptions**: Visualization settings

#### Enums
- **TrendSeriesType**: Line, Area, Column, Scatter
- **TrendTimeRangeType**: Predefined time periods

#### Files
- `src/DataForeman.Shared/Models/TrendConfig.cs` - Data models (legacy .NET)
- `src/DataForeman.App/Components/Pages/Trends.razor` - UI page (legacy .NET)
- `src/DataForeman.App/config/trends.json` - Configuration storage
- `core/src/nodes/data/TimelineNode.js` - Flow node implementation (Node.js)
- `core/test/nodes/data/TimelineNode.test.js` - Node tests

## Integration

### ConfigService
The `ConfigService` has been extended to support both features:
- `LoadStateMachinesAsync()` / `SaveStateMachinesAsync()`
- `LoadTrendsAsync()` / `SaveTrendsAsync()`
- CRUD operations for both state machines and trends

### Navigation
Both features are accessible from the main navigation bar:
- **State Machines** - Access the State Machine Builder
- **Trends** - Access the Trend Visualizer

### Storage
Configurations are stored as JSON files:
- `config/state-machines.json` - State machine definitions
- `config/trends.json` - Trend configurations

## Future Enhancements

### State Machine Builder
- [ ] Visual state diagram editor with drag-and-drop
- [ ] Real-time state execution monitoring
- [ ] Condition expression builder
- [ ] Action script editor
- [ ] State machine templates
- [ ] Import/export functionality
- [x] State tracking via StateMachineNode in Flow Studio
- [x] Event-driven transitions with runtime state persistence

### Trend Visualizer
- [ ] Real-time data integration with HistoryService
- [ ] Multiple Y-axes support
- [x] Data aggregation options (min, max, avg) via TimelineNode
- [ ] Export to CSV/Excel
- [ ] Comparison overlays
- [ ] Annotation support
- [ ] Alert threshold lines
- [ ] Statistical analysis tools
- [x] Rolling buffer with time/count windowing via TimelineNode

## Example Use Cases

### State Machine: Equipment Status
```
States:
- Stopped (Initial)
- Starting
- Running
- Stopping
- Maintenance (Final)

Transitions:
- Stopped → Starting (Event: "start_command")
- Starting → Running (Event: "ready")
- Running → Stopping (Event: "stop_command")
- Stopping → Stopped (Event: "stopped")
- Any → Maintenance (Event: "maintenance_required")
```

### Trend: Temperature Monitoring
```
Series:
- Tank A Temperature (Red, Line)
- Tank B Temperature (Blue, Line)
- Ambient Temperature (Green, Area)

Time Range: Last 24 Hours
Refresh: 5 seconds
```

## Notes

- State machines must be enabled to execute in the Engine
- Trends currently generate sample data for demonstration
- Full integration with DataForeman's tag system is in progress
- Both features follow the existing DataForeman architecture and patterns
