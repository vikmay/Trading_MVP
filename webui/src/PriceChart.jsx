// src/PriceChart.jsx  ────────────────────────────────────────────────
import {
    AreaChart,
    Area,
    XAxis,
    YAxis,
    CartesianGrid,
    Tooltip,
    ResponsiveContainer,
    Brush,
    ReferenceLine,
} from 'recharts';

/**
 * Exchange-style price chart with enhanced visualization.
 *
 * Props:
 * • data      – array [{ tsMs, mid, outOfOrder, … }]
 * • paused    – bool; when false we keep a sliding live window
 * • windowMs  – length of the live window (default 2 min)
 * • autoScroll – whether to auto-scroll to latest data
 * • setAutoScroll – callback to update auto-scroll state
 */
export default function PriceChart({
    data = [], // ← safe default, never undefined
    paused = false, // ← safe default
    windowMs = 120_000, // 2 minutes
    autoScroll = true,
    setAutoScroll = () => {},
}) {
    // Trim to the live window unless we're paused
    const now = Date.now();
    let slice;

    if (paused) {
        slice = data;
    } else {
        // For live mode, show recent data but ensure we always have enough points
        const recentData = data.filter((p) => p.tsMs >= now - windowMs);
        if (recentData.length >= 2) {
            slice = recentData;
        } else {
            // If not enough recent data, show the last N points
            slice = data.slice(-Math.max(10, data.length));
        }

        // Reset out-of-order flags for live data to clean up red dots
        slice = slice.map((point) => ({ ...point, outOfOrder: false }));
    }

    if (slice.length < 2) {
        return (
            <div
                style={{
                    flex: 1,
                    minHeight: '150px',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    background: 'rgba(255,255,255,0.02)',
                    borderRadius: '8px',
                    border: '1px dashed rgba(255,255,255,0.1)',
                }}
            >
                <div style={{ textAlign: 'center', color: '#8e8e93' }}>
                    📊 Collecting price data...
                    <br />
                    <small>
                        Need at least 2 points to draw chart ({slice.length}/2)
                    </small>
                </div>
            </div>
        );
    }

    /* ── helpers ────────────────────────────────────────────── */
    const fmtTick = (ts) =>
        new Date(ts).toLocaleTimeString([], {
            hour12: false,
            minute: '2-digit',
            second: '2-digit',
        });

    const fmtTooltipLabel = (ts) =>
        new Date(ts).toLocaleTimeString([], { hour12: false });

    /* ── render ─────────────────────────────────────────────── */
    return (
        <div
            style={{
                width: '100%',
                flex: 1,
                minHeight: '200px',
                background: 'rgba(255,255,255,0.02)',
                borderRadius: '8px',
                padding: '0.5rem',
                display: 'flex',
                flexDirection: 'column',
            }}
        >
            <ResponsiveContainer width="100%" height="100%">
                <AreaChart
                    data={slice}
                    margin={{ top: 10, right: 20, left: 10, bottom: 60 }}
                    onMouseDown={() => setAutoScroll(false)}
                    onTouchStart={() => setAutoScroll(false)}
                >
                    {/* Enhanced gradient fill */}
                    <defs>
                        <linearGradient
                            id="priceFill"
                            x1="0"
                            y1="0"
                            x2="0"
                            y2="1"
                        >
                            <stop
                                offset="0%"
                                stopColor="#0a84ff"
                                stopOpacity={0.4}
                            />
                            <stop
                                offset="50%"
                                stopColor="#0a84ff"
                                stopOpacity={0.2}
                            />
                            <stop
                                offset="100%"
                                stopColor="#0a84ff"
                                stopOpacity={0.05}
                            />
                        </linearGradient>

                        <linearGradient
                            id="strokeGradient"
                            x1="0%"
                            y1="0%"
                            x2="100%"
                            y2="0%"
                        >
                            <stop offset="0%" stopColor="#0a84ff" />
                            <stop offset="100%" stopColor="#34c759" />
                        </linearGradient>
                    </defs>

                    <CartesianGrid
                        stroke="rgba(255,255,255,0.08)"
                        strokeDasharray="3 3"
                        vertical={false}
                    />

                    <XAxis
                        dataKey="tsMs"
                        type="number"
                        scale="time"
                        domain={['dataMin', 'dataMax']}
                        tickFormatter={fmtTick}
                        tick={{ fill: '#8e8e93', fontSize: 11 }}
                        axisLine={{ stroke: 'rgba(255,255,255,0.1)' }}
                        tickLine={{ stroke: 'rgba(255,255,255,0.1)' }}
                    />

                    <YAxis
                        domain={['auto', 'auto']}
                        tick={{ fill: '#8e8e93', fontSize: 11 }}
                        tickFormatter={(v) => `$${v.toLocaleString('en-US')}`}
                        width={80}
                        axisLine={{ stroke: 'rgba(255,255,255,0.1)' }}
                        tickLine={{ stroke: 'rgba(255,255,255,0.1)' }}
                    />

                    <Tooltip
                        contentStyle={{
                            background: '#2a2a2a',
                            border: '1px solid rgba(255,255,255,0.2)',
                            borderRadius: '8px',
                            color: '#fff',
                        }}
                        itemStyle={{ color: '#0a84ff' }}
                        labelFormatter={fmtTooltipLabel}
                        formatter={(value) => [
                            `$${value.toFixed(2)}`,
                            'Mid Price',
                        ]}
                    />

                    <Area
                        type="monotone"
                        dataKey="mid"
                        stroke="url(#strokeGradient)"
                        strokeWidth={2.5}
                        fill="url(#priceFill)"
                        dot={(props) => {
                            if (props.payload?.outOfOrder) {
                                return (
                                    <circle
                                        cx={props.cx}
                                        cy={props.cy}
                                        r={5}
                                        fill="#ff3b30"
                                        stroke="#fff"
                                        strokeWidth={2}
                                    />
                                );
                            }
                            return null;
                        }}
                        isAnimationActive={!paused}
                        animationDuration={300}
                    />

                    {/* Add brush for scrolling/zooming when paused */}
                    {paused && slice.length > 20 && (
                        <Brush
                            dataKey="tsMs"
                            height={30}
                            stroke="#0a84ff"
                            tickFormatter={(ts) =>
                                new Date(ts).toLocaleTimeString([], {
                                    hour: '2-digit',
                                    minute: '2-digit',
                                })
                            }
                        />
                    )}
                </AreaChart>
            </ResponsiveContainer>

            {/* Chart info footer */}
            <div
                style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    marginTop: '0.5rem',
                    fontSize: '0.8rem',
                    color: '#8e8e93',
                    flexShrink: 0,
                }}
            >
                <span>
                    📊 Showing {slice.length} data points
                    {!paused && ` (last ${(windowMs / 60000).toFixed(1)}min)`}
                    {paused &&
                        slice.length > 20 &&
                        ' - Use brush below to scroll'}
                </span>
                <span>
                    {slice.some((p) => p.outOfOrder) &&
                        '🔴 Red dots = out-of-order data'}
                    {paused ? ' 📌 Paused view' : ' 🟢 Live mode'}
                </span>
            </div>
        </div>
    );
}
