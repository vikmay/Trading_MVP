// src/TickerWS.jsx  ───────────────────────────────────────────────────────────
import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import {
    LineChart,
    Line,
    XAxis,
    YAxis,
    CartesianGrid,
    Tooltip,
    Brush,
    Dot,
} from 'recharts';
import './TickerWS.css';

const LIVE_WINDOW_MS = 120_000; // 2-minute look-back
const MAX_POINTS = 1_500; // keep memory bounded

export default function TickerWS() {
    const [points, setPoints] = useState([]); // all received points
    const [queue, setQueue] = useState([]); // buffered while paused
    const [tick, setTick] = useState(null); // latest live point

    const [paused, setPaused] = useState(false);
    const [connected, setConn] = useState(false);
    const [error, setErr] = useState(null);

    const lastSeq = useRef(0);
    const prevBid = useRef(null);
    const prevAsk = useRef(null);

    /* ───────── helpers ───────── */
    const nf = (n, d = 2) =>
        Number.isFinite(n)
            ? n.toLocaleString('en-US', {
                  minimumFractionDigits: d,
                  maximumFractionDigits: d,
              })
            : '—';

    const tsLabel = (ts) =>
        new Date(ts).toLocaleTimeString([], { hour12: false });

    const mapDto = (d) => ({
        seq: d.Seq ?? d.seq,
        bid: d.Bid ?? d.bid,
        ask: d.Ask ?? d.ask,
        mid: d.Mid ?? d.mid,
        tsMs: d.TsMs ?? d.tsMs,
        spreadPct: d.SpreadPct ?? d.spreadPct,
    });

    /* pushPoint: put dto into live list or pause-queue */
    const pushPoint = useCallback(
        (dto) => {
            const expected = lastSeq.current + 1;
            const outOfOrder = dto.seq !== expected && lastSeq.current !== 0;
            lastSeq.current = dto.seq;

            const mid = Number.isFinite(dto.mid)
                ? dto.mid
                : (dto.bid + dto.ask) / 2;
            const spreadPct = Number.isFinite(dto.spreadPct)
                ? dto.spreadPct
                : ((dto.ask - dto.bid) / mid) * 100;

            const p = { ...dto, mid, spreadPct, outOfOrder };

            (paused ? setQueue : setPoints)((arr) =>
                [...arr, p].slice(-MAX_POINTS)
            );
            if (!paused) setTick(p);
        },
        [paused]
    );

    /* ───────── SignalR setup ───────── */
    useEffect(() => {
        const hub = new signalR.HubConnectionBuilder()
            .withUrl('http://localhost:8080/hub/market')
            .withAutomaticReconnect()
            .build();

        const sub = () =>
            hub.invoke('SubscribeFrom', lastSeq.current).catch(() => {});

        const start = () =>
            hub
                .start()
                .then(() => {
                    setConn(true);
                    setErr(null);
                    sub();
                })
                .catch(() => {
                    setConn(false);
                    setErr('hub connect failed');
                    setTimeout(start, 5_000);
                });

        hub.on('tick', (raw) =>
            pushPoint(mapDto(typeof raw === 'string' ? JSON.parse(raw) : raw))
        );
        hub.onreconnected(() => {
            setConn(true);
            sub();
        });
        hub.onclose(() => {
            setConn(false);
            setTimeout(start, 5_000);
        });

        start();
        return () => hub.stop();
    }, [pushPoint]);

    /* pause / resume */
    const togglePause = () => {
        if (paused) {
            setPoints((p) => [...p, ...queue].slice(-MAX_POINTS));
            if (queue.length) setTick(queue[queue.length - 1]);
            setQueue([]);
        }
        setPaused((p) => !p);
    };

    /* flash util – returns class name & updates ref */
    const flash = (val, ref) => {
        let cls = '';
        if (ref.current !== null && val !== ref.current) {
            cls = val > ref.current ? 'flash-green' : 'flash-red';
        }
        ref.current = val; // update *after* comparison
        return cls;
    };

    /* data actually fed to chart (live window unless paused) */
    const now = Date.now();
    const chartData = paused
        ? points
        : points.filter((pt) => pt.tsMs >= now - LIVE_WINDOW_MS);

    /* ───────── render ───────── */
    return (
        <div className="ticker">
            <header className="toolbar">
                <button className="btn" onClick={togglePause}>
                    {paused ? 'Go live' : 'Pause'}
                </button>
                {!connected && <span className="badge red">Disconnected…</span>}
                {error && <span className="badge orange">{error}</span>}
            </header>

            {/* price panel */}
            {tick && (
                <div className="price-card">
                    <h2>{tick.symbol ?? 'BTC/USDT'}</h2>
                    <table className="price-table">
                        <thead>
                            <tr>
                                <th>Bid</th>
                                <th>Ask</th>
                                <th>Mid</th>
                                <th>Spread&nbsp;%</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr>
                                <td className={flash(tick.bid, prevBid)}>
                                    {nf(tick.bid)}
                                </td>
                                <td className={flash(tick.ask, prevAsk)}>
                                    {nf(tick.ask)}
                                </td>
                                <td>{nf(tick.mid)}</td>
                                <td>{nf(tick.spreadPct)}</td>
                            </tr>
                        </tbody>
                    </table>
                    <div className="timestamp">Last: {tsLabel(tick.tsMs)}</div>
                </div>
            )}

            {/* chart */}
            {chartData.length > 1 && (
                <LineChart
                    width={800}
                    height={360}
                    data={chartData}
                    margin={{ top: 10, right: 30, left: 0, bottom: 0 }}
                >
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis
                        type="number"
                        dataKey="tsMs"
                        scale="time"
                        domain={['dataMin', 'dataMax']}
                        tickFormatter={tsLabel}
                    />
                    <YAxis domain={['auto', 'auto']} />
                    <Tooltip labelFormatter={tsLabel} />
                    <Line
                        type="monotone"
                        dataKey="mid"
                        stroke="#3b82f6"
                        strokeWidth={2}
                        isAnimationActive={false}
                        dot={(p) => (
                            <Dot
                                {...p}
                                r={p.payload.outOfOrder ? 5 : 3}
                                fill={p.payload.outOfOrder ? 'red' : undefined}
                            />
                        )}
                    />
                    <Brush
                        dataKey="tsMs"
                        height={20}
                        stroke="#3b82f6"
                        travellerWidth={10}
                        tickFormatter={tsLabel}
                        onChange={() => !paused && setPaused(true)}
                    />
                </LineChart>
            )}
        </div>
    );
}
