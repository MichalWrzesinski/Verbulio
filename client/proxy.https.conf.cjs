const target = new URL(process.env.VERBULIO_DEV_API_URL ?? 'https://localhost:5186');
if (target.protocol !== 'https:') {
  throw new Error('VERBULIO_DEV_API_URL must use HTTPS.');
}

module.exports = {
  '/api': {
    target: target.href,
    secure: true,
  },
};
