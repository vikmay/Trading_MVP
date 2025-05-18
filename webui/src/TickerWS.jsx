import { useEffect, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import './TickerWS.css';

function TickerWS() {
    const [tick, setTick] = useState(null);
    const [isConnected, setIsConnected] = useState(false);
    const [error, setError] = useState(null);
    const [lastBid, setLastBid] = useState(null);
    const [flash, setFlash] = useState(null);
    const [lastFlashTime, setLastFlashTime] = useState(0);

    useEffect(() => {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl('http://localhost:8080/hub/market')
            .withAutomaticReconnect()
            .build();

        async function startConnection() {
            try {
                await connection.start();
                console.log('SignalR Connected');
                setIsConnected(true);
                setError(null);
            } catch (err) {
                console.error('SignalR Connection Error:', err);
                setError('Failed to connect to market hub');
                setIsConnected(false);
                setTimeout(startConnection, 5000);
            }
        }

        connection.on('tick', (newTick) => {
            console.log('Received tick:', newTick);
            setTick(newTick);
        });

        connection.onclose(() => {
            console.log('SignalR Disconnected');
            setIsConnected(false);
            setTimeout(startConnection, 5000);
        });

        startConnection();

        return () => {
            connection.stop();
        };
    }, []);

    useEffect(() => {
        if (tick) {
            const now = Date.now();
            // Перевіряємо, чи пройшло достатньо часу з останнього flash
            if (now - lastFlashTime > 500) {
                if (lastBid !== null && tick.bid !== lastBid) {
                    setFlash(tick.bid > lastBid ? 'green' : 'red');
                    setLastFlashTime(now);
                    setTimeout(() => setFlash(null), 800); // Довший час відображення
                }
                setLastBid(tick.bid);
            }
        }
    }, [tick, lastBid, lastFlashTime]);

    const formatNumber = (number) => {
        return new Intl.NumberFormat('en-US', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2,
        }).format(number);
    };

    return (
        <div className="ticker">
            {!isConnected && (
                <div className="disconnected-banner">
                    🔴 Disconnected from market data. Attempting to reconnect...
                </div>
            )}

            {error && <div className="error-banner">{error}</div>}

            <div className="ticker-status">
                Status: {isConnected ? '🟢 Connected' : '🔴 Disconnected'}
            </div>

            {tick && (
                <div className={`ticker-data ${flash}`}>
                    <h2>BTC/USDT</h2>
                    <table className="price-table">
                        <thead>
                            <tr>
                                <th>Bid</th>
                                <th>Ask</th>
                                <th>Mid</th>
                                <th>Spread %</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr>
                                <td>
                                    $
                                    {tick.bid != null
                                        ? formatNumber(tick.bid)
                                        : 'N/A'}
                                </td>
                                <td>
                                    $
                                    {tick.ask != null
                                        ? formatNumber(tick.ask)
                                        : 'N/A'}
                                </td>
                                <td>
                                    $
                                    {tick.mid != null
                                        ? formatNumber(tick.mid)
                                        : 'N/A'}
                                </td>
                                <td>
                                    {tick.spreadPct != null
                                        ? formatNumber(tick.spreadPct)
                                        : 'N/A'}
                                    %
                                </td>
                            </tr>
                        </tbody>
                    </table>
                    <div className="timestamp">
                        Last update:{' '}
                        {tick.tsMs != null
                            ? new Date(tick.tsMs).toLocaleTimeString()
                            : 'N/A'}
                    </div>
                </div>
            )}
        </div>
    );
}

export default TickerWS;
