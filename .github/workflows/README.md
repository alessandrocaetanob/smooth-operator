# GitHub Actions Workflows

This directory contains automated workflows for code quality, security scanning, and CI/CD processes.

## 🔐 Security Workflows

### CodeQL Analysis (`codeql-analysis.yml`)
**Purpose:** Automated security vulnerability detection using GitHub's CodeQL engine.

**When it runs:**
- Push to `main` or `develop` branches
- Pull requests to `main` or `develop`
- Scheduled: Every Monday at 2:30 AM UTC

**What it scans:**
- **C# Backend**: Security vulnerabilities, code quality issues, SQL injection, authentication flaws
- **TypeScript/JavaScript Frontend**: XSS vulnerabilities, injection flaws, security misconfigurations

**Queries used:** `security-extended` and `security-and-quality`

### Dependency Security Scan (`dependency-scan.yml`)
**Purpose:** Identify vulnerable dependencies and license compliance issues.

**When it runs:**
- Push to `main` or `develop` (when dependency files change)
- Pull requests (when dependency files change)
- Scheduled: Daily at 3:00 AM UTC

**What it checks:**
- **Frontend**: npm audit for JavaScript package vulnerabilities
- **Backend**: .NET vulnerable package detection
- **PR Review**: Dependency review action for new/updated dependencies
- **License compliance**: Blocks GPL-2.0 and GPL-3.0 licenses

### Docker Security Scan (`docker-security.yml`)
**Purpose:** Scan Docker images and Dockerfiles for security issues.

**When it runs:**
- Push to `main` or `develop` (when Docker files change)
- Pull requests (when Docker files change)
- Scheduled: Every Monday at 4:00 AM UTC

**What it scans:**
- Dockerfile best practices with Hadolint
- Container image vulnerabilities with Trivy
- Docker Compose configuration validation

## ✨ Code Quality Workflows

### Frontend Code Quality (`frontend-quality.yml`)
**Purpose:** Ensure frontend code meets quality standards.

**When it runs:**
- Push to `main` or `develop` (when frontend code changes)
- Pull requests (when frontend code changes)

**Checks performed:**
- **Prettier formatting**: Validates consistent code formatting
- **ESLint**: Lints TypeScript/HTML files (if configured)
- **Build validation**: Ensures production build succeeds
- **Tests**: Runs frontend test suite

### Backend Code Quality (`backend-quality.yml`)
**Purpose:** Ensure backend code meets .NET quality standards.

**When it runs:**
- Push to `main` or `develop` (when backend code changes)
- Pull requests (when backend code changes)

**Checks performed:**
- **.NET analyzers**: Enforces code style and quality rules
- **dotnet format**: Validates code formatting
- **Build validation**: Ensures release build succeeds with latest analysis level
- **Tests**: Runs backend test suite

## 🔄 Pull Request Workflow

### PR Quality Checks (`pr-checks.yml`)
**Purpose:** Comprehensive PR validation before merging.

**When it runs:**
- On all pull requests (when not in draft mode)

**Validation steps:**

1. **PR Validation**
   - Title must follow [Conventional Commits](https://www.conventionalcommits.org/) format
   - Description must not be empty
   - Detects which components changed (backend/frontend)

2. **Backend Checks** (if backend changed)
   - Build with code analyzers
   - Security vulnerability scan

3. **Frontend Checks** (if frontend changed)
   - Prettier formatting check
   - Build validation
   - Test execution
   - npm security audit

4. **Security Scan**
   - Trivy filesystem scan for all code
   - Results uploaded to GitHub Security tab

5. **All Checks Pass**
   - Final gate ensuring all previous jobs succeeded

## 📋 Conventional Commits Format

PR titles must follow this format:
```
type(scope?): description

Types:
- feat: A new feature
- fix: A bug fix
- docs: Documentation only changes
- style: Code style changes (formatting, semicolons, etc.)
- refactor: Code refactoring without changing functionality
- perf: Performance improvements
- test: Adding or updating tests
- chore: Changes to build process or auxiliary tools
- ci: CI/CD configuration changes
- build: Changes that affect the build system
- revert: Revert a previous commit

Examples:
✅ feat(backend): add JWT authentication
✅ fix(frontend): resolve login redirect issue
✅ docs: update deployment instructions
✅ chore(deps): update Angular to v21.2.0
❌ Added new feature (missing type)
❌ backend: add auth (wrong format)
```

## 🎯 Best Practices

### For Developers

1. **Before creating a PR:**
   - Run `npm run build` and `npm test` in frontend
   - Run `dotnet build` and `dotnet test` in backend
   - Run `npx prettier --write` to format frontend code
   - Run `dotnet format` to format backend code

2. **Creating PRs:**
   - Use conventional commit format for PR title
   - Provide clear description of changes
   - Keep PRs focused and reasonably sized
   - Link related issues

3. **Responding to workflow failures:**
   - Check the Actions tab for detailed error logs
   - Fix issues locally before pushing
   - Security alerts should be addressed promptly

### For Maintainers

1. **Security alerts:**
   - Review CodeQL findings in Security tab
   - Prioritize HIGH and CRITICAL vulnerabilities
   - Update dependencies regularly

2. **Merge requirements:**
   - All PR checks must pass
   - At least one approval (configure in branch protection)
   - No unresolved conversations

## 🔧 Configuration

### Branch Protection Rules (Recommended)

Configure these settings in GitHub repository settings:

```yaml
Protected branches: main, develop

Required status checks:
  - PR Validation
  - Backend Quality & Security (if backend changed)
  - Frontend Quality & Security (if frontend changed)
  - Security Scanning
  - All Checks Passed

Additional settings:
  - Require branches to be up to date before merging
  - Require linear history
  - Do not allow bypassing the above settings
```

### Secrets Required

No secrets are required for these workflows. All tools use GitHub's built-in tokens.

## 📊 Viewing Results

### Security Results
- Navigate to: **Security** → **Code scanning alerts**
- View CodeQL and Trivy findings
- Track vulnerability trends over time

### Action Runs
- Navigate to: **Actions** tab
- Filter by workflow name
- View logs, artifacts, and test results

## 🚀 Future Enhancements

Potential improvements for consideration:

- [ ] Add code coverage reporting (Codecov/Coveralls)
- [ ] Implement automated dependency updates (Dependabot)
- [ ] Add performance testing workflow
- [ ] Integrate Lighthouse CI for frontend performance
- [ ] Add semantic versioning and changelog generation
- [ ] Implement automated deployment workflows
- [ ] Add Slack/Discord notifications for failures
- [ ] Container image signing with Sigstore

## 📚 Additional Resources

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [CodeQL Documentation](https://codeql.github.com/docs/)
- [Conventional Commits](https://www.conventionalcommits.org/)
- [SARIF Format](https://sarifweb.azurewebsites.net/)
- [Trivy Documentation](https://aquasecurity.github.io/trivy/)
