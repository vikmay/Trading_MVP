// src/TickerWS.jsx  ─────────────────────────────────────────────────────────
import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import PriceChart from './PriceChart';
import { useAuth } from './useAuth';
import './TickerWS.css';

export default function TickerWS() {
    /* ───────── reactive state ───────── */
    const [points, setPoints] = useState([]);
    const [queued, setQueued] = useState([]); // held while paused
    const [tick, setTick] = useState(null);

    const [connected, setConnected] = useState(false);
    const [error, setError] = useState(null);
    const [paused, setPaused] = useState(false);
    const [autoScroll, setAutoScroll] = useState(true); // false after manual pan
    const [userInfo, setUserInfo] = useState(null);

    const lastSeqRef = useRef(0);
    const lastBidRef = useRef(null);
    const [flashBid, setFlashBid] = useState('');
    const [flashAsk, setFlashAsk] = useState('');

    const { token, isAuthenticated } = useAuth();

    /* ───────── helpers ───────── */
    const fmt = (n) =>
        new Intl.NumberFormat('en-US', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2,
        }).format(n);

    const mapDto = (d) => ({
        seq: d.Seq ?? d.seq,
        bid: d.Bid ?? d.bid,
        ask: d.Ask ?? d.ask,
        mid: d.Mid ?? d.mid,
        tsMs: d.TsMs ?? d.tsMs,
        spreadPct:
            (((d.ask ?? d.Ask) - (d.bid ?? d.Bid)) /
                (((d.bid ?? d.Bid) + (d.ask ?? d.Ask)) / 2)) *
            100,
    });

    /* ───────── push a new point ───────── */
    const pushPoint = useCallback(
        (dto) => {
            const expect = lastSeqRef.current + 1;
            const outOfOrder = dto.seq !== expect && lastSeqRef.current !== 0;
            lastSeqRef.current = dto.seq;

            const target = paused ? setQueued : setPoints;
            target((arr) => [...arr, { ...dto, outOfOrder }]);
            if (!paused) setTick(dto);
        },
        [paused]
    ); /* ───────── SignalR wiring ───────── */
    useEffect(() => {
        const signalRUrl =
            import.meta.env.VITE_SIGNALR_URL ||
            'http://localhost:8080/hub/market';

        // Build the URL with token as query parameter if available
        const urlWithToken = token
            ? `${signalRUrl}?access_token=${token}`
            : signalRUrl;

        const hubBuilder = new signalR.HubConnectionBuilder()
            .withUrl(urlWithToken, {
                accessTokenFactory: () => token || '',
            })
            .withAutomaticReconnect();

        const hub = hubBuilder.build();

        const subscribeFrom = async () => {
            try {
                const backlog = await hub.invoke(
                    'NeedTicksSince',
                    lastSeqRef.current
                );
                if (Array.isArray(backlog) && backlog.length) {
                    const mapped = backlog.map(mapDto);
                    lastSeqRef.current = mapped[mapped.length - 1].seq;
                    setPoints((p) => [...p, ...mapped]);
                }
            } catch (err) {
                console.warn('SubscribeFrom failed', err);
            }
        };

        const getUserInfo = async () => {
            if (isAuthenticated) {
                try {
                    const info = await hub.invoke('GetUserInfo');
                    setUserInfo(info);
                } catch (err) {
                    console.warn('GetUserInfo failed', err);
                }
            }
        };

        const joinUserGroup = async () => {
            if (isAuthenticated) {
                try {
                    await hub.invoke('JoinUserGroup');
                    console.log('Joined user-specific group');
                } catch (err) {
                    console.warn('JoinUserGroup failed', err);
                }
            }
        };
        const start = async () => {
            try {
                console.log(
                    'Starting SignalR connection with token:',
                    token ? 'Present' : 'Missing'
                );
                await hub.start();
                setConnected(true);
                setError(null);
                subscribeFrom();
                getUserInfo();
                joinUserGroup();
            } catch (err) {
                console.error('Hub connection failed', err);
                setConnected(false);
                setError('Hub connection failed');
                setTimeout(start, 5_000);
            }
        };

        hub.on('tick', (raw) =>
            pushPoint(mapDto(typeof raw === 'string' ? JSON.parse(raw) : raw))
        );

        hub.onreconnected(() => {
            setConnected(true);
            subscribeFrom();
            getUserInfo();
            joinUserGroup();
        });
        hub.onclose(() => {
            setConnected(false);
            setTimeout(start, 5_000);
        });

        start();
        return () => void hub.stop();
    }, [pushPoint, token, isAuthenticated]);

    /* ───────── flash numbers only ───────── */
    useEffect(() => {
        if (!tick) return;
        if (lastBidRef.current != null && tick.bid !== lastBidRef.current) {
            setFlashBid(tick.bid > lastBidRef.current ? 'green' : 'red');
            setTimeout(() => setFlashBid(''), 500);
        }
        if (lastBidRef.current != null && tick.ask !== lastBidRef.current) {
            setFlashAsk(tick.ask > lastBidRef.current ? 'green' : 'red');
            setTimeout(() => setFlashAsk(''), 500);
        }
        lastBidRef.current = tick.bid;
    }, [tick]);

    /* ───────── pause / resume ───────── */
    const togglePause = () => {
        if (paused) {
            // resume
            setPoints((p) => [...p, ...queued]);
            if (queued.length) setTick(queued[queued.length - 1]);
            setQueued([]);
            setAutoScroll(true);
        }
        setPaused((v) => !v);
    }; /* ───────── render ───────── */
    return (
        <div className="ticker">
            {' '}
            {/* ─── status bar & controls ──────────────────────────────── */}
            <div className="status-bar">
                <div
                    className={`connection-status ${
                        connected ? 'connected' : 'disconnected'
                    }`}
                >
                    {connected ? '🟢 Connected' : '🔴 Disconnected'}
                    {!connected && ' - reconnecting...'}
                </div>

                {isAuthenticated && userInfo && (
                    <div className="connection-status connected">
                        👤 {userInfo.userId} (authenticated)
                    </div>
                )}

                {!isAuthenticated && (
                    <div className="connection-status disconnected">
                        👤 Anonymous (not authenticated)
                    </div>
                )}

                {error && <div className="error-banner">⚠️ {error}</div>}

                <button className="btn-primary" onClick={togglePause}>
                    {paused ? '▶️ Resume Live' : '⏸️ Pause'}
                </button>

                {queued.length > 0 && (
                    <div className="connection-status disconnected">
                        📊 {queued.length} updates queued
                    </div>
                )}
            </div>
            {/* ─── price table ─────────────────────────────────────────── */}
            {tick && (
                <div className="ticker-data">
                    <h2>💰 {tick.symbol || 'BTC/USDT'}</h2>
                    <table className="price-table">
                        <thead>
                            <tr>
                                <th>Bid</th>
                                <th>Ask</th>
                                <th>Mid Price</th>
                                <th>Spread %</th>
                                <th>Sequence</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr>
                                <td className={flashBid}>${fmt(tick.bid)}</td>
                                <td className={flashAsk}>${fmt(tick.ask)}</td>
                                <td>${fmt(tick.mid)}</td>
                                <td>{fmt(tick.spreadPct)}%</td>
                                <td>#{tick.seq}</td>
                            </tr>
                        </tbody>
                    </table>
                    <div className="timestamp">
                        📅 Last update:{' '}
                        {new Date(tick.tsMs).toLocaleTimeString()}
                        {tick.outOfOrder && (
                            <span
                                style={{ color: '#ff3b30', marginLeft: '1rem' }}
                            >
                                ⚠️ Out of order!
                            </span>
                        )}
                    </div>
                </div>
            )}
            {/* ─── price chart ─────────────────────────────────────────── */}
            <div className="chart-container">
                {' '}
                <div className="chart-header">
                    <h3>📈 Real-time Price Chart</h3>
                    <div className="chart-controls">
                        <button
                            className={`btn-secondary ${
                                autoScroll ? 'active' : ''
                            }`}
                            onClick={() => setAutoScroll(!autoScroll)}
                            disabled={!paused}
                        >
                            {autoScroll ? '🔄 Auto-scroll' : '📌 Manual'}
                        </button>
                        <span className="btn-secondary">
                            📊 {points.length} points
                        </span>
                        {paused && (
                            <span className="btn-secondary active">
                                📌 Paused - Scroll with brush below
                            </span>
                        )}
                        {!paused && (
                            <span className="btn-secondary active">
                                🟢 Live - Click/drag to pause & scroll
                            </span>
                        )}
                    </div>
                </div>
                {points.length > 1 ? (
                    <PriceChart
                        data={points}
                        paused={paused}
                        autoScroll={autoScroll}
                        setAutoScroll={setAutoScroll}
                    />
                ) : (
                    <div
                        style={{
                            flex: 1,
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            color: '#8e8e93',
                            fontSize: '1.1rem',
                            background: 'rgba(255,255,255,0.02)',
                            borderRadius: '8px',
                            border: '1px dashed rgba(255,255,255,0.1)',
                        }}
                    >
                        📡 Waiting for price data... ({points.length} points
                        collected)
                    </div>
                )}
            </div>
        </div>
    );
}
