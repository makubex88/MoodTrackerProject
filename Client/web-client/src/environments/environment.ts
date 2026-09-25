// `ng serve` proxies /api to the backend (proxy.conf.json), so development is same-origin like production.
export const environment = {
  production: false,
  apiUrl: '/api'
};
