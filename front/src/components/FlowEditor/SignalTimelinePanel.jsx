/**
 * Signal Timeline Panel
 * 
 * Real-time signal timeline viewer showing live tag values as step charts.
 * Displays a list of signals with their current values and a synchronized
 * time-series chart for visual trend monitoring.
 */

import React, { useState, useEffect, useRef, useMemo, useCallback } from 'react';
import {
  Box,
  Typography,
  IconButton,
  Tooltip,
  Chip,
  useTheme,
  Checkbox,
  Button,
} from '@mui/material';
import {
  Close as CloseIcon,
  DeleteSweep as ClearIcon,
  PlayArrow as PlayIcon,
  Pause as PauseIcon,
  SwapVert as SwapVertIcon,
  FiberManualRecord as DotIcon,
  FilterList as FilterIcon,
} from '@mui/icons-material';
import ReactECharts from 'echarts-for-react';

// Color palette for signals
const SIGNAL_COLORS = [
  '#00e5ff', '#76ff03', '#ffea00', '#ff6e40',
  '#e040fb', '#40c4ff', '#69f0ae', '#ffd740',
  '#ff5252', '#7c4dff', '#18ffff', '#b2ff59',
  '#ffff00', '#ff9100', '#f50057', '#536dfe',
];

// Panel size constraints
const MIN_PANEL_HEIGHT = 200;
const MAX_PANEL_HEIGHT = 800;
const MIN_PANEL_WIDTH = 350;
const MAX_PANEL_WIDTH = 900;

// Keep 3x the visible window in buffer for smooth scrollback
const BUFFER_WINDOW_MULTIPLIER = 3;

/**
 * Formats a value for display in the signal list.
 * @param {*} value - The signal value (number, boolean, string, null, or undefined)
 * @returns {string} Formatted display string
 */
function formatValue(value) {
  if (value === null || value === undefined) return '—';
  if (typeof value === 'boolean') return value ? 'True' : 'False';
  if (typeof value === 'number') {
    if (Number.isInteger(value)) return value.toString();
    return value.toFixed(3);
  }
  return String(value);
}

/**
 * Converts a signal value to a numeric chart value.
 * Booleans are mapped to 0/1.
 * @param {*} value - The signal value
 * @returns {number|*} Numeric value suitable for charting
 */
function toChartValue(value) {
  if (typeof value === 'boolean') return value ? 1 : 0;
  return value;
}

