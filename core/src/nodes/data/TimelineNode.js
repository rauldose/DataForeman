import { BaseNode } from '../base/BaseNode.js';

/**
 * Timeline Node
 * 
 * Buffers input values over a configurable time window or count, providing
 * a rolling history of values for trend analysis. Supports aggregation
 * modes for summarizing buffered data.
 * 
 * Stateful: Maintains a buffer of recent values via runtimeState.
 */
export class TimelineNode extends BaseNode {
  description = {
    schemaVersion: 1,
    displayName: 'Timeline',
    name: 'timeline',
    version: 1,
    description: 'Buffer values over time for trend analysis with rolling window and aggregation',
    category: 'DATA_TRANSFORM',
    section: 'ANALYSIS',
    icon: '📈',
    color: '#0288D1',

    inputs: [
      {
        type: 'main',
        displayName: 'Value',
        required: true,
        description: 'Input value to add to the timeline buffer'
      },
      {
        type: 'main',
        displayName: 'Clear',
        required: false,
        description: 'When truthy, clears the timeline buffer'
      }
    ],

    outputs: [
      {
        type: 'main',
        displayName: 'Aggregated',
        description: 'Aggregated value from the buffer (based on selected mode)'
      },
      {
        type: 'main',
        displayName: 'Buffer',
        description: 'Full buffer as an array of {value, timestamp} entries'
      }
    ],

    visual: {
      canvas: {
        minWidth: 180,
        shape: 'rounded-rect',
        borderRadius: 8,
        resizable: false
      },
      layout: [
        {
          type: 'header',
          icon: '📈',
          title: 'Timeline',
          color: '#0288D1',
          badges: ['executionOrder']
        },
        {
          type: 'subtitle',
          text: '{{aggregation}} / {{maxEntries}} entries',
          visible: '{{aggregation}}'
        }
      ],
      handles: {
        inputs: [
          { index: 0, position: 'auto', color: 'auto', label: 'Value', visible: true },
          { index: 1, position: 'auto', color: 'auto', label: 'Clear', visible: true }
        ],
        outputs: [
          { index: 0, position: 'auto', color: 'auto', label: 'Aggregated', visible: true },
          { index: 1, position: 'auto', color: 'auto', label: 'Buffer', visible: true }
        ],
        size: 12,
        borderWidth: 2,
        borderColor: '#ffffff'
      },
      status: {
        execution: {
          enabled: true,
          position: 'top-left',
          offset: { x: -10, y: -10 }
        },
        pinned: {
          enabled: true,
          position: 'top-right',
          offset: { x: -8, y: -8 }
        },
        executionOrder: {
          enabled: true,
          position: 'header'
        }
      },
      runtime: {
        enabled: false
      }
    },

    properties: [
      {
        name: 'maxEntries',
        displayName: 'Max Entries',
        type: 'number',
        default: 100,
        required: true,
        description: 'Maximum number of entries to keep in the buffer',
        userExposable: true,
        min: 1,
        max: 10000,
        step: 1
      },
      {
        name: 'windowMs',
        displayName: 'Time Window (ms)',
        type: 'number',
        default: 0,
        required: false,
        description: 'Time window in milliseconds. Entries older than this are removed. 0 = no time limit (count-based only).',
        userExposable: true,
        min: 0,
        step: 1000
      },
      {
        name: 'aggregation',
        displayName: 'Aggregation',
        type: 'options',
        default: 'last',
        required: true,
        description: 'How to aggregate buffered values for the primary output',
        options: [
          { name: 'Last', value: 'last', description: 'Most recent value' },
          { name: 'First', value: 'first', description: 'Oldest value in buffer' },
          { name: 'Average', value: 'avg', description: 'Average of all values' },
          { name: 'Minimum', value: 'min', description: 'Minimum value in buffer' },
          { name: 'Maximum', value: 'max', description: 'Maximum value in buffer' },
          { name: 'Sum', value: 'sum', description: 'Sum of all values' },
          { name: 'Count', value: 'count', description: 'Number of entries in buffer' },
          { name: 'Range', value: 'range', description: 'Difference between max and min' }
        ]
      }
    ],

    configUI: {
      sections: [
        {
          type: 'property-group',
          title: 'Configuration'
        }
      ]
    },

    extensions: {
      behaviors: {
        stateful: true,
        streaming: false,
        sideEffects: false
      }
    }
  };

  /**
   * Validate node configuration
   */
  validate(node) {
    const baseValidation = super.validate(node);
    if (!baseValidation.valid) {
      return baseValidation;
    }

    const errors = [];
    const maxEntries = this.getParameter(node, 'maxEntries');

    if (maxEntries !== undefined && (typeof maxEntries !== 'number' || maxEntries < 1)) {
      errors.push('Max entries must be a positive number');
    }

    const windowMs = this.getParameter(node, 'windowMs');
    if (windowMs !== undefined && (typeof windowMs !== 'number' || windowMs < 0)) {
      errors.push('Time window must be a non-negative number');
    }

    return {
      valid: errors.length === 0,
      errors
    };
  }

