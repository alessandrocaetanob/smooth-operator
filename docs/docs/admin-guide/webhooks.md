---
sidebar_position: 11
---

# Webhooks

Webhooks let Smooth Operator push audit events to your own systems in real time —
a SIEM, a chat channel, a ticketing automation — instead of you polling the API.
Whenever an audit event fires, every subscribed endpoint receives an
HMAC-signed HTTPS `POST`.

Go to **Settings → Webhooks** to configure them (Owner / Admin only).

## Registering an endpoint

1. Click **Add endpoint**.
2. Enter a **Name** (a label for your reference) and a **Payload URL**. The URL
   must be an absolute **HTTPS** URL.
3. Choose which events to deliver:
   - **All events** — every audit action.
   - Or tick one or more **categories** (Users & authentication, Connections,
     Credentials, Single sign-on, Groups, Invitations, Webhooks, System).
4. Click **Save endpoint**.

On save, Smooth Operator shows the **signing secret once**. Copy it immediately —
it is never displayed again. Store it wherever your receiver reads it from.

## Event subscriptions

Each endpoint stores its subscription as a set of patterns:

| Pattern        | Matches                                                        |
| -------------- | -------------------------------------------------------------- |
| `*`            | Every audit event.                                             |
| `user.*`       | Every action whose name starts with `user.` (category prefix). |
| `connection.started` | Exactly that one action.                                 |

The category checkboxes in the UI map to prefixes such as `user.*`,
`connection.*`, and `credential.*`. Audit action names follow a
`category.action` convention (e.g. `user.login_success`,
`connection.started`, `webhook.created`).

## Payload

The request body is JSON:

```json
{
  "id": "0c2e1f9a-...-delivery-id",
  "event": "connection.started",
  "timestamp": "2026-05-22T20:00:00.0000000Z",
  "data": {
    "auditLogId": "8722ca00-...",
    "action": "connection.started",
    "resourceType": "Connection",
    "resourceId": "3ea79444-...",
    "outcome": "success",
    "userId": "1330476e-...",
    "ipAddress": "203.0.113.7",
    "correlationId": "abc123",
    "details": { }
  }
}
```

Each request also carries these headers:

| Header                      | Value                                                       |
| --------------------------- | ----------------------------------------------------------- |
| `X-SmoothOperator-Event`    | The event name (same as `event` in the body).               |
| `X-SmoothOperator-Delivery` | A unique delivery id (same as `id` in the body).            |
| `X-SmoothOperator-Timestamp`| ISO-8601 UTC time the request was signed.                   |
| `X-SmoothOperator-Signature`| `sha256=<hex>` — see below.                                 |

## Verifying the signature

The signature protects against forged and replayed requests. To verify a
request, compute an HMAC-SHA256 over the string
`"{timestamp}.{raw-body}"` using your endpoint's signing secret, and compare it
to the `X-SmoothOperator-Signature` header.

```python
import hmac, hashlib

def is_valid(secret, timestamp, raw_body, signature_header):
    signed = f"{timestamp}.{raw_body}".encode()
    expected = "sha256=" + hmac.new(secret.encode(), signed, hashlib.sha256).hexdigest()
    return hmac.compare_digest(expected, signature_header)
```

Use the **raw request body** exactly as received — do not re-serialize the JSON.
Reject requests whose `X-SmoothOperator-Timestamp` is far from your current time
to prevent replay.

## Testing an endpoint

Use the **Test** button on any endpoint to queue a synthetic `webhook.ping`
event. It is delivered and signed exactly like a real event, so you can confirm
your receiver and signature check work before relying on live traffic.

## Delivery, retries and status

Deliveries are queued the moment an audit event is committed and sent by a
background worker, so an unreachable endpoint never slows down the app.

- A `2xx` response marks the delivery **succeeded**.
- Any other response, a timeout, or a connection error is **retried** with
  exponential backoff.
- After the configured maximum number of attempts the delivery is marked
  **dead** and not retried again.

The endpoint list shows the **last delivery status**, the last HTTP response
code, and a **consecutive-failure** count so you can spot a broken receiver.

## Rotating the secret

Use **Rotate** to generate a new signing secret (shown once). The old secret
stops working immediately, so update your receiver promptly.

## Configuration

Delivery behaviour is tunable via the `Webhooks` configuration section
(environment variables use the `Webhooks__` prefix):

| Setting                  | Default | Purpose                                        |
| ------------------------ | ------- | ---------------------------------------------- |
| `PollIntervalSeconds`    | `10`    | How often the worker scans for due deliveries. |
| `BatchSize`              | `50`    | Maximum deliveries processed per scan.         |
| `MaxAttempts`            | `5`     | Attempts before a delivery is marked dead.     |
| `RetryBaseSeconds`       | `60`    | Base of the exponential backoff.               |
| `HttpTimeoutSeconds`     | `15`    | Per-request timeout for a delivery.            |
| `DeliveryRetentionDays`  | `30`    | How long delivered/dead rows are kept.         |
