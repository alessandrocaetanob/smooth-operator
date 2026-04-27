# 🧪 Add tests for InvitesService.redeem

## Description
🎯 **What:** The testing gap addressed
This PR addresses a missing test gap for the `InvitesService.redeem` method within the `frontend/src/app/services/invites.service.ts` file, ensuring its robustness and reliability.

📊 **Coverage:** What scenarios are now tested
The new `invites.service.spec.ts` test file covers:
- **Happy Path:** Validates that a successful redeem triggers a POST request with the correct endpoint (`/api/invites/{token}/redeem`) and payload configuration (`{ password, name }`).
- **URL Encoding:** Validates that tokens containing special characters (such as `/`) are safely URL-encoded before the HTTP request is executed.
- **Error Propagation:** Verifies correct propagation of server side HTTP error responses including 400 Bad Request, 404 Not Found, and 500 Internal Server Error back to the subscriber.

✨ **Result:** The improvement in test coverage
Test coverage for the `InvitesService` has increased, specifically ensuring 100% path coverage on the `redeem` method. This enables refactoring with confidence.
