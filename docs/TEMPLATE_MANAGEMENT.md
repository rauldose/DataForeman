# Template Flow Management Guide

## Overview

This guide covers comprehensive template flow management including editing templates, tracking usage, and monitoring deployment status.

## Features

### 1. Template Editing

Templates can be fully edited after creation while maintaining their template status and relationships.

#### Update Template Definition

**Endpoint**: `PUT /api/flows/templates/{id}`

**Request**:
```json
{
  "name": "Updated Template Name",
  "description": "Updated description",
  "definition": "{\"nodes\":[...],\"edges\":[...]}",
  "templateInputs": "[\"input1\", \"input2\"]",
  "templateOutputs": "[\"output1\", \"output2\"]",
  "exposedParameters": "[{\"name\":\"threshold\",\"type\":\"double\",\"default\":80.0}]",
  "shared": true
}
```

**Response**:
```json
{
  "ok": true
}
```

**Features**:
- Update template definition (nodes, edges)
- Modify inputs/outputs configuration
- Change exposed parameters
- Update name, description, sharing status
- Preserves template relationships
- Logs all template changes

**Important Notes**:
- Only template owner can edit
- Changes don't automatically update instantiated flows
- Consider versioning for major changes
- Test template after significant modifications

### 2. Template Usage Tracking

See all flows that use a specific template and their deployment status.

#### Get Template Usage

**Endpoint**: `GET /api/flows/templates/{id}/usage`

**Response**:
```json
{
  "template": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "Threshold Monitor Template"
  },
  "usedBy": [
    {
      "id": "660e8400-e29b-41d4-a716-446655440001",
      "name": "Tank 1 Temperature Monitor",
      "description": "Monitors tank 1 temperature",
      "owner": "admin@example.com",
      "deployed": true,
      "deploymentStatus": "modified",
      "hasChanges": true,
      "deployedAt": "2026-01-29T10:00:00Z",
      "updatedAt": "2026-01-30T04:00:00Z",
      "createdAt": "2026-01-25T08:00:00Z"
    },
    {
      "id": "770e8400-e29b-41d4-a716-446655440002",
      "name": "Pressure Monitor",
      "description": "Monitors pressure levels",
      "owner": "operator@example.com",
      "deployed": true,
      "deploymentStatus": "up-to-date",
      "hasChanges": false,
      "deployedAt": "2026-01-28T14:00:00Z",
      "updatedAt": "2026-01-28T14:00:00Z",
      "createdAt": "2026-01-26T09:00:00Z"
    }
  ],
  "count": 2
}
```

**Deployment Status Values**:
- `"not-deployed"` - Flow has never been deployed
- `"up-to-date"` - Deployed definition matches current definition
- `"modified"` - Current definition differs from deployed version

**Use Cases**:
- **Impact Analysis**: Understand which flows will be affected by template changes
- **Dependency Tracking**: See how widely a template is used
- **Compliance**: Verify all instances are deployed correctly
- **Cleanup**: Identify unused templates for deletion

### 3. Enhanced Template Listings

Template listings now include usage count.

#### Get All Templates

**Endpoint**: `GET /api/flows/templates`

**Response**:
```json
{
  "templates": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "name": "Threshold Monitor Template",
      "description": "Monitors value against configurable thresholds",
      "exposedParameters": "[{\"name\":\"highThreshold\",\"type\":\"double\",\"default\":75.0}]",
      "templateInputs": "[\"value\"]",
      "templateOutputs": "[\"alert\",\"level\",\"message\"]",
      "createdAt": "2026-01-20T10:00:00Z",
      "updatedAt": "2026-01-29T15:00:00Z",
      "isOwner": true,
      "usedByCount": 5
    }
  ],
  "count": 1
}
```

### 4. Deployment Status & Drift Detection

Track whether deployed flows differ from their current definitions.

#### Check Deployment Status

**Endpoint**: `GET /api/flows/{id}/deployment-status`

**Response**:
```json
{
  "flowId": "660e8400-e29b-41d4-a716-446655440001",
  "name": "Tank 1 Temperature Monitor",
  "deployed": true,
  "deploymentStatus": "modified",
  "hasChanges": true,
  "deployedAt": "2026-01-29T10:00:00Z",
  "lastModified": "2026-01-30T04:00:00Z",
  "changeCount": 145,
  "isTemplate": false,
  "templateFlowId": "550e8400-e29b-41d4-a716-446655440000"
}
```

