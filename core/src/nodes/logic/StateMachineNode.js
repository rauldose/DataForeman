import { BaseNode } from '../base/BaseNode.js';

/**
 * State Machine Node
 * 
 * Implements a finite state machine within a flow. Tracks the current state
 * and transitions between states based on input events. Useful for modeling
 * equipment status, process phases, alarm states, and sequential operations.
 * 
 * Stateful: Maintains current state across executions via runtimeState.
 */
export class StateMachineNode extends BaseNode {
  description = {
    schemaVersion: 1,
    displayName: 'State Machine',
    name: 'state-machine',
    version: 1,
    description: 'Finite state machine that transitions between states based on input events',
    category: 'LOGIC_MATH',
    section: 'CONTROL',
    icon: '🔄',
    color: '#7B1FA2',

    inputs: [
      {
        type: 'main',
        displayName: 'Event',
        required: true,
        description: 'Event name or value that triggers a state transition'
      },
      {
        type: 'main',
        displayName: 'Reset',
        required: false,
        description: 'When truthy, resets the state machine to its initial state'
      }
    ],

    outputs: [
      {
        type: 'main',
        displayName: 'Current State',
        description: 'The current state name after processing the event'
      },
      {
        type: 'main',
        displayName: 'Transition',
        description: 'Details about the transition that occurred (null if no transition)'
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
          icon: '🔄',
          title: 'State Machine',
          color: '#7B1FA2',
          badges: ['executionOrder']
        },
        {
          type: 'subtitle',
          text: '{{initialState}}',
          visible: '{{initialState}}'
        }
      ],
      handles: {
        inputs: [
          { index: 0, position: 'auto', color: 'auto', label: 'Event', visible: true },
          { index: 1, position: 'auto', color: 'auto', label: 'Reset', visible: true }
        ],
        outputs: [
          { index: 0, position: 'auto', color: 'auto', label: 'State', visible: true },
          { index: 1, position: 'auto', color: 'auto', label: 'Transition', visible: true }
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
        name: 'initialState',
        displayName: 'Initial State',
        type: 'string',
        default: 'idle',
        required: true,
        userExposable: true,
        description: 'Name of the starting state'
      },
      {
        name: 'transitions',
        displayName: 'Transitions',
        type: 'string',
        default: 'idle:start->running,running:stop->idle',
        required: true,
        userExposable: true,
        description: 'Comma-separated transitions in format "sourceState:event->targetState"'
      },
      {
        name: 'resetOnInvalid',
        displayName: 'Reset on Invalid Event',
        type: 'boolean',
        default: false,
        required: false,
        description: 'Reset to initial state when an event has no valid transition'
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
   * Parse transitions string into a structured map.
   * Format: "sourceState:event->targetState,sourceState:event->targetState"
   * 
   * @param {string} transitionsStr - Comma-separated transition definitions
   * @returns {Map<string, Array<{event: string, target: string}>>} Map of source state to transitions
   */
  parseTransitions(transitionsStr) {
    const transitionMap = new Map();

    if (!transitionsStr || typeof transitionsStr !== 'string') {
      return transitionMap;
    }

    const parts = transitionsStr.split(',').map(s => s.trim()).filter(Boolean);

    for (const part of parts) {
      // Format: sourceState:event->targetState
      const arrowIndex = part.indexOf('->');
      if (arrowIndex === -1) continue;

      const left = part.substring(0, arrowIndex).trim();
      const target = part.substring(arrowIndex + 2).trim();

      const colonIndex = left.indexOf(':');
      if (colonIndex === -1) continue;

      const source = left.substring(0, colonIndex).trim();
      const event = left.substring(colonIndex + 1).trim();

      if (!source || !event || !target) continue;

      if (!transitionMap.has(source)) {
        transitionMap.set(source, []);
      }
      transitionMap.get(source).push({ event, target });
    }

    return transitionMap;
  }

  /**
   * Validate node configuration
   */
  validate(node) {
    const baseValidation = super.validate(node);
    if (!baseValidation.valid) {
      return baseValidation;
    }

    const errors = [];
    const initialState = this.getParameter(node, 'initialState');
    const transitionsStr = this.getParameter(node, 'transitions');

    if (!initialState || typeof initialState !== 'string' || !initialState.trim()) {
      errors.push('Initial state is required');
    }

    if (!transitionsStr || typeof transitionsStr !== 'string' || !transitionsStr.trim()) {
      errors.push('At least one transition is required');
    } else {
      const transitionMap = this.parseTransitions(transitionsStr);
      if (transitionMap.size === 0) {
        errors.push('No valid transitions found. Use format: "sourceState:event->targetState"');
      }
    }

    return {
      valid: errors.length === 0,
      errors
    };
  }

  /**
   * Execute state machine logic
   */
  async execute(context) {
    const initialState = this.getParameter(context.node, 'initialState', 'idle');
    const transitionsStr = this.getParameter(context.node, 'transitions', '');
    const resetOnInvalid = this.getParameter(context.node, 'resetOnInvalid', false);

    const transitionMap = this.parseTransitions(transitionsStr);

    // Get runtime state key for this specific node
    const stateKey = `sm_${context.node.id}`;

    // Determine current state from runtime state or use initial
    let currentState;
    if (context.runtimeState && context.runtimeState[stateKey]) {
      currentState = context.runtimeState[stateKey];
    } else {
      currentState = initialState;
    }

    // Check for reset input
    const resetInput = context.getInputValue(1);
    const resetValue = resetInput !== null && resetInput !== undefined && resetInput.value !== undefined
      ? resetInput.value
      : resetInput;
    if (resetValue) {
      currentState = initialState;
      if (context.runtimeState) {
        context.runtimeState[stateKey] = currentState;
      }
      return [
        { value: currentState, quality: 0 },
        { value: { from: currentState, to: currentState, event: 'reset', transitioned: true }, quality: 0 }
      ];
    }

    // Get event input
    const eventInput = context.getInputValue(0);
    const eventValue = eventInput !== null && eventInput !== undefined && eventInput.value !== undefined
      ? eventInput.value
      : eventInput;

    // If no event, just output current state with no transition
    if (eventValue === null || eventValue === undefined || eventValue === '') {
      return [
        { value: currentState, quality: 0 },
        { value: null, quality: 64 }
      ];
    }

    const eventStr = String(eventValue);

    // Look for matching transition
    const stateTransitions = transitionMap.get(currentState);
    let matched = null;

    if (stateTransitions) {
      matched = stateTransitions.find(t => t.event === eventStr);
    }

    if (matched) {
      // Transition found
      const previousState = currentState;
      currentState = matched.target;

      // Store new state in runtime state
      if (context.runtimeState) {
        context.runtimeState[stateKey] = currentState;
      }

      return [
        { value: currentState, quality: 0 },
        { value: { from: previousState, to: currentState, event: eventStr, transitioned: true }, quality: 0 }
      ];
    }

    // No matching transition
    if (resetOnInvalid) {
      const previousState = currentState;
      currentState = initialState;
      if (context.runtimeState) {
        context.runtimeState[stateKey] = currentState;
      }
      return [
        { value: currentState, quality: 0 },
        { value: { from: previousState, to: currentState, event: eventStr, transitioned: true, resetOnInvalid: true }, quality: 0 }
      ];
    }

    // Stay in current state
    return [
      { value: currentState, quality: 0 },
      { value: { from: currentState, to: currentState, event: eventStr, transitioned: false }, quality: 192 }
    ];
  }

  /**
   * Declarative log messages
   */
  getLogMessages() {
    return {
      info: (result) => {
        if (Array.isArray(result) && result[1]?.value) {
          const t = result[1].value;
          if (t.transitioned) {
            return `State Machine: ${t.from} → ${t.to} (event: ${t.event})`;
          }
          return `State Machine: stayed in ${t.from} (event: ${t.event}, no valid transition)`;
        }
        return `State Machine: ${result[0]?.value || 'unknown'}`;
      },
      debug: (result) => {
        return `State output: ${JSON.stringify(result)}`;
      },
      error: (error) => `State Machine failed: ${error.message}`
    };
  }

  static get help() {
    return {
      overview: 'Implements a finite state machine within a flow. Tracks the current state and transitions between states based on input events. Useful for equipment status tracking, process automation, alarm management, and sequential control.',
      useCases: [
        'Track equipment status (idle, running, stopped, maintenance)',
        'Model process phases (startup, processing, cooldown, shutdown)',
        'Manage alarm states (normal, warning, alarm, acknowledged)',
        'Control sequential operations with event-driven transitions'
      ],
      examples: [
        {
          title: 'Equipment Status',
          config: {
            initialState: 'stopped',
            transitions: 'stopped:start->starting,starting:ready->running,running:stop->stopping,stopping:done->stopped'
          },
          input: { value: 'start' },
          output: { currentState: 'starting', transition: { from: 'stopped', to: 'starting', event: 'start' } }
        },
        {
          title: 'Traffic Light',
          config: {
            initialState: 'red',
            transitions: 'red:next->green,green:next->yellow,yellow:next->red'
          },
          input: { value: 'next' },
          output: { currentState: 'green', transition: { from: 'red', to: 'green', event: 'next' } }
        }
      ],
      tips: [
        'Define transitions in format: sourceState:event->targetState',
        'Separate multiple transitions with commas',
        'Use the Reset input to force the machine back to its initial state',
        'Enable "Reset on Invalid Event" to return to initial state when no transition matches',
        'The Transition output provides details about state changes for downstream processing',
        'State is preserved across executions via runtime state storage'
      ],
      relatedNodes: ['SwitchNode', 'GateNode', 'ComparisonNode']
    };
  }
}
