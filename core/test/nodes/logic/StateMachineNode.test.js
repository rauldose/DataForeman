/**
 * Tests for StateMachineNode
 */

import { StateMachineNode } from '../../../src/nodes/logic/StateMachineNode.js';

describe('StateMachineNode', () => {
  let node;

  beforeEach(() => {
    node = new StateMachineNode();
  });

  // Helper function to create mock execution context
  function createMockContext(nodeData = {}, inputValues = [], runtimeState = null) {
    return {
      node: {
        id: 'test-sm-node',
        data: nodeData
      },
      getInputValue: (index) => {
        const value = inputValues[index];
        return value !== undefined ? { value } : null;
      },
      getInputCount: () => inputValues.length,
      runtimeState: runtimeState,
      logger: {
        info: () => {},
        debug: () => {},
        error: () => {}
      }
    };
  }

  describe('Metadata', () => {
    test('has valid schema', () => {
      expect(node.description).toBeDefined();
      expect(node.description.schemaVersion).toBe(1);
      expect(node.description.displayName).toBe('State Machine');
      expect(node.description.name).toBe('state-machine');
      expect(node.description.category).toBe('LOGIC_MATH');
    });

    test('has inputs and outputs defined', () => {
      expect(Array.isArray(node.description.inputs)).toBe(true);
      expect(node.description.inputs).toHaveLength(2);
      expect(node.description.inputs[0].displayName).toBe('Event');
      expect(node.description.inputs[1].displayName).toBe('Reset');
      expect(Array.isArray(node.description.outputs)).toBe(true);
      expect(node.description.outputs).toHaveLength(2);
    });

    test('has properties array', () => {
      expect(Array.isArray(node.description.properties)).toBe(true);
      expect(node.description.properties.length).toBeGreaterThanOrEqual(3);
    });

    test('is marked as stateful', () => {
      expect(node.description.extensions.behaviors.stateful).toBe(true);
    });
  });

  describe('parseTransitions', () => {
    test('parses simple transitions', () => {
      const map = node.parseTransitions('idle:start->running');
      expect(map.has('idle')).toBe(true);
      expect(map.get('idle')).toEqual([{ event: 'start', target: 'running' }]);
    });

    test('parses multiple transitions', () => {
      const map = node.parseTransitions('idle:start->running,running:stop->idle');
      expect(map.has('idle')).toBe(true);
      expect(map.has('running')).toBe(true);
      expect(map.get('idle')).toEqual([{ event: 'start', target: 'running' }]);
      expect(map.get('running')).toEqual([{ event: 'stop', target: 'idle' }]);
    });

    test('parses multiple transitions from same state', () => {
      const map = node.parseTransitions('idle:start->running,idle:maintain->maintenance');
      expect(map.get('idle')).toHaveLength(2);
    });

    test('handles empty/null input', () => {
      expect(node.parseTransitions('')).toEqual(new Map());
      expect(node.parseTransitions(null)).toEqual(new Map());
      expect(node.parseTransitions(undefined)).toEqual(new Map());
    });

    test('skips malformed transitions', () => {
      const map = node.parseTransitions('idle:start->running,badformat,another:bad');
      expect(map.size).toBe(1);
      expect(map.has('idle')).toBe(true);
    });

    test('handles whitespace in transitions', () => {
      const map = node.parseTransitions('  idle : start -> running , running : stop -> idle  ');
      expect(map.has('idle')).toBe(true);
      expect(map.has('running')).toBe(true);
    });
  });

  describe('Validation', () => {
    test('validates required initial state', () => {
      const result = node.validate({ id: 'n1', type: 'state-machine', data: { transitions: 'a:b->c' } });
      expect(result.valid).toBe(false);
      expect(result.errors.some(e => e.includes('Initial'))).toBe(true);
    });

    test('validates required transitions', () => {
      const result = node.validate({ id: 'n1', type: 'state-machine', data: { initialState: 'idle' } });
      expect(result.valid).toBe(false);
      expect(result.errors.some(e => e.toLowerCase().includes('transition'))).toBe(true);
    });

    test('validates malformed transitions', () => {
      const result = node.validate({ id: 'n1', type: 'state-machine', data: { initialState: 'idle', transitions: 'badformat' } });
      expect(result.valid).toBe(false);
    });

    test('passes for valid configuration', () => {
      const result = node.validate({
        id: 'n1',
        type: 'state-machine',
        data: { initialState: 'idle', transitions: 'idle:start->running' }
      });
      expect(result.valid).toBe(true);
    });
  });

  describe('Execution', () => {
    test('starts in initial state with no event', async () => {
      const ctx = createMockContext(
        { initialState: 'idle', transitions: 'idle:start->running' },
        [null],
        {}
      );
      const result = await node.execute(ctx);
      expect(result[0].value).toBe('idle');
      expect(result[1].value).toBeNull();
    });

    test('transitions on matching event', async () => {
      const ctx = createMockContext(
        { initialState: 'idle', transitions: 'idle:start->running' },
        ['start'],
        {}
      );
      const result = await node.execute(ctx);
      expect(result[0].value).toBe('running');
      expect(result[1].value.from).toBe('idle');
      expect(result[1].value.to).toBe('running');
      expect(result[1].value.transitioned).toBe(true);
    });

    test('stays in state on non-matching event', async () => {
      const ctx = createMockContext(
        { initialState: 'idle', transitions: 'idle:start->running' },
        ['unknown'],
        {}
      );
      const result = await node.execute(ctx);
      expect(result[0].value).toBe('idle');
      expect(result[1].value.transitioned).toBe(false);
    });

    test('persists state across executions via runtimeState', async () => {
      const runtimeState = {};
      // First execution: idle -> running
      const ctx1 = createMockContext(
        { initialState: 'idle', transitions: 'idle:start->running,running:stop->idle' },
        ['start'],
        runtimeState
      );
      const result1 = await node.execute(ctx1);
      expect(result1[0].value).toBe('running');

      // Second execution: running -> idle
      const ctx2 = createMockContext(
        { initialState: 'idle', transitions: 'idle:start->running,running:stop->idle' },
        ['stop'],
        runtimeState
      );
      const result2 = await node.execute(ctx2);
      expect(result2[0].value).toBe('idle');
      expect(result2[1].value.from).toBe('running');
      expect(result2[1].value.to).toBe('idle');
    });

    test('resets on reset input', async () => {
      const runtimeState = {};
      // First: transition to running
      const ctx1 = createMockContext(
        { initialState: 'idle', transitions: 'idle:start->running,running:stop->idle' },
        ['start'],
        runtimeState
      );
      await node.execute(ctx1);

      // Then: reset
      const ctx2 = createMockContext(
        { initialState: 'idle', transitions: 'idle:start->running,running:stop->idle' },
        ['anything', true],
        runtimeState
      );
      const result = await node.execute(ctx2);
      expect(result[0].value).toBe('idle');
      expect(result[1].value.event).toBe('reset');
    });

    test('resets on invalid event when resetOnInvalid is true', async () => {
      const runtimeState = {};
      // First: transition to running
      const ctx1 = createMockContext(
        { initialState: 'idle', transitions: 'idle:start->running,running:stop->idle' },
        ['start'],
        runtimeState
      );
      await node.execute(ctx1);

      // Then: invalid event with resetOnInvalid
      const ctx2 = createMockContext(
        { initialState: 'idle', transitions: 'idle:start->running,running:stop->idle', resetOnInvalid: true },
        ['invalid_event'],
        runtimeState
      );
      const result = await node.execute(ctx2);
      expect(result[0].value).toBe('idle');
      expect(result[1].value.resetOnInvalid).toBe(true);
    });

    test('works without runtimeState (manual execution)', async () => {
      const ctx = createMockContext(
        { initialState: 'idle', transitions: 'idle:start->running' },
        ['start']
      );
      const result = await node.execute(ctx);
      expect(result[0].value).toBe('running');
    });

    test('handles numeric event values', async () => {
      const ctx = createMockContext(
        { initialState: 'off', transitions: 'off:1->on,on:0->off' },
        [1],
        {}
      );
      const result = await node.execute(ctx);
      expect(result[0].value).toBe('on');
    });
  });

  describe('Edge Cases', () => {
    test('handles null event input', async () => {
      const ctx = createMockContext(
        { initialState: 'idle', transitions: 'idle:start->running' },
        [null],
        {}
      );
      const result = await node.execute(ctx);
      expect(result[0].value).toBe('idle');
    });

    test('handles undefined event input', async () => {
      const ctx = createMockContext(
        { initialState: 'idle', transitions: 'idle:start->running' },
        [undefined],
        {}
      );
      const result = await node.execute(ctx);
      expect(result[0].value).toBe('idle');
    });

    test('handles empty string event', async () => {
      const ctx = createMockContext(
        { initialState: 'idle', transitions: 'idle:start->running' },
        [''],
        {}
      );
      const result = await node.execute(ctx);
      expect(result[0].value).toBe('idle');
    });
  });

  describe('Log Messages', () => {
    test('generates info log for transition', () => {
      const logs = node.getLogMessages();
      const result = [
        { value: 'running', quality: 0 },
        { value: { from: 'idle', to: 'running', event: 'start', transitioned: true }, quality: 0 }
      ];
      const msg = logs.info(result);
      expect(msg).toContain('idle');
      expect(msg).toContain('running');
    });

    test('generates info log for no transition', () => {
      const logs = node.getLogMessages();
      const result = [
        { value: 'idle', quality: 0 },
        { value: { from: 'idle', to: 'idle', event: 'unknown', transitioned: false }, quality: 192 }
      ];
      const msg = logs.info(result);
      expect(msg).toContain('no valid transition');
    });
  });
});
