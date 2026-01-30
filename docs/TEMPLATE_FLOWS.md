# Template Flows Documentation

## Overview

Template flows are reusable flow configurations that can be instantiated and used as nodes in other flows. This feature enables modularity, reusability, and consistency across complex industrial automation workflows.

## Key Concepts

### What is a Template Flow?

A template flow is a regular flow that has been marked with the `IsTemplate` flag. Once marked as a template, it can be:
- **Discovered** in the node palette under the "TEMPLATES" category
- **Instantiated** as a node in other flows
- **Parameterized** with configurable values
- **Reused** across multiple flows without duplication

### Benefits

✅ **Reusability** - Design once, use many times  
✅ **Maintainability** - Update template, all instances benefit  
✅ **Consistency** - Standardized operations across flows  
✅ **Modularity** - Complex flows built from simple templates  
✅ **Parameterization** - Customize each instance with parameters  

## Creating a Template Flow

### Step 1: Design Your Flow

Create a regular flow with the desired logic:

```json
{
  "nodes": [
    {
      "id": "input_value",
      "type": "tag-input",
      "label": "Value Input",
      "config": { "tagId": 1 }
    },
    {
      "id": "high_check",
      "type": "compare",
      "label": "Check High Threshold",
      "config": { "operation": ">", "threshold": 75.0 }
    },
    {
      "id": "alert_logic",
      "type": "csharp",
      "label": "Alert Generator",
      "config": {
        "code": "var high = input.GetBool(\"highAlert\"); return new { alert = high, message = high ? \"High!\" : \"Normal\" };"
      }
    }
  ],
  "edges": [
    { "id": "e1", "source": "input_value", "target": "high_check" },
    { "id": "e2", "source": "high_check", "target": "alert_logic" }
  ]
}
```

### Step 2: Mark as Template

Use the API to mark your flow as a template:

```http
POST /api/flows/{flowId}/mark-template
Content-Type: application/json

{
  "isTemplate": true,
  "templateInputs": "[\"value\"]",
  "templateOutputs": "[\"alert\", \"level\", \"message\"]",
  "exposedParameters": "[{\"name\":\"highThreshold\",\"type\":\"double\",\"default\":75.0,\"description\":\"High threshold limit\"},{\"name\":\"lowThreshold\",\"type\":\"double\",\"default\":30.0,\"description\":\"Low threshold limit\"}]"
}
```

### Step 3: Define Template Metadata

**Template Inputs**: Array of input parameter names that the template expects
```json
["value", "temperature", "pressure"]
```

**Template Outputs**: Array of output values the template produces
```json
["alert", "level", "message", "value"]
```

**Exposed Parameters**: Array of configurable parameters
```json
[
  {
    "name": "highThreshold",
    "type": "double",
    "default": 75.0,
    "description": "High threshold limit"
  },
  {
    "name": "lowThreshold",
    "type": "double",
    "default": 30.0,
    "description": "Low threshold limit"
  }
]
```

## Using Template Flows

### Discovering Templates

Templates appear in the node palette under the "TEMPLATES" category. Each template is represented as a node type with:

```http
GET /api/flows/node-types
```

Response includes:
```json
{
  "nodeTypes": [
    {
      "type": "template-{templateId}",
      "displayName": "Threshold Monitor Template",
      "category": "TEMPLATES",
      "section": "CUSTOM",
      "icon": "📦",
      "color": "#00bcd4",
      "templateId": "10000000-0000-0000-0000-000000000004",
      "description": "Reusable template for monitoring a value against thresholds",
      "inputs": "[\"value\"]",
      "outputs": "[\"alert\", \"level\", \"message\"]",
      "parameters": "[{\"name\":\"highThreshold\",\"type\":\"double\",\"default\":75.0}]"
    }
  ]
}
```

### Using a Template as a Node

Add a template node to your flow:

```json
{
  "id": "monitor_tank1",
  "type": "template-flow",
  "label": "Tank 1 Monitor",
  "position": { "x": 300, "y": 150 },
  "config": {
    "templateFlowId": "10000000-0000-0000-0000-000000000004",
    "parameters": {
      "highThreshold": 80.0,
      "lowThreshold": 25.0
    }
  }
}
```

### Creating Flow from Template

Create a new flow based on a template:

