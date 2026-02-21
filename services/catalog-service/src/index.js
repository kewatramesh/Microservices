const express = require('express');
const { createClient } = require('redis');
const { v4: uuid } = require('uuid');

const app = express();
app.use(express.json());

const products = [];
const redisUrl = process.env.REDIS_URL || 'redis://localhost:6379';
const cache = createClient({ url: redisUrl });
cache.connect().catch(() => console.warn('Redis unavailable; running without cache.'));

app.get('/health', (_, res) => res.json({ service: 'catalog-service', status: 'ok' }));

app.post('/catalog/products', async (req, res) => {
  const { name, price } = req.body;
  if (!name || typeof price !== 'number') {
    return res.status(400).json({ error: 'name and numeric price required' });
  }

  const product = { id: uuid(), name, price };
  products.push(product);
  await cache.del('catalog:all').catch(() => {});
  return res.status(201).json(product);
});

app.get('/catalog/products', async (_, res) => {
  const cached = await cache.get('catalog:all').catch(() => null);
  if (cached) {
    return res.json({ source: 'cache', data: JSON.parse(cached) });
  }

  await cache.setEx('catalog:all', 30, JSON.stringify(products)).catch(() => {});
  return res.json({ source: 'service', data: products });
});

const port = process.env.PORT || 4002;
app.listen(port, () => console.log(`catalog-service running on ${port}`));
