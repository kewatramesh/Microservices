const express = require('express');
const { v4: uuid } = require('uuid');

const app = express();
app.use(express.json());

const users = [];

app.get('/health', (_, res) => res.json({ service: 'identity-service', status: 'ok' }));

app.post('/auth/register', (req, res) => {
  const { email, password } = req.body;
  if (!email || !password) {
    return res.status(400).json({ error: 'email and password required' });
  }

  const exists = users.find((u) => u.email === email);
  if (exists) {
    return res.status(409).json({ error: 'user already exists' });
  }

  const user = { id: uuid(), email, password };
  users.push(user);
  return res.status(201).json({ id: user.id, email: user.email });
});

app.post('/auth/login', (req, res) => {
  const { email, password } = req.body;
  const user = users.find((u) => u.email === email && u.password === password);
  if (!user) {
    return res.status(401).json({ error: 'invalid credentials' });
  }

  return res.json({
    accessToken: Buffer.from(`${user.id}:${Date.now()}`).toString('base64'),
    tokenType: 'Bearer'
  });
});

const port = process.env.PORT || 4001;
app.listen(port, () => console.log(`identity-service running on ${port}`));
