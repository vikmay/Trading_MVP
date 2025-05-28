import {
    LineChart,
    Line,
    XAxis,
    YAxis,
    CartesianGrid,
    Tooltip,
    Dot,
    Brush,
    ResponsiveContainer,
} from 'recharts';

/**
 * Time-series price chart.
 *
 * Props
 * ─────
 * points           Array<{ tsMs, mid, outOfOrder }>
 * paused           Boolean – if `true` we show the Brush so the
 *                  user can scroll/zoom history; hidden in live mode.
 * onBrushPause     () ⇒ void – called *once* when user moves the Brush
 *                  while we’re live (parent can switch to paused mode).
 * windowMs         How much history (ms) to show when live.
 */
export default function PriceChart({
    points,
    paused,
    onBrushPause,
    windowMs = 120_000,
}) {
    /* ─── X-axis domain ───────────────────────────────────────────── */
    const now = Date.now();
    const domainLive = [now - windowMs, now]; // fixed sliding window
    const domainHist = ['dataMin', 'dataMax']; // zoomable

    /* ─── render ──────────────────────────────────────────────────── */
    return (
        <ResponsiveContainer width="100%" height={360}>
            <LineChart
                data={points}
                margin={{ top: 10, right: 30, left: 0, bottom: 0 }}
            >
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis
                    dataKey="tsMs"
                    type="number"
                    scale="time"
                    domain={paused ? domainHist : domainLive}
                    allowDataOverflow
                    tickFormatter={(t) =>
                        new Date(t).toLocaleTimeString([], {
                            minute: '2-digit',
                            second: '2-digit',
                        })
                    }
                />
                <YAxis domain={['auto', 'auto']} />
                <Tooltip
                    labelFormatter={(t) =>
                        new Date(t).toLocaleTimeString([], { hour12: false })
                    }
                />

                <Line
                    type="monotone"
                    dataKey="mid"
                    stroke="#3b82f6"
                    strokeWidth={2}
                    isAnimationActive={false}
                    dot={(p) => (
                        <Dot
                            {...p}
                            r={p.payload?.outOfOrder ? 5 : 3}
                            fill={p.payload?.outOfOrder ? 'red' : undefined}
                        />
                    )}
                />

                {/* Brush only makes sense when the feed is *not* live */}
                {paused && (
                    <Brush
                        dataKey="tsMs"
                        height={20}
                        travellerWidth={10}
                        stroke="#3b82f6"
                        tickFormatter={(t) =>
                            new Date(t).toLocaleTimeString([], {
                                second: '2-digit',
                            })
                        }
                    />
                )}

                {/* Invisible Brush that simply detects a drag while live.
              If the user drags, we call onBrushPause() exactly once.          */}
                {!paused && onBrushPause && (
                    <Brush dataKey="tsMs" height={0} onChange={onBrushPause} />
                )}
            </LineChart>
        </ResponsiveContainer>
    );
}
