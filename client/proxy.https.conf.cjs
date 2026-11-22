module.exports = {
  '/api': {
    target: process.env.VERBULIO_DEV_API_URL ?? 'https://localhost:5186',
    secure: true,
  },
};
