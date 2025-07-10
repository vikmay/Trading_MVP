# Trading MVP

A microservices-based trading platform with real-time data processing and web interface.

## Setup

### Environment Configuration

1. Copy the environment template:

    ```
    cp .env.example .env
    ```

2. Edit `.env` and fill in your actual values:
    - `GOOGLE_CLIENT_ID`: Your Google OAuth2 client ID
    - `GOOGLE_CLIENT_SECRET`: Your Google OAuth2 client secret
    - `DB_PASSWORD`: Database password

### Running the Application

1. Start the backend services:

    ```
    docker-compose up -d
    ```

2. Start the frontend:

    ```
    cd webui
    npm install
    npm run dev
    ```

3. Access the application at `http://localhost:5174`

## Architecture

-   **Auth Service**: Handles authentication including Google OAuth2
-   **Gateway Service**: SignalR hub for real-time communication
-   **Collector Service**: Data collection from exchanges
-   **Normaliser Service**: Data normalization
-   **Bridge Service**: Data bridging between services
-   **Web UI**: React-based frontend interface

## Services

-   Auth: http://localhost:8082
-   Gateway: http://localhost:8083
-   Web UI: http://localhost:5174 (dev)
-   PostgreSQL: localhost:5432
-   pgAdmin: http://localhost:5050