```http
POST /api/flows/from-template
Content-Type: application/json

{
  "templateFlowId": "10000000-0000-0000-0000-000000000004",
  "name": "Tank 1 Temperature Monitor",
  "description": "Monitor tank 1 temperature with custom thresholds",
  "parameters": "{\"highThreshold\":80.0,\"lowThreshold\":25.0}"
}
```

## API Reference

### List Templates

```http
GET /api/flows/templates?limit=50
```

**Response**:
```json
{
  "templates": [
    {
      "id": "uuid",
      "name": "Threshold Monitor Template",
      "description": "Reusable threshold monitoring logic",
      "exposedParameters": "[...]",
      "templateInputs": "[\"value\"]",
      "templateOutputs": "[\"alert\", \"level\", \"message\"]",
      "createdAt": "2024-01-01T00:00:00Z",
      "updatedAt": "2024-01-01T00:00:00Z",
      "isOwner": true
    }
  ],
  "count": 1
}
```

### Mark Flow as Template

```http
POST /api/flows/{flowId}/mark-template
Content-Type: application/json

{
  "isTemplate": true,
  "templateInputs": "[\"input1\", \"input2\"]",
  "templateOutputs": "[\"output1\", \"output2\"]",
  "exposedParameters": "[{\"name\":\"param1\",\"type\":\"double\",\"default\":0}]"
}
```

**Response**:
```json
{
  "ok": true,
  "isTemplate": true
}
```

### Create from Template

```http
POST /api/flows/from-template
Content-Type: application/json

{
  "templateFlowId": "uuid",
  "name": "My Custom Flow",
  "description": "Flow based on template",
  "parameters": "{\"param1\":100}",
  "folderId": "uuid"
}
```

**Response**:
```json
{
  "id": "new-flow-uuid",
  "templateId": "template-uuid"
}
```

## Seeded Template Example

### "Threshold Monitor Template"

A production-ready template for monitoring values against configurable thresholds.

**Description**: Monitors a single value against high and low thresholds, generates alerts with customizable limits.

**Inputs**: 
- `value` - The value to monitor (e.g., temperature, pressure, level)

**Outputs**:
- `alert` (boolean) - Whether an alert condition exists
- `level` (string) - Alert level: "high", "low", or "normal"
- `message` (string) - Human-readable alert message
- `value` (double) - The input value passed through

**Parameters**:
- `highThreshold` (double, default: 75.0) - High threshold limit
- `lowThreshold` (double, default: 30.0) - Low threshold limit

**Flow Structure**:
```
Value Input → High Check (>75) ┐
            ↓                   ├→ Alert Logic (C#) → Output
            → Low Check (<30)  ┘
```

**Use Cases**:
- Temperature monitoring for tanks
- Pressure monitoring for vessels
- Level monitoring for silos
- Speed monitoring for motors
- Any threshold-based alerting

## Database Schema

### Flow Entity Additions

```csharp
public class Flow
{
    // ... existing properties ...
    
    public bool IsTemplate { get; set; } = false;
    public Guid? TemplateFlowId { get; set; }
    public string TemplateInputs { get; set; } = "[]";
    public string TemplateOutputs { get; set; } = "[]";
    
    // Navigation
    public virtual Flow? TemplateFlow { get; set; }
    public virtual ICollection<Flow> InstantiatedFlows { get; set; } = new List<Flow>();
}
```

### Relationships

```
Flow (Template)
 ├─ InstantiatedFlows[] ──→ Flow (Instances)
 │   └─ TemplateFlowId ───→ Template Flow
 └─ IsTemplate = true
```

## Implementation Status

### ✅ Completed

- **Database Schema**: Template fields added to Flow entity
- **API Endpoints**: 
  - `GET /api/flows/templates` - List templates
  - `POST /api/flows/{id}/mark-template` - Mark as template
  - `POST /api/flows/from-template` - Create from template
  - `GET /api/flows/node-types` - Includes templates
- **Template Flow Executor**: Basic executor structure created
- **Seeded Example**: "Threshold Monitor Template" added to seed data
- **Documentation**: Complete usage guide

### 🚧 Pending (Future Enhancements)

- **Template Execution**: Full sub-flow execution within TemplateFlowExecutor
- **Parameter Mapping**: Runtime parameter substitution in template nodes
- **Input/Output Mapping**: Connect parent flow values to template inputs
- **Versioning**: Template version control and migration
- **UI Components**: Visual template editor and marketplace

