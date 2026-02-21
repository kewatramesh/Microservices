const express = require('express');
const axios = require('axios');
const morgan = require('morgan');

const app = express();
app.use(express.json());
app.use(morgan('dev'));

const routes = {
  '/auth': process.env.IDENTITY_SERVICE_URL || 'http://localhost:4001',
  '/catalog': process.env.CATALOG_SERVICE_URL || 'http://localhost:4002',
  '/orders': process.env.ORDER_SERVICE_URL || 'http://localhost:4003'
};

app.get('/health', (_, res) => res.json({ service: 'api-gateway', status: 'ok' }));

Object.entries(routes).forEach(([prefix, target]) => {
  app.use(prefix, async (req, res) => {
    try {
      const url = `${target}${req.originalUrl}`;
      const response = await axios({
        method: req.method,
        url,
        data: req.body,
        headers: { 'content-type': 'application/json' }
      });
      res.status(response.status).json(response.data);
    } catch (error) {
      const status = error.response?.status || 502;
      const message = error.response?.data || { error: 'Upstream service unavailable' };
      res.status(status).json(message);
    }
  });
});

const port = process.env.PORT || 8080;
app.listen(port, () => console.log(`api-gateway running on ${port}`));