  /**
   * Compute aggregated value from numeric buffer entries.
   * 
   * @param {Array<{value: *, timestamp: number}>} buffer - The buffer entries
   * @param {string} aggregation - Aggregation mode
   * @returns {*} Aggregated value
   */
  aggregate(buffer, aggregation) {
    if (buffer.length === 0) {
      return aggregation === 'count' ? 0 : null;
    }

    // Extract numeric values for math aggregations
    const numericValues = buffer
      .map(e => Number(e.value))
      .filter(v => !isNaN(v) && isFinite(v));

    switch (aggregation) {
      case 'last':
        return buffer[buffer.length - 1].value;

      case 'first':
        return buffer[0].value;

      case 'avg':
        if (numericValues.length === 0) return null;
        return numericValues.reduce((a, b) => a + b, 0) / numericValues.length;

      case 'min':
        if (numericValues.length === 0) return null;
        return Math.min(...numericValues);

      case 'max':
        if (numericValues.length === 0) return null;
        return Math.max(...numericValues);

      case 'sum':
        if (numericValues.length === 0) return null;
        return numericValues.reduce((a, b) => a + b, 0);

      case 'count':
        return buffer.length;

      case 'range':
        if (numericValues.length === 0) return null;
        return Math.max(...numericValues) - Math.min(...numericValues);

      default:
        return buffer[buffer.length - 1].value;
    }
  }

  /**
   * Execute timeline logic
   */
  async execute(context) {
    const maxEntries = this.getParameter(context.node, 'maxEntries', 100);
    const windowMs = this.getParameter(context.node, 'windowMs', 0);
    const aggregation = this.getParameter(context.node, 'aggregation', 'last');

    // Runtime state key for this node's buffer
    const bufferKey = `tl_${context.node.id}`;

    // Initialize or retrieve buffer from runtime state
    let buffer;
    if (context.runtimeState && context.runtimeState[bufferKey]) {
      buffer = context.runtimeState[bufferKey];
    } else {
      buffer = [];
    }

    // Check for clear input
    const clearInput = context.getInputValue(1);
    const clearValue = clearInput !== null && clearInput !== undefined && clearInput.value !== undefined
      ? clearInput.value
      : clearInput;
    if (clearValue) {
      buffer = [];
      if (context.runtimeState) {
        context.runtimeState[bufferKey] = buffer;
      }
      return [
        { value: null, quality: 64 },
        { value: [], quality: 0 }
      ];
    }

    // Get input value
    const inputData = context.getInputValue(0);
    const inputValue = inputData !== null && inputData !== undefined && inputData.value !== undefined
      ? inputData.value
      : inputData;
    const now = Date.now();

    // Add new entry if input is not null/undefined
    if (inputValue !== null && inputValue !== undefined) {
      buffer.push({ value: inputValue, timestamp: now });
    }

    // Prune by time window if configured
    if (windowMs > 0) {
      const cutoff = now - windowMs;
      buffer = buffer.filter(e => e.timestamp >= cutoff);
    }

    // Prune by max entries (keep most recent)
    if (buffer.length > maxEntries) {
      buffer = buffer.slice(buffer.length - maxEntries);
    }

    // Store updated buffer in runtime state
    if (context.runtimeState) {
      context.runtimeState[bufferKey] = buffer;
    }

    // Compute aggregated output
    const aggregatedValue = this.aggregate(buffer, aggregation);
    const quality = buffer.length > 0 ? 0 : 64;

    return [
      { value: aggregatedValue, quality },
      { value: buffer.map(e => ({ value: e.value, timestamp: e.timestamp })), quality: 0 }
    ];
  }

  /**
   * Declarative log messages
   */
  getLogMessages() {
    return {
      info: (result) => {
        if (Array.isArray(result)) {
          const bufferSize = result[1]?.value?.length ?? 0;
          return `Timeline: ${bufferSize} entries, aggregated=${JSON.stringify(result[0]?.value)}`;
        }
        return `Timeline output: ${JSON.stringify(result)}`;
      },
      debug: (result) => {
        return `Timeline buffer output: ${JSON.stringify(result)}`;
      },
      error: (error) => `Timeline failed: ${error.message}`
    };
  }

  static get help() {
    return {
      overview: 'Buffers input values over a configurable time window or entry count, providing a rolling history for trend analysis. Supports multiple aggregation modes to summarize buffered data.',
      useCases: [
        'Track temperature trends over the last hour',
        'Compute rolling averages for smoothing sensor data',
        'Detect min/max values within a time window',
        'Buffer data points for downstream chart visualization',
        'Calculate rate of change by comparing first and last values'
      ],
      examples: [
        {
          title: 'Rolling Average (last 10 readings)',
          config: { maxEntries: 10, windowMs: 0, aggregation: 'avg' },
          input: { value: 25.5 },
          output: { aggregated: 24.8, buffer: '10 entries' }
        },
        {
          title: 'Time-windowed Max (last 5 minutes)',
          config: { maxEntries: 1000, windowMs: 300000, aggregation: 'max' },
          input: { value: 98.6 },
          output: { aggregated: 99.1, buffer: '47 entries' }
        }
      ],
      tips: [
        'Set maxEntries to limit memory usage for high-frequency data',
        'Use windowMs to automatically remove old entries based on time',
        'Combine maxEntries and windowMs for dual-constraint buffering',
        'The Buffer output provides full history for charting or analysis',
        'Non-numeric values are preserved in the buffer but excluded from math aggregations',
        'Use the Clear input to reset the buffer programmatically'
      ],
      relatedNodes: ['DelayNode', 'ArrayOpsNode', 'MathNode']
    };
  }
}