**Fields**:
- `deploymentStatus` - Current status (not-deployed, up-to-date, modified)
- `hasChanges` - Boolean indicating if definition differs
- `changeCount` - Approximate size of changes (for UI indication)
- `deployedAt` - When flow was last deployed
- `lastModified` - When definition was last modified

#### Get Deployment Differences

**Endpoint**: `GET /api/flows/{id}/deployment-diff`

**Response**:
```json
{
  "hasDiff": true,
  "deployedDefinition": "{\"nodes\":[...]}",
  "currentDefinition": "{\"nodes\":[...modified...]}",
  "deployedAt": "2026-01-29T10:00:00Z",
  "lastModified": "2026-01-30T04:00:00Z"
}
```

**Use Cases**:
- **Safety**: Review changes before redeploying
- **Debugging**: Compare working deployed version with modified version
- **Audit**: Track what changed and when
- **Rollback**: Revert to deployed version if needed

### 5. Enhanced Flow Listings

Flow listings now include deployment status information.

#### Get Flows

**Endpoint**: `GET /api/flows`

**Response** (per flow):
```json
{
  "id": "660e8400-e29b-41d4-a716-446655440001",
  "name": "Tank 1 Monitor",
  "deployed": true,
  "deploymentStatus": "modified",
  "hasChanges": true,
  "isTemplate": false,
  "templateFlowId": "550e8400-e29b-41d4-a716-446655440000",
  ...
}
```

**Visual Indicators** (UI Implementation):
- 🟢 `deployed: true, hasChanges: false` - Green, "Deployed & Up-to-date"
- 🟡 `deployed: true, hasChanges: true` - Yellow/Orange, "Deployed (Modified)"
- 🔴 `deployed: false` - Red/Gray, "Not Deployed"

## Deployment Workflow

### Safe Deployment Process

1. **Make Changes**
   - Edit flow definition in Flow Studio
   - Auto-saves to `definition` field

2. **Check Status**
   ```
   GET /api/flows/{id}/deployment-status
   ```
   - Verify `hasChanges: true`

3. **Review Differences**
   ```
   GET /api/flows/{id}/deployment-diff
   ```
   - Compare deployed vs current
   - Ensure changes are intentional

4. **Test in Test Mode**
   - Enable test mode
   - Run flow with test data
   - Verify behavior

5. **Deploy**
   ```
   POST /api/flows/{id}/deploy
   { "deploy": true }
   ```
   - Creates snapshot of definition
   - Starts flow session
   - Records deployment timestamp

6. **Verify**
   ```
   GET /api/flows/{id}/deployment-status
   ```
   - Should show `deploymentStatus: "up-to-date"`
   - `hasChanges: false`

### Rollback to Deployed Version

If current changes are problematic:

1. **Get Deployed Definition**
   ```
   GET /api/flows/{id}/deployment-diff
   ```

2. **Restore Deployed Version**
   ```
   PUT /api/flows/{id}
   {
     "definition": "<deployedDefinition from step 1>"
   }
   ```

3. **Verify**
   ```
   GET /api/flows/{id}/deployment-status
   ```
   - Should show `up-to-date`

## Template Change Management

### Best Practices

1. **Version Templates**
   - Create new template versions for breaking changes
   - Use naming: "Template v1", "Template v2"
   - Preserve old versions

2. **Communication**
   - Check usage before major changes: `GET /api/flows/templates/{id}/usage`
   - Notify users of template updates
   - Document changes in description

3. **Testing**
   - Create test instance from template
   - Verify all parameters work
   - Test edge cases

4. **Phased Rollout**
   - Update template
   - Test with one instance
   - Gradually update other instances
   - Monitor for issues

### Template Update Workflow

1. **Check Impact**
   ```
   GET /api/flows/templates/{id}/usage
   ```
   - See how many flows use template
   - Identify critical instances

2. **Update Template**
   ```
   PUT /api/flows/templates/{id}
   {
     "definition": "<updated definition>",
     "templateInputs": "[...]",
     "templateOutputs": "[...]"
   }
   ```

3. **Test with Single Instance**
   - Create test flow from updated template
   - Deploy and validate

4. **Update Instances** (if needed)
   - Recreate flows from updated template
   - Or manually apply changes

5. **Monitor**
   - Watch deployment status of instances
   - Check for errors in logs

## Database Schema

### Flow Entity Fields

