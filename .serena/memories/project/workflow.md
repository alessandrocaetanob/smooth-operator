# Workflow and Task Completion

## Task Completion Checklist
Before finishing a task, ensure the following are performed:
1. **Build**: Verify both backend and frontend build successfully.
2. **Lint**: Run linting to ensure code style compliance.
3. **Format**: Apply formatting to keep the codebase clean.
4. **Test**: Run unit and integration tests.

## Commands for Completion
- **Frontend Lint & Format**: `npm run lint` in the `frontend` directory.
- **Backend Format**: `dotnet format smooth-operator.sln`.
- **Frontend Test**: `npm test -- --run` in the `frontend` directory.
- **Backend Test**: `dotnet test smooth-operator.sln`.
- **Full Build**: `docker-compose up --build`.
