import TickerWS from './TickerWS';
import './App.css';

function App() {
    return (
        <div className="container">
            <header>
                <h1>🚀 Trading Dashboard MVP</h1>
                <p>Real-time cryptocurrency market data streaming</p>
            </header>

            <main>
                <div className="ticker-container">
                    <TickerWS />
                </div>
            </main>

            <footer>
                <p>
                    🏗️ Trading MVP Dashboard | Microservices Architecture Demo
                </p>
            </footer>
        </div>
    );
}

export default App;
