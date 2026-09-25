// nginx proxies /api to the backend container (nginx.conf), so the app never needs the backend's host or port.
export const environment = {
  production: true,
  apiUrl: '/api'
};
