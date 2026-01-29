# Flow Examples Documentation

This document describes the working flow examples seeded in the DataForeman database.

## Seeded Flows

### 1. Temperature Alert System

**Purpose**: Monitor tank temperatures, calculate average, and generate alerts when out of normal range.

**Description**: This flow demonstrates a complete monitoring system with multiple inputs, calculations, and conditional logic. It's a practical example for industrial temperature monitoring applications.

**Flow Logic**:

```
Tank 1 Temp ──────┬─────> High Temp Alert (>75°C) ───┐
                  │                                    │
                  ├─────> Calculate Average ─────────>│
                  │              ▲                     │
Tank 2 Temp ──────┼──────────────┘                    ├─> Alert Logic (JavaScript)
                  │                                    │
                  └─────> Low Temp Alert (<30°C) ─────┘
```

**Nodes**:
- **Tag Input (Tank 1 Temp)**: Reads temperature from Simulator/Tank1/Temperature
- **Tag Input (Tank 2 Temp)**: Reads temperature from Simulator/Tank2/Temperature
- **Math (Calculate Average)**: Computes average of both tank temperatures
- **Comparison (High Temp Alert)**: Checks if Tank 1 temp > 75°C
- **Comparison (Low Temp Alert)**: Checks if Tank 2 temp < 30°C
- **JavaScript (Alert Logic)**: Determines alert status and generates message

**JavaScript Node Code**:
```javascript
// Determine alert status
const highAlert = $input.highTemp || false;
const lowAlert = $input.lowTemp || false;
const avgTemp = $input.average || 0;

if (highAlert) {
  return { 
    alert: true, 
    level: 'high', 
    message: 'Temperature too high!', 
    value: avgTemp 
  };
} else if (lowAlert) {
  return { 
    alert: true, 
    level: 'low', 
    message: 'Temperature too low!', 
    value: avgTemp 
  };
} else {
  return { 
    alert: false, 
    level: 'normal', 
    message: 'Temperature normal', 
    value: avgTemp 
  };
}
```

**Execution Settings**:
- Mode: Continuous
- Scan Rate: 2000ms (2 seconds)
- Deployed: No (ready to deploy)

**Use Cases**:
- Tank temperature monitoring
- Alert generation for out-of-range conditions
- Average temperature tracking
- Safety system integration

---

### 2. Production Efficiency Calculator

**Purpose**: Calculate production efficiency as units produced per kilowatt of power consumed.

**Description**: This flow demonstrates a Key Performance Indicator (KPI) calculation commonly used in manufacturing to measure energy efficiency.

**Flow Logic**:

```
Production Rate ──────┐
                      ├─> Efficiency (Divide) ─> Format Result (JavaScript)
Motor Power ──────────┘
```

**Nodes**:
- **Tag Input (Production Rate)**: Reads from Simulator/Process/Production_Rate (units/hour)
- **Tag Input (Motor Power)**: Reads from Simulator/Motor1/Power (kW)
- **Math (Efficiency)**: Divides production rate by power (units/kW)
- **JavaScript (Format Result)**: Formats efficiency and calculates percentage rating

**JavaScript Node Code**:
```javascript
// Calculate efficiency percentage
const efficiency = ($input.value || 0);
const percentage = Math.min(100, Math.max(0, efficiency * 10));

return {
  efficiency: efficiency.toFixed(2),
  percentage: percentage.toFixed(1),
  rating: percentage > 80 ? 'Excellent' : percentage > 60 ? 'Good' : 'Poor'
};
```

**Execution Settings**:
- Mode: Continuous
- Scan Rate: 5000ms (5 seconds)
- Deployed: No (ready to deploy)

**Use Cases**:
- Energy efficiency monitoring
- Production KPI tracking
- Cost analysis
- Performance benchmarking

---

### 3. Simple Math Example

**Purpose**: Basic example demonstrating tag reading and math operations.

**Description**: A simple flow that reads two temperature tags and calculates their sum. Perfect for learning the basics of flow creation.

**Flow Logic**:

```
Tag 1 (Temperature) ──────┐
                          ├─> Add ─> Result
Tag 2 (Pressure) ─────────┘
```

