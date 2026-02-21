const express = require('express');

const app = express();
app.use(express.json());

app.get('/health', (_, res) => res.json({ service: 'payment-service', status: 'ok' }));

app.post('/payments/authorize', (req, res) => {
  const { orderId, amount } = req.body;
  if (!orderId || typeof amount !== 'number') {
    return res.status(400).json({ error: 'orderId and amount required' });
  }

  const approved = amount < 1000; // simple policy
  if (!approved) {
    return res.status(402).json({ status: 'DECLINED', reason: 'Amount exceeds risk threshold' });
  }

  return res.json({ status: 'APPROVED', transactionId: `tx_${Date.now()}` });
});

const port = process.env.PORT || 4004;
app.listen(port, () => console.log(`payment-service running on ${port}`));
