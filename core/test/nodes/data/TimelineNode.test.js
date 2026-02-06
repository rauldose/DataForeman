/**
 * Tests for TimelineNode
 */

import { TimelineNode } from '../../../src/nodes/data/TimelineNode.js';

describe('TimelineNode', () => {
  let node;

  beforeEach(() => {
    node = new TimelineNode();
  });

  // Helper function to create mock execution context
  function createMockContext(nodeData = {}, inputValues = [], runtimeState = null) {
    return {
      node: {
        id: 'test-tl-node',
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
      expect(node.description.displayName).toBe('Timeline');
      expect(node.description.name).toBe('timeline');
      expect(node.description.category).toBe('DATA_TRANSFORM');
    });

    test('has inputs and outputs defined', () => {
      expect(Array.isArray(node.description.inputs)).toBe(true);
      expect(node.description.inputs).toHaveLength(2);
      expect(node.description.inputs[0].displayName).toBe('Value');
      expect(node.description.inputs[1].displayName).toBe('Clear');
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

  describe('Validation', () => {
    test('validates maxEntries is positive', () => {
      const result = node.validate({ id: 'n1', type: 'timeline', data: { maxEntries: -1, aggregation: 'last' } });
      expect(result.valid).toBe(false);
      expect(result.errors.some(e => e.includes('Max entries'))).toBe(true);
    });

    test('validates windowMs is non-negative', () => {
      const result = node.validate({ id: 'n1', type: 'timeline', data: { maxEntries: 10, windowMs: -5, aggregation: 'last' } });
      expect(result.valid).toBe(false);
      expect(result.errors.some(e => e.includes('Time window'))).toBe(true);
    });

    test('passes for valid configuration', () => {
      const result = node.validate({ id: 'n1', type: 'timeline', data: { maxEntries: 100, windowMs: 0, aggregation: 'last' } });
      expect(result.valid).toBe(true);
    });
  });

  describe('Aggregation', () => {
    const buffer = [
      { value: 10, timestamp: 1000 },
      { value: 20, timestamp: 2000 },
      { value: 30, timestamp: 3000 },
      { value: 5, timestamp: 4000 },
      { value: 15, timestamp: 5000 }
    ];

    test('last returns most recent value', () => {
      expect(node.aggregate(buffer, 'last')).toBe(15);
    });

    test('first returns oldest value', () => {
      expect(node.aggregate(buffer, 'first')).toBe(10);
    });

    test('avg computes average', () => {
      expect(node.aggregate(buffer, 'avg')).toBe(16);
    });

    test('min returns minimum', () => {
      expect(node.aggregate(buffer, 'min')).toBe(5);
    });

    test('max returns maximum', () => {
      expect(node.aggregate(buffer, 'max')).toBe(30);
    });

    test('sum computes total', () => {
      expect(node.aggregate(buffer, 'sum')).toBe(80);
    });

    test('count returns entry count', () => {
      expect(node.aggregate(buffer, 'count')).toBe(5);
    });

    test('range computes max - min', () => {
      expect(node.aggregate(buffer, 'range')).toBe(25);
    });

    test('handles empty buffer', () => {
      expect(node.aggregate([], 'avg')).toBeNull();
      expect(node.aggregate([], 'last')).toBeNull();
      expect(node.aggregate([], 'count')).toBe(0);
    });

    test('handles non-numeric values for math aggregations', () => {
      const mixedBuffer = [
        { value: 'hello', timestamp: 1000 },
        { value: 42, timestamp: 2000 }
      ];
      expect(node.aggregate(mixedBuffer, 'avg')).toBe(42);
      expect(node.aggregate(mixedBuffer, 'last')).toBe(42);
      expect(node.aggregate(mixedBuffer, 'first')).toBe('hello');
    });
  });

  describe('Execution', () => {
    test('adds value to buffer and returns aggregated', async () => {
      const runtimeState = {};
      const ctx = createMockContext(
        { maxEntries: 100, aggregation: 'last' },
        [42],
        runtimeState
      );
      const result = await node.execute(ctx);
      expect(result[0].value).toBe(42);
      expect(result[1].value).toHaveLength(1);
      expect(result[1].value[0].value).toBe(42);
    });

    test('accumulates values across executions', async () => {
      const runtimeState = {};
      const config = { maxEntries: 100, aggregation: 'avg' };

      const ctx1 = createMockContext(config, [10], runtimeState);
      await node.execute(ctx1);

      const ctx2 = createMockContext(config, [20], runtimeState);
      await node.execute(ctx2);

      const ctx3 = createMockContext(config, [30], runtimeState);
      const result = await node.execute(ctx3);

      expect(result[0].value).toBe(20); // avg of 10, 20, 30
      expect(result[1].value).toHaveLength(3);
    });

    test('enforces maxEntries limit', async () => {
      const runtimeState = {};
      const config = { maxEntries: 3, aggregation: 'count' };

      for (let i = 0; i < 5; i++) {
        const ctx = createMockContext(config, [i], runtimeState);
        await node.execute(ctx);
      }

      const ctx = createMockContext(config, [99], runtimeState);
      const result = await node.execute(ctx);

      expect(result[0].value).toBe(3); // count should be maxEntries
      expect(result[1].value).toHaveLength(3);
      // Should keep most recent entries
      expect(result[1].value[2].value).toBe(99);
    });

    test('clears buffer on clear input', async () => {
      const runtimeState = {};
      const config = { maxEntries: 100, aggregation: 'last' };

      // Add some values
      const ctx1 = createMockContext(config, [10], runtimeState);
      await node.execute(ctx1);

      // Clear
      const ctx2 = createMockContext(config, [null, true], runtimeState);
      const result = await node.execute(ctx2);

      expect(result[0].value).toBeNull();
      expect(result[1].value).toHaveLength(0);
    });

    test('returns empty state for null input', async () => {
      const runtimeState = {};
      const ctx = createMockContext(
        { maxEntries: 100, aggregation: 'last' },
        [null],
        runtimeState
      );
      const result = await node.execute(ctx);
      expect(result[0].quality).toBe(64); // bad quality (empty buffer)
      expect(result[1].value).toHaveLength(0);
    });

    test('works without runtimeState (manual execution)', async () => {
      const ctx = createMockContext(
        { maxEntries: 100, aggregation: 'last' },
        [42]
      );
      const result = await node.execute(ctx);
      expect(result[0].value).toBe(42);
    });

    test('prunes by time window', async () => {
      const runtimeState = {};
      const config = { maxEntries: 100, windowMs: 1000, aggregation: 'count' };

      // Manually seed buffer with old timestamps
      const bufferKey = 'tl_test-tl-node';
      runtimeState[bufferKey] = [
        { value: 1, timestamp: Date.now() - 5000 },
        { value: 2, timestamp: Date.now() - 500 }
      ];

      const ctx = createMockContext(config, [3], runtimeState);
      const result = await node.execute(ctx);

      // Old entry (5000ms ago) should be pruned, 500ms ago and new should remain
      expect(result[0].value).toBe(2); // count of remaining entries
      expect(result[1].value).toHaveLength(2);
    });
  });

  describe('Edge Cases', () => {
    test('handles undefined input', async () => {
      const ctx = createMockContext(
        { maxEntries: 100, aggregation: 'last' },
        [undefined],
        {}
      );
      const result = await node.execute(ctx);
      expect(result).toBeDefined();
      expect(result[1].value).toHaveLength(0);
    });

    test('handles string values', async () => {
      const ctx = createMockContext(
        { maxEntries: 100, aggregation: 'last' },
        ['hello'],
        {}
      );
      const result = await node.execute(ctx);
      expect(result[0].value).toBe('hello');
    });

    test('handles boolean values', async () => {
      const ctx = createMockContext(
        { maxEntries: 100, aggregation: 'last' },
        [true],
        {}
      );
      const result = await node.execute(ctx);
      expect(result[0].value).toBe(true);
    });
  });

  describe('Log Messages', () => {
    test('generates info log', () => {
      const logs = node.getLogMessages();
      const result = [
        { value: 42, quality: 0 },
        { value: [{ value: 42, timestamp: Date.now() }], quality: 0 }
      ];
      const msg = logs.info(result);
      expect(msg).toContain('1 entries');
      expect(msg).toContain('42');
    });
  });
});
