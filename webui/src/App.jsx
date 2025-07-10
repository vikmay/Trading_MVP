import TickerWS from './TickerWS';
import LoginForm from './LoginForm';
import UserInfo from './UserInfo';
import { AuthProvider } from './AuthContext';
import { useAuth } from './useAuth';
import './App.css';

function AppContent() {
    const { isAuthenticated, loading } = useAuth();

    if (loading) {
        return (
            <div className="container">
                <div style={{ 
                    display: 'flex', 
                    justifyContent: 'center', 
                    alignItems: 'center', 
                    height: '100vh',
                    fontSize: '1.2rem'
                }}>
                    🔄 Loading...
                </div>
            </div>
        );
    }

    return (
        <div className="container">
            <header>
                <h1>🚀 Trading Dashboard MVP</h1>
                <p>Real-time cryptocurrency market data streaming</p>
                {isAuthenticated && <UserInfo />}
            </header>

            <main>
                {isAuthenticated ? (
                    <div className="ticker-container">
                        <TickerWS />
                    </div>
                ) : (
                    <LoginForm />
                )}
            </main>

            <footer>
                <p>
                    🏗️ Trading MVP Dashboard | Microservices Architecture Demo
                </p>
            </footer>
        </div>
    );
}

function App() {
    return (
        <AuthProvider>
            <AppContent />
        </AuthProvider>
    );
}

export default App;