export default function SignalTimelinePanel({
  flowId,
  liveData = {},
  position = 'right',  // 'bottom' or 'right'
  onPositionChange,
  onClose,
}) {
  const theme = useTheme();
  const [followLive, setFollowLive] = useState(true);
  const [signalHistory, setSignalHistory] = useState({}); // { signalKey: [{time, value}] }
  const [selectedSignals, setSelectedSignals] = useState(new Set());
  const [maxDataPoints] = useState(300);
  const [timeWindowMs] = useState(60000); // 60 seconds visible window
  const chartRef = useRef(null);
  const prevLiveDataRef = useRef({});

  // Resize state
  const [size, setSize] = useState(() => {
    const saved = localStorage.getItem(`signalPanel_${position}_size`);
    return saved ? parseInt(saved) : (position === 'bottom' ? 350 : 480);
  });
  const [isResizing, setIsResizing] = useState(false);

  // Collect all signal keys from live data
  const signalKeys = useMemo(() => {
    return Object.keys(liveData).sort();
  }, [liveData]);

  // Auto-select all signals when they first appear
  useEffect(() => {
    setSelectedSignals(prev => {
      const next = new Set(prev);
      let changed = false;
      for (const key of signalKeys) {
        if (!next.has(key)) {
          next.add(key);
          changed = true;
        }
      }
      return changed ? next : prev;
    });
  }, [signalKeys]);

  // Update signal history when live data changes
  useEffect(() => {
    if (!followLive) return;
    const now = Date.now();
    const prev = prevLiveDataRef.current;

    setSignalHistory(history => {
      const updated = { ...history };
      let changed = false;

      for (const [key, data] of Object.entries(liveData)) {
        const value = data?.value ?? data;
        const prevValue = prev[key]?.value ?? prev[key];

        // Only add point if value changed or new signal
        if (!updated[key] || prevValue !== value) {
          if (!updated[key]) {
            updated[key] = [];
          }
          updated[key] = [...updated[key], { time: now, value }];

          // Prune old data points beyond the buffer window
          const cutoff = now - timeWindowMs * BUFFER_WINDOW_MULTIPLIER;
          if (updated[key].length > maxDataPoints) {
            updated[key] = updated[key].filter(p => p.time > cutoff).slice(-maxDataPoints);
          }
          changed = true;
        }
      }

      return changed ? updated : history;
    });

    prevLiveDataRef.current = liveData;
  }, [liveData, followLive, maxDataPoints, timeWindowMs]);

  // Build ECharts option
  const chartOption = useMemo(() => {
    const now = Date.now();
    const visibleSignals = signalKeys.filter(k => selectedSignals.has(k));

    const series = visibleSignals.map((key, idx) => {
      const points = signalHistory[key] || [];
      const color = SIGNAL_COLORS[idx % SIGNAL_COLORS.length];

      return {
        name: key,
        type: 'line',
        step: 'end',
        symbol: 'none',
        lineStyle: { width: 1.5, color },
        itemStyle: { color },
        data: points.map(p => [p.time, toChartValue(p.value)]),
        animation: false,
      };
    });

    return {
      backgroundColor: 'transparent',
      grid: {
        left: 60,
        right: 20,
        top: 10,
        bottom: 30,
      },
      xAxis: {
        type: 'time',
        min: followLive ? now - timeWindowMs : undefined,
        max: followLive ? now : undefined,
        axisLabel: {
          color: theme.palette.text.secondary,
          fontSize: 10,
          formatter: (value) => {
            const d = new Date(value);
            return `${d.getMinutes().toString().padStart(2, '0')}:${d.getSeconds().toString().padStart(2, '0')}`;
          },
        },
        axisLine: { lineStyle: { color: theme.palette.divider } },
        splitLine: { show: true, lineStyle: { color: theme.palette.divider, opacity: 0.3 } },
      },
      yAxis: {
        type: 'value',
        axisLabel: {
          color: theme.palette.text.secondary,
          fontSize: 10,
        },
        axisLine: { lineStyle: { color: theme.palette.divider } },
        splitLine: { show: true, lineStyle: { color: theme.palette.divider, opacity: 0.3 } },
      },
      tooltip: {
        trigger: 'axis',
        backgroundColor: theme.palette.background.paper,
        borderColor: theme.palette.divider,
        textStyle: { color: theme.palette.text.primary, fontSize: 11 },
      },
      series,
    };
  }, [signalHistory, selectedSignals, signalKeys, followLive, timeWindowMs, theme]);

  // Toggle signal selection
  const toggleSignal = useCallback((key) => {
    setSelectedSignals(prev => {
      const next = new Set(prev);
      if (next.has(key)) {
        next.delete(key);
      } else {
        next.add(key);
      }
      return next;
    });
  }, []);

  // Clear history
  const handleClear = useCallback(() => {
    setSignalHistory({});
  }, []);

  // Resize handlers
  useEffect(() => {
    const handleMouseMove = (e) => {
      if (!isResizing) return;
      if (position === 'bottom') {
        const newHeight = window.innerHeight - e.clientY;
        setSize(Math.max(MIN_PANEL_HEIGHT, Math.min(MAX_PANEL_HEIGHT, newHeight)));
      } else {
        const newWidth = window.innerWidth - e.clientX;
        setSize(Math.max(MIN_PANEL_WIDTH, Math.min(MAX_PANEL_WIDTH, newWidth)));
      }
    };
    const handleMouseUp = () => {
      if (isResizing) {
        setIsResizing(false);
        localStorage.setItem(`signalPanel_${position}_size`, size.toString());
      }
    };

    if (isResizing) {
      document.addEventListener('mousemove', handleMouseMove);
      document.addEventListener('mouseup', handleMouseUp);
      document.body.style.cursor = position === 'bottom' ? 'ns-resize' : 'ew-resize';
      document.body.style.userSelect = 'none';
    }
    return () => {
      document.removeEventListener('mousemove', handleMouseMove);
      document.removeEventListener('mouseup', handleMouseUp);
      document.body.style.cursor = '';
      document.body.style.userSelect = '';
    };
  }, [isResizing, position, size]);

  useEffect(() => {
    const saved = localStorage.getItem(`signalPanel_${position}_size`);
    setSize(saved ? parseInt(saved) : (position === 'bottom' ? 350 : 480));
  }, [position]);

  const visibleCount = signalKeys.filter(k => selectedSignals.has(k)).length;

  return (
    <Box
      sx={{
        display: 'flex',
        flexDirection: 'column',
        width: position === 'right' ? size : '100%',
        height: position === 'bottom' ? size : '100%',
        borderLeft: position === 'right' ? 1 : 0,
        borderTop: position === 'bottom' ? 1 : 0,
        borderColor: 'divider',
        bgcolor: 'background.paper',
        overflow: 'hidden',
        position: 'relative',
      }}
    >
      {/* Resize handle */}
      <Box
        onMouseDown={() => setIsResizing(true)}
        sx={{
          position: 'absolute',
          ...(position === 'right' ? {
            left: 0, top: 0, bottom: 0, width: 4,
            cursor: 'ew-resize',
            '&:hover': { bgcolor: 'primary.main', opacity: 0.5 },
          } : {
            left: 0, right: 0, top: 0, height: 4,
            cursor: 'ns-resize',
            '&:hover': { bgcolor: 'primary.main', opacity: 0.5 },
          }),
          zIndex: 10,
        }}
      />

      {/* Header */}
      <Box sx={{
        display: 'flex',
        alignItems: 'center',
        px: 1.5,
        py: 0.5,
        borderBottom: 1,
        borderColor: 'divider',
        minHeight: 40,
        gap: 1,
      }}>
        <FilterIcon sx={{ fontSize: 16, color: 'text.secondary' }} />
        <Typography variant="caption" fontWeight="bold" sx={{ mr: 'auto' }}>
          Signals
        </Typography>

        <Tooltip title={followLive ? 'Pause' : 'Follow Live'}>
          <IconButton size="small" onClick={() => setFollowLive(f => !f)}>
            {followLive ? <PauseIcon fontSize="small" /> : <PlayIcon fontSize="small" />}
          </IconButton>
        </Tooltip>
        <Tooltip title="Clear">
          <IconButton size="small" onClick={handleClear}>
            <ClearIcon fontSize="small" />
          </IconButton>
        </Tooltip>
        <Chip
          label={followLive ? 'Live' : 'Paused'}
          size="small"
          color={followLive ? 'success' : 'default'}
          variant="outlined"
          sx={{ height: 20, fontSize: 10 }}
        />
        <Chip
          label={`${visibleCount} / ${signalKeys.length}`}
          size="small"
          variant="outlined"
          sx={{ height: 20, fontSize: 10 }}
        />
        {onPositionChange && (
          <Tooltip title={`Move to ${position === 'right' ? 'bottom' : 'right'}`}>
            <IconButton size="small" onClick={() => onPositionChange(position === 'right' ? 'bottom' : 'right')}>
              <SwapVertIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        )}
        <Tooltip title="Close">
          <IconButton size="small" onClick={onClose}>
            <CloseIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </Box>

      {/* Content: signal list + chart */}
      <Box sx={{
        display: 'flex',
        flexDirection: position === 'bottom' ? 'row' : 'column',
        flex: 1,
        overflow: 'hidden',
      }}>
        {/* Signal list */}
        <Box sx={{
          width: position === 'bottom' ? 220 : '100%',
          maxHeight: position === 'right' ? '40%' : '100%',
          borderRight: position === 'bottom' ? 1 : 0,
          borderBottom: position === 'right' ? 1 : 0,
          borderColor: 'divider',
          overflow: 'auto',
          flexShrink: 0,
        }}>
          {signalKeys.length === 0 ? (
            <Box sx={{ p: 2, textAlign: 'center' }}>
              <Typography variant="caption" color="text.secondary">
                No live signals. Deploy or test a flow with live values enabled.
              </Typography>
            </Box>
          ) : (
            signalKeys.map((key, idx) => {
              const data = liveData[key];
              const value = data?.value ?? data;
              const color = SIGNAL_COLORS[idx % SIGNAL_COLORS.length];
              const checked = selectedSignals.has(key);

              return (
                <Box
                  key={key}
                  onClick={() => toggleSignal(key)}
                  sx={{
                    display: 'flex',
                    alignItems: 'center',
                    px: 1,
                    py: 0.25,
                    cursor: 'pointer',
                    opacity: checked ? 1 : 0.4,
                    '&:hover': { bgcolor: 'action.hover' },
                    borderBottom: '1px solid',
                    borderColor: 'divider',
                  }}
                >
                  <DotIcon sx={{ fontSize: 10, color, mr: 0.5, flexShrink: 0 }} />
                  <Typography
                    variant="caption"
                    noWrap
                    sx={{ flex: 1, fontSize: 11, color: 'text.primary' }}
                    title={key}
                  >
                    {key}
                  </Typography>
                  <Typography
                    variant="caption"
                    sx={{
                      fontFamily: 'monospace',
                      fontSize: 11,
                      ml: 1,
                      color: 'text.secondary',
                      flexShrink: 0,
                      minWidth: 50,
                      textAlign: 'right',
                    }}
                  >
                    {formatValue(value)}
                  </Typography>
                </Box>
              );
            })
          )}
        </Box>

        {/* Chart */}
        <Box sx={{ flex: 1, minHeight: 0, minWidth: 0, position: 'relative' }}>
          {signalKeys.length > 0 ? (
            <ReactECharts
              ref={chartRef}
              option={chartOption}
              style={{ width: '100%', height: '100%' }}
              opts={{ renderer: 'canvas' }}
              notMerge={false}
              lazyUpdate={true}
            />
          ) : (
            <Box sx={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              height: '100%',
              color: 'text.secondary',
            }}>
              <Typography variant="body2">
                Waiting for signals...
              </Typography>
            </Box>
          )}
        </Box>
      </Box>

      {/* Footer status bar */}
      <Box sx={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        px: 1.5,
        py: 0.25,
        borderTop: 1,
        borderColor: 'divider',
        bgcolor: theme.palette.mode === 'dark' ? 'rgba(0,0,0,0.2)' : 'rgba(0,0,0,0.04)',
      }}>
        <Typography variant="caption" color="text.secondary" sx={{ fontSize: 10 }}>
          Signals: {visibleCount} / {signalKeys.length}
        </Typography>
        <Box sx={{
          width: 8,
          height: 8,
          borderRadius: '50%',
          bgcolor: followLive ? 'success.main' : 'text.disabled',
        }} />
      </Box>
    </Box>
  );
}