## Best Practices

### Designing Templates

1. **Keep it focused**: Each template should solve one specific problem
2. **Use parameters**: Make thresholds and limits configurable
3. **Document well**: Clear descriptions and parameter documentation
4. **Test thoroughly**: Validate with various parameter values
5. **Name clearly**: Use descriptive names (e.g., "PID Controller Template", not "Template1")

### Parameter Design

- Use meaningful parameter names
- Provide sensible defaults
- Include descriptions for each parameter
- Choose appropriate data types
- Consider validation ranges

### Input/Output Design

- Clearly define expected inputs
- Document output structure
- Use consistent naming conventions
- Consider optional vs required inputs

## Examples

### Example 1: Alert System Template

```json
{
  "name": "Multi-Level Alert Template",
  "description": "Configurable alert system with warning and critical levels",
  "exposedParameters": [
    {"name": "warningThreshold", "type": "double", "default": 70},
    {"name": "criticalThreshold", "type": "double", "default": 90},
    {"name": "hysteresis", "type": "double", "default": 5}
  ],
  "templateInputs": ["value"],
  "templateOutputs": ["alertLevel", "message", "acknowledged"]
}
```

### Example 2: PID Controller Template

```json
{
  "name": "PID Controller Template",
  "description": "Proportional-Integral-Derivative controller",
  "exposedParameters": [
    {"name": "kp", "type": "double", "default": 1.0, "description": "Proportional gain"},
    {"name": "ki", "type": "double", "default": 0.1, "description": "Integral gain"},
    {"name": "kd", "type": "double", "default": 0.05, "description": "Derivative gain"},
    {"name": "setpoint", "type": "double", "default": 50, "description": "Target value"}
  ],
  "templateInputs": ["processVariable"],
  "templateOutputs": ["controlOutput", "error", "integral", "derivative"]
}
```

### Example 3: Data Filter Template

```json
{
  "name": "Moving Average Filter Template",
  "description": "Smooths noisy data using moving average",
  "exposedParameters": [
    {"name": "windowSize", "type": "integer", "default": 10, "description": "Number of samples to average"},
    {"name": "outlierThreshold", "type": "double", "default": 3.0, "description": "Standard deviations for outlier detection"}
  ],
  "templateInputs": ["rawValue"],
  "templateOutputs": ["filteredValue", "outlierDetected"]
}
```

## Migration Guide

### Converting Existing Flows to Templates

1. **Review the flow** - Ensure it's generic and reusable
2. **Identify parameters** - Find hardcoded values to parameterize
3. **Define inputs/outputs** - Document expected data flow
4. **Update code** - Replace hardcoded values with parameter references
5. **Mark as template** - Use the API endpoint
6. **Test** - Create instances with various parameters
7. **Document** - Add clear description and usage examples

### Using Templates in Existing Projects

1. **List available templates** - `GET /api/flows/templates`
2. **Review template parameters** - Check inputs, outputs, parameters
3. **Create instance** - `POST /api/flows/from-template`
4. **Configure parameters** - Set custom values
5. **Test instance** - Verify with actual data
6. **Deploy** - Use in production workflows

## Troubleshooting

### Template not appearing in node palette

- Verify `IsTemplate` is set to true
- Check `Shared` is true (if accessing from different user)
- Refresh node types: `GET /api/flows/node-types`

### Parameter not working

- Verify parameter is in `ExposedParameters` JSON
- Check parameter name spelling matches exactly
- Ensure parameter type is correct (double, string, bool, etc.)

### Template execution fails

- Check template flow definition is valid JSON
- Verify all nodes in template have valid types
- Ensure template has no circular dependencies
- Check logs for detailed error messages

## Future Enhancements

- **Template Marketplace**: Share and discover community templates
- **Version Control**: Track template changes and manage versions
- **Visual Editor**: Drag-drop template designer with parameter UI
- **Execution Optimization**: Cache compiled templates
- **Testing Framework**: Automated template validation
- **Documentation Generator**: Auto-generate docs from templates
- **Template Analytics**: Track usage and performance metrics

---

**Status**: Core infrastructure complete. Template execution implementation pending for full functionality.
