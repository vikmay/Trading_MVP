import { useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import {
    LineChart,
    Line,
    XAxis,
    YAxis,
    CartesianGrid,
    Tooltip,
    Dot,
} from 'recharts';
import './TickerWS.css';

/**
 * BTC/USDT live ticker fed from SignalR (gateway → RabbitMQ → browser)
 * Now with a scrolling price chart and gap-highlight.
 */
export default function TickerWS() {
    const [tick, setTick] = useState(null);
    const [points, setPoints] = useState([]);
    const [isConnected, setConnected] = useState(false);
    const [error, setError] = useState(null);
    const [lastBid, setLastBid] = useState(null);
    const [flash, setFlash] = useState(null);
    const [lastFlashAt, setLastFlashAt] = useState(0);

    const lastSeqRef = useRef(0); // 🔹 track highest Seq

    // ─────────────────────────────────── SignalR ──────────────────────────
    useEffect(() => {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl('http://localhost:8080/hub/market')
            .withAutomaticReconnect()
            .build();

        // unified handler to pull backlog after connect / reconnect
        const pullBacklog = async () => {
            try {
                const missed = await connection.invoke(
                    'NeedTicksSince',
                    lastSeqRef.current
                ); // returns RawTick[]

                if (Array.isArray(missed) && missed.length) {
                    setPoints(
                        (p) => [...p, ...missed.map(mapDto)].slice(-1000) // keep last 1k
                    );
                    lastSeqRef.current = missed[missed.length - 1].Seq;
                }
            } catch (e) {
                console.warn('No backlog available', e);
            }
        };

        const start = async () => {
            try {
                await connection.start();
                console.log('✅ SignalR connected');
                setConnected(true);
                setError(null);
                pullBacklog();
            } catch (e) {
                console.error('❌ SignalR connection error', e);
                setError('Failed to connect to market hub');
                setConnected(false);
                setTimeout(start, 5_000);
            }
        };

        const mapDto = (dto) => ({
            seq: dto.Seq ?? dto.seq,
            bid: dto.Bid ?? dto.bid,
            ask: dto.Ask ?? dto.ask,
            mid: dto.Mid ?? dto.mid,
            tsMs: dto.TsMs ?? dto.tsMs,
        });

        connection.on('tick', (raw) => {
            const obj = typeof raw === 'string' ? JSON.parse(raw) : raw;
            const dto = mapDto(obj);

            // gap detector
            const expectedSeq = lastSeqRef.current + 1;
            const outOfOrder =
                dto.seq !== expectedSeq && lastSeqRef.current !== 0;
            lastSeqRef.current = dto.seq;

            setPoints(
                (p) => [...p, { ...dto, outOfOrder }].slice(-1000) // max 1 000 pts
            );

            setTick(dto);
        });

        connection.onreconnected(() => {
            setConnected(true);
            pullBacklog();
        });

        connection.onclose(() => {
            setConnected(false);
            setTimeout(start, 5_000);
        });

        start();
        return () => void connection.stop();
    }, []);
    // ───────────────────────────────────────────────────────────────────────

    // ───────── flashing row background when bid changes ───────────────────
    useEffect(() => {
        if (!tick) return;
        const now = Date.now();
        if (
            now - lastFlashAt > 500 &&
            lastBid !== null &&
            tick.bid !== lastBid
        ) {
            setFlash(tick.bid > lastBid ? 'green' : 'red');
            setLastFlashAt(now);
            setTimeout(() => setFlash(null), 800);
        }
        setLastBid(tick.bid);
    }, [tick, lastBid, lastFlashAt]);
    // ───────────────────────────────────────────────────────────────────────

    const fmt = (n) =>
        new Intl.NumberFormat('en-US', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2,
        }).format(n);

    return (
        <div className="ticker flex flex-col gap-6 p-4">
            {!isConnected && (
                <div className="disconnected-banner text-red-600 font-medium">
                    🔴 Disconnected from market data. Attempting to reconnect…
                </div>
            )}

            {error && <div className="error-banner text-red-500">{error}</div>}

            {/* ─── Price panel ───────────────────────────────────────── */}
            {tick && (
                <div
                    className={`ticker-data rounded-xl shadow p-4 transition-colors duration-300 ${
                        flash === 'green'
                            ? 'bg-green-100'
                            : flash === 'red'
                            ? 'bg-red-100'
                            : ''
                    }`}
                >
                    <h2 className="text-xl font-bold mb-3">
                        {tick.symbol ?? 'BTC/USDT'}
                    </h2>

                    <table className="price-table w-full text-right mb-2">
                        <thead className="text-sm text-gray-500">
                            <tr>
                                <th>Bid</th>
                                <th>Ask</th>
                                <th>Mid</th>
                                <th>Spread&nbsp;%</th>
                            </tr>
                        </thead>
                        <tbody className="text-lg">
                            <tr>
                                <td>
                                    {tick.bid != null
                                        ? `$${fmt(tick.bid)}`
                                        : 'N/A'}
                                </td>
                                <td>
                                    {tick.ask != null
                                        ? `$${fmt(tick.ask)}`
                                        : 'N/A'}
                                </td>
                                <td>
                                    {tick.mid != null
                                        ? `$${fmt(tick.mid)}`
                                        : 'N/A'}
                                </td>
                                <td>
                                    {tick.spreadPct != null
                                        ? `${fmt(tick.spreadPct)} %`
                                        : 'N/A'}
                                </td>
                            </tr>
                        </tbody>
                    </table>

                    <div className="timestamp text-sm text-gray-500">
                        Last update:{' '}
                        {tick.tsMs
                            ? new Date(tick.tsMs).toLocaleTimeString()
                            : 'N/A'}
                    </div>
                </div>
            )}

            {/* ─── Price chart ──────────────────────────────────────── */}
            {points.length > 1 && (
                <LineChart
                    width={800}
                    height={360}
                    data={points}
                    margin={{ top: 10, right: 30, left: 0, bottom: 0 }}
                >
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis
                        dataKey="seq"
                        tickFormatter={(v) => (v % 5 === 0 ? v : '')}
                        domain={['auto', 'auto']}
                    />
                    <YAxis domain={['auto', 'auto']} />
                    <Tooltip />
                    <Line
                        type="monotone"
                        dataKey="mid"
                        dot={(p) => (
                            <Dot
                                {...p}
                                r={p.payload.outOfOrder ? 5 : 3}
                                stroke={
                                    p.payload.outOfOrder ? 'red' : undefined
                                }
                                fill={p.payload.outOfOrder ? 'red' : undefined}
                            />
                        )}
                        isAnimationActive={false}
                        stroke="#3b82f6"
                        strokeWidth={2}
                    />
                </LineChart>
            )}
        </div>
    );
}
