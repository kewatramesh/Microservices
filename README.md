# Microservices Reference Project

This repository is a runnable microservices blueprint that demonstrates core microservices concepts in one place:

- **API Gateway** for edge routing.
- **Service decomposition** by business capability.
- **Database-per-service** and ownership boundaries (in-memory in this starter, can be swapped for dedicated DBs).
- **Synchronous communication** (REST between gateway/services and service-to-service calls).
- **Asynchronous communication** (RabbitMQ events).
- **Saga-style workflow** (`order-service` orchestrates payment + emits domain events).
- **Resilience patterns** (circuit breaker with fallback for payment calls).
- **Caching** (Redis read-through style in catalog).
- **Centralized configuration** via environment variables and `.env`.
- **Containerization + local orchestration** via Docker Compose.
- **Health checks + basic observability hooks** through `/health` endpoints.
- **CI pipeline** example via GitHub Actions.

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
3. `order-service` calls `payment-service` using a circuit breaker.
4. If payment succeeds:
   - order becomes `CONFIRMED`
   - `order.confirmed` event is published
5. `notification-service` consumes event and logs a confirmation notification.

## Quick Start

```bash
cp .env.example .env
npm run bootstrap
npm run up
```

### Try the system

```bash
# Register a user
curl -X POST http://localhost:8080/auth/register -H 'content-type: application/json' -d '{"email":"dev@example.com","password":"secret"}'

# Create a product
curl -X POST http://localhost:8080/catalog/products -H 'content-type: application/json' -d '{"name":"Keyboard","price":89.99}'

# Create an order
curl -X POST http://localhost:8080/orders -H 'content-type: application/json' -d '{"userId":"u1","items":[{"productId":"p1","quantity":1}]}'
```

## Local Development (without Docker)

Run each service in its own terminal:

```bash
cd services/<service-name>
npm install
npm run dev
```

> RabbitMQ + Redis must still be running if you want async messaging/caching.

## Suggested Extensions

- Replace in-memory stores with service-owned PostgreSQL/MongoDB databases.
- Add JWT verification middleware in each internal service.
- Introduce service discovery (Consul/Eureka) and dynamic routing.
- Add tracing (OpenTelemetry) and metrics exporters.
- Add contract testing (Pact) and consumer-driven tests.