```csharp
public class Flow
{
    // Template fields
    public bool IsTemplate { get; set; }
    public Guid? TemplateFlowId { get; set; }
    public string TemplateInputs { get; set; }
    public string TemplateOutputs { get; set; }
    
    // Deployment tracking
    public bool Deployed { get; set; }
    public string? DeployedDefinition { get; set; }
    public DateTime? DeployedAt { get; set; }
    
    // Definition
    public string Definition { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

### Deployment Snapshot Logic

When deploying:
```csharp
flow.Deployed = true;
flow.DeployedDefinition = flow.Definition; // Snapshot
flow.DeployedAt = DateTime.UtcNow;
```

Status calculation:
```csharp
if (!flow.Deployed) 
    return "not-deployed";
else if (flow.DeployedDefinition == flow.Definition)
    return "up-to-date";
else
    return "modified";
```

## UI Implementation Examples

### Flow List Visual Indicators

```html
<!-- Deployed & Up-to-date -->
<span class="status-badge status-success">
  🟢 Deployed
</span>

<!-- Deployed but Modified -->
<span class="status-badge status-warning">
  🟡 Modified (needs redeploy)
</span>

<!-- Not Deployed -->
<span class="status-badge status-inactive">
  ⚪ Not Deployed
</span>
```

### Template Usage Modal

```
Template: "Threshold Monitor"
Used by 5 flows:

┌─────────────────────────────────────────────────────┐
│ Tank 1 Monitor                      🟡 Modified     │
│ Owner: admin@example.com                            │
│ Deployed: Jan 29, 10:00 AM                         │
│ Modified: Jan 30, 4:00 AM                          │
├─────────────────────────────────────────────────────┤
│ Pressure Monitor                    🟢 Up-to-date  │
│ Owner: operator@example.com                         │
│ Deployed: Jan 28, 2:00 PM                          │
└─────────────────────────────────────────────────────┘

[View All] [Update All]
```

### Deployment Diff Viewer

```
┌─────────────────────────────────────────────────────┐
│ Deployment Comparison                               │
├─────────────────────────────────────────────────────┤
│ Deployed Version (Jan 29, 10:00 AM)                │
│ Current Version (Jan 30, 4:00 AM)                  │
│                                                     │
│ Changes detected: 3 nodes added, 1 connection      │
│                                                     │
│ [Show JSON Diff] [Revert to Deployed]             │
└─────────────────────────────────────────────────────┘
```

## API Reference Summary

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/flows/templates` | GET | List all templates with usage count |
| `/api/flows/templates/{id}` | PUT | Update template definition |
| `/api/flows/templates/{id}/usage` | GET | Get all flows using template |
| `/api/flows/{id}/deployment-status` | GET | Check deployment status |
| `/api/flows/{id}/deployment-diff` | GET | Get deployed vs current diff |
| `/api/flows/{id}/deploy` | POST | Deploy (snapshots definition) |
| `/api/flows` | GET | List flows (includes deployment status) |

## Security Considerations

- Only template owner can edit template
- Only flow owner can deploy/undeploy
- Shared templates visible to all users
- Deployment snapshots immutable once created
- Audit log tracks all template changes

## Performance Notes

- Template usage count uses COUNT() query (indexed)
- Deployment status calculated on-demand (no stored field)
- Diff endpoint returns full definitions (can be large)
- Consider caching template usage for frequently used templates

## Future Enhancements

- **Template Versioning**: Full version history with semantic versioning
- **Automated Updates**: Option to auto-update instances when template changes
- **Visual Diff**: Side-by-side graphical comparison of changes
- **Change Notifications**: Alert users when templates they use are updated
- **Approval Workflow**: Require approval for template changes
- **A/B Testing**: Deploy different versions to different instances
- **Rollback History**: Keep history of deployed versions for easy rollback

## Troubleshooting

### Issue: Template usage count is zero but flows exist

**Cause**: Flows created before TemplateFlowId was set

**Solution**:
```sql
UPDATE Flows 
SET TemplateFlowId = '<template-id>'
WHERE Name LIKE '%template-name%';
```

### Issue: Deployment status always shows "modified"

**Cause**: DeployedDefinition was null when deployed

**Solution**: Redeploy the flow to create snapshot

### Issue: Can't edit template

**Cause**: Not the template owner

**Solution**: Contact owner or request shared edit permissions

## Summary

This comprehensive template management system provides:

✅ **Full Template Editing** - Modify templates after creation  
✅ **Usage Tracking** - See all flows using a template  
✅ **Deployment Monitoring** - Track deployment status and changes  
✅ **Drift Detection** - Identify flows that differ from deployed version  
✅ **Safe Deployment** - Review changes before deploying  
✅ **Audit Trail** - Track when changes were made  

These features enable confident template management in production environments with full visibility into template usage and deployment status.