**Nodes**:
- **Tag Input (Tag 1)**: Reads from Simulator/Tank1/Temperature
- **Tag Input (Tag 2)**: Reads from Simulator/Tank1/Pressure
- **Math (Add)**: Adds the two values together

**Execution Settings**:
- Mode: Continuous
- Scan Rate: 1000ms (1 second)
- Deployed: No (ready to deploy)

**Use Cases**:
- Learning flow basics
- Testing tag connections
- Simple calculations
- Template for more complex flows

---

## How to Use These Flows

### Viewing Flows

1. Navigate to **Flow Studio** from the main menu
2. You'll see a list of all available flows
3. Click on any flow to open it in the editor

### Testing a Flow

1. Open the flow in the editor
2. Click **"Test Run"** button
3. Configure test options:
   - **Disable writes**: Prevents writing to tags (safe for testing)
   - **Auto-exit**: Automatically stops test after timeout
4. Click **"Start Test"** to begin
5. Watch the nodes update with live values
6. Click **"Stop Test"** when finished

### Deploying a Flow

1. Open the flow in the editor
2. Ensure the flow logic is correct
3. Click **"Deploy"** button
4. The flow will start running continuously at the configured scan rate
5. Click **"Stop"** to undeploy

### Modifying a Flow

1. Open the flow in the editor
2. Drag nodes from the palette on the left
3. Connect nodes by dragging from output (right side) to input (left side)
4. Click on nodes to configure their settings
5. Save changes with the **"Save"** button
6. Test before deploying to production

---

## Flow Development Best Practices

### Node Naming
- Use descriptive labels for nodes
- Include units in labels (e.g., "Tank 1 Temp (°C)")
- Group related nodes visually

### Error Handling
- Set appropriate **Maximum Data Age** on Tag Input nodes
- Handle null values in JavaScript nodes
- Test with various input conditions

### Performance
- Use appropriate scan rates (don't scan faster than needed)
- Minimize complex JavaScript operations
- Consider data aggregation for high-frequency tags

### Testing
- Always test flows before deploying
- Use **Disable writes** during initial testing
- Verify all node connections and configurations
- Test edge cases (null values, out-of-range data)

---

## Technical Details

### Flow Definition Format

Flows are stored in JSON format with this structure:

```json
{
  "nodes": [
    {
      "id": "unique_node_id",
      "type": "tag-input | math | comparison | javascript",
      "label": "Human-readable label",
      "position": { "x": 100, "y": 100 },
      "config": {
        // Node-specific configuration
      }
    }
  ],
  "edges": [
    {
      "id": "unique_edge_id",
      "source": "source_node_id",
      "target": "target_node_id",
      "targetHandle": "input_port_name"
    }
  ]
}
```

### Execution Order

Nodes execute in dependency order (topological sort):
1. Nodes with no inputs execute first
2. Nodes execute when all inputs are ready
3. Output nodes execute last

### Data Flow

- Each node receives inputs through connections
- Nodes process data and produce outputs
- Outputs flow to connected downstream nodes
- Final results can be written to tags or used for alerts

---

## Extending the Examples

### Adding More Inputs

To add more temperature sensors:
1. Add new Tag Input nodes
2. Connect to the Math (Average) node
3. Update the JavaScript logic if needed

### Adding Output Tags

To write results to database:
1. Add Tag Output node
2. Connect from result node
3. Select target tag from browser
4. Deploy to start writing

### Adding Alerts

To send alerts:
1. Add Comparison nodes for thresholds
2. Use JavaScript to format alert messages
3. Connect to Tag Output for alert flags
4. Optionally integrate with external systems

---

## Related Documentation

- [Flows User Guide](flows-user-guide.md) - Complete guide to using Flow Studio
- [Flow Node Schema](flow-node-schema.md) - Technical node documentation
- [Flow Studio Implementation](flow-studio-implementation.md) - Architecture details

---

## Support

For questions or issues:
- Check the [Troubleshooting Guide](../TROUBLESHOOTING.md)
- Review the [Quick Start Guide](../QUICK-START.md)
- Visit the DataForeman repository for updates
