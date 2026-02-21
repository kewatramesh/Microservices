const express = require('express');
const amqp = require('amqplib');

const app = express();
const rabbitUrl = process.env.RABBITMQ_URL || 'amqp://localhost:5672';

app.get('/health', (_, res) => res.json({ service: 'notification-service', status: 'ok' }));

const listen = async () => {
  try {
    const conn = await amqp.connect(rabbitUrl);
    const channel = await conn.createChannel();
    await channel.assertExchange('domain.events', 'topic', { durable: false });
    const queue = await channel.assertQueue('notification.order.confirmed', { durable: false });
    await channel.bindQueue(queue.queue, 'domain.events', 'order.confirmed');

    channel.consume(queue.queue, (msg) => {
      if (!msg) return;
      const order = JSON.parse(msg.content.toString());
      console.log(`Notification sent for order ${order.id} to user ${order.userId}`);
      channel.ack(msg);
    });
  } catch (err) {
    console.warn('Could not connect to RabbitMQ:', err.message);
  }
};
listen();

const port = process.env.PORT || 4005;
app.listen(port, () => console.log(`notification-service running on ${port}`));
