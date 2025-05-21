import { useEffect, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import './TickerWS.css';

/**
 * BTC/USDT live ticker fed from SignalR (gateway → RabbitMQ → browser)
 */
export default function TickerWS() {
    const [tick, setTick] = useState(null);
    const [isConnected, setConnected] = useState(false);
    const [error, setError] = useState(null);
    const [lastBid, setLastBid] = useState(null);
    const [flash, setFlash] = useState(null);
    const [lastFlashAt, setLastFlashAt] = useState(0);

    // ───────────────────────────────────────────────────────── SignalR ─┐
    useEffect(() => {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl('http://localhost:8080/hub/market')
            .withAutomaticReconnect()
            .build();

        const start = async () => {
            try {
                await connection.start();
                console.log('✅ SignalR connected');
                setConnected(true);
                setError(null);
            } catch (e) {
                console.error('❌ SignalR connection error', e);
                setError('Failed to connect to market hub');
                setConnected(false);
                setTimeout(start, 5_000);
            }
        };

        // payload normalisation -> nice camel‑cased object for the UI
        connection.on('tick', (raw) => {
            const obj = typeof raw === 'string' ? JSON.parse(raw) : raw;
            setTick({
                symbol: obj.Symbol,
                bid: obj.Bid,
                ask: obj.Ask,
                mid: obj.Mid,
                spreadPct: obj.SpreadPct,
                tsMs: obj.TsMs,
            });
        });

        connection.onclose(() => {
            setConnected(false);
            setTimeout(start, 5_000);
        });

        start();
        return () => void connection.stop();
    }, []);
    // ─────────────────────────────────────────────────────────────────────┘

    // ─────────── flashing row background when bid changes ───────────────┐
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
    // ─────────────────────────────────────────────────────────────────────┘

    const fmt = (n) =>
        new Intl.NumberFormat('en-US', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2,
        }).format(n);

    return (
        <div className="ticker flex flex-col gap-4 p-4">
            {!isConnected && (
                <div className="disconnected-banner text-red-600 font-medium">
                    🔴 Disconnected from market data. Attempting to reconnect…
                </div>
            )}

            {error && <div className="error-banner text-red-500">{error}</div>}

            <div className="ticker-status font-semibold">
                Status: {isConnected ? '🟢 Connectedddddd' : '🔴 Disconnected'}
            </div>

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
                    <h2 className="text-xl font-bold mb-2">
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
        </div>
    );
}
