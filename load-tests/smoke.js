// k6 smoke load test for Smooth Operator.
//
// Exercises the hot API paths: the public app-initializer endpoint, credential
// login (sets an httpOnly auth cookie), and the connections list query.
//
// Run against a running stack (see ../load-tests/README.md):
//   k6 run load-tests/smoke.js
//   BASE_URL=http://localhost:5000 SO_EMAIL=admin@example.com SO_PASSWORD='...' k6 run load-tests/smoke.js
//
// Auth note: the backend delivers the JWT via an httpOnly cookie, not the
// response body. k6 keeps a per-VU cookie jar, so requests after login are
// authenticated automatically — no token extraction needed.

import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const EMAIL = __ENV.SO_EMAIL || 'admin@example.com';
const PASSWORD = __ENV.SO_PASSWORD || 'ChangeMe123!';

export const options = {
  scenarios: {
    smoke: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 10 }, // ramp up
        { duration: '1m', target: 10 }, // hold
        { duration: '15s', target: 0 }, // ramp down
      ],
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'], // <1% errors
    http_req_duration: ['p(95)<500'], // 95th percentile under 500ms
  },
};

export default function () {
  // 1. Public app-initializer endpoint — called on every full page load.
  const status = http.get(`${BASE_URL}/api/auth/setup-status`);
  check(status, { 'setup-status is 200': (r) => r.status === 200 });

  // 2. Credential login — stores the auth cookie in this VU's cookie jar.
  const login = http.post(
    `${BASE_URL}/api/auth/login`,
    JSON.stringify({ email: EMAIL, password: PASSWORD }),
    { headers: { 'Content-Type': 'application/json' } },
  );
  const authed = check(login, { 'login is 200': (r) => r.status === 200 });
  if (!authed) {
    sleep(1);
    return;
  }

  // 3. Connections list — exercises GetConnectionsQuery and its EF eager loads.
  const connections = http.get(`${BASE_URL}/api/connections`);
  check(connections, { 'connections is 200': (r) => r.status === 200 });

  sleep(1);
}
