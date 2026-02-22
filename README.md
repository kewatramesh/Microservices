# Microservices Reference Project (.NET)

This repository is a runnable microservices blueprint implemented in **C# / .NET 8**:

- **API Gateway** for edge routing.
- **Service decomposition** by business capability.
- **Database-per-service** and ownership boundaries (in-memory in this starter, can be swapped for dedicated DBs).
- **Synchronous communication** (REST between gateway/services and service-to-service calls).
- **Asynchronous communication** (RabbitMQ events).
- **Saga-style workflow** (`order-service` orchestrates payment + emits domain events).
- **Resilience patterns** (basic circuit breaker fallback for payment calls).
- **Caching** (Redis read-through style in catalog).
- **Centralized configuration** via environment variables and `.env`.
- **Containerization + local orchestration** via Docker Compose.
- **Health checks** through `/health` endpoints.

## Services

| Service | Port | Responsibility |
| --- | --- | --- |
| api-gateway | 8080 | Single entrypoint, request forwarding |
| identity-service | 4001 | User registration and login tokens |
| catalog-service | 4002 | Product catalog, cached reads |
| order-service | 4003 | Order creation and saga orchestration |
| payment-service | 4004 | Payment authorization simulation |
| notification-service | 4005 | Event-driven notifications |

## Event Flow

1. Client creates an order through gateway (`POST /orders`).
2. `order-service` creates order in `PENDING_PAYMENT` state.
3. `order-service` calls `payment-service` with circuit-breaker behavior.
4. If payment succeeds:
   - order becomes `CONFIRMED`
   - `order.confirmed` event is published
5. `notification-service` consumes event and logs a confirmation notification.

## Quick Start

```bash
cp .env.example .env
docker compose up --build
```

## Local Development (without Docker)

Run each service in its own terminal:

```bash
cd services/<service-name>
dotnet restore
dotnet run
```

> RabbitMQ + Redis must still be running if you want async messaging/caching.
