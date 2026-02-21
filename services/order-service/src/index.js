const express = require('express');
const axios = require('axios');
const { v4: uuid } = require('uuid');
const CircuitBreaker = require('opossum');
const amqp = require('amqplib');

const app = express();
app.use(express.json());

const rabbitUrl = process.env.RABBITMQ_URL || 'amqp://localhost:5672';
const paymentUrl = process.env.PAYMENT_SERVICE_URL || 'http://localhost:4004';
const orders = [];

let channel;
const initBus = async () => {
  try {
    const conn = await amqp.connect(rabbitUrl);
    channel = await conn.createChannel();
    await channel.assertExchange('domain.events', 'topic', { durable: false });
  } catch (err) {
    console.warn('RabbitMQ unavailable; events disabled.', err.message);
  }
};
initBus();

const chargePayment = async (payload) => {
  const response = await axios.post(`${paymentUrl}/payments/authorize`, payload);
  return response.data;
};

const paymentBreaker = new CircuitBreaker(chargePayment, {
  timeout: 3000,
  errorThresholdPercentage: 50,
  resetTimeout: 5000
});
paymentBreaker.fallback(() => ({ status: 'PENDING_RETRY' }));

app.get('/health', (_, res) => res.json({ service: 'order-service', status: 'ok' }));

app.post('/orders', async (req, res) => {
  const { userId, items } = req.body;
  if (!userId || !Array.isArray(items) || items.length === 0) {
    return res.status(400).json({ error: 'userId and items are required' });
  }

  const amount = items.reduce((acc, item) => acc + (item.price || 100) * (item.quantity || 1), 0);
  const order = { id: uuid(), userId, items, amount, status: 'PENDING_PAYMENT' };
  orders.push(order);

  try {
    const payment = await paymentBreaker.fire({ orderId: order.id, amount });
    if (payment.status === 'APPROVED') {
      order.status = 'CONFIRMED';
      if (channel) {
        channel.publish('domain.events', 'order.confirmed', Buffer.from(JSON.stringify(order)));
      }
    } else if (payment.status === 'PENDING_RETRY') {
      order.status = 'PAYMENT_RETRY';
    } else {
      order.status = 'PAYMENT_FAILED';
    }
  } catch (_) {
    order.status = 'PAYMENT_FAILED';
  }

  return res.status(201).json(order);
});

app.get('/orders', (_, res) => res.json(orders));

const port = process.env.PORT || 4003;
app.listen(port, () => console.log(`order-service running on ${port}`));
