import TickerWS from './TickerWS';
import './App.css';

function App() {
    return (
        <div className="container">
            <header>
                <h1>Trading Dashboard</h1>
            </header>

            <main>
                <div className="ticker-container">
                    <TickerWS />
                </div>
            </main>

            <footer>
                <p>Trading MVP Dashboard</p>
            </footer>
        </div>
    );
}

export default App;
