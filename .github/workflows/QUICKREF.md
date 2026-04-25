# Quick Reference: CI/CD Workflows

## ▶️ Running Workflows Manually

All workflows can be triggered manually from the GitHub interface:

1. Go to **Actions** tab → Select workflow → Click **"Run workflow"**
2. Choose branch → Click **"Run workflow"** to start

## 🚦 Workflow Status Summary

| Workflow | Triggers | Purpose |
|----------|----------|---------|
| **CodeQL Analysis** | Push, PR, Weekly schedule, **Manual** | Security vulnerability scanning |
| **Frontend Quality** | Push/PR (frontend changes), **Manual** | Linting, formatting, build, tests |
| **Backend Quality** | Push/PR (backend changes), **Manual** | .NET analyzers, formatting, tests |
| **Dependency Scan** | Push/PR (dependency files), Daily, **Manual** | Vulnerable dependency detection |
| **Docker Security** | Push/PR (Docker files), Weekly, **Manual** | Container security scanning |
| **PR Checks** | All Pull Requests | Comprehensive PR validation gate |

## 🔍 What Gets Scanned

### Security Scans
- ✅ SQL Injection vulnerabilities
- ✅ XSS (Cross-Site Scripting)
- ✅ Authentication/Authorization flaws
- ✅ Hardcoded secrets detection
- ✅ Insecure dependencies
- ✅ Container vulnerabilities
- ✅ Code quality issues

### Code Quality Checks
- ✅ Code formatting (Prettier, dotnet format)
- ✅ Linting rules (ESLint, .NET analyzers)
- ✅ Build validation
- ✅ Test execution
- ✅ Type safety

## ⚡ Quick Commands

### Before Committing
```bash
# Frontend
cd frontend
npm install
npx prettier --write "src/**/*.{ts,html,css,scss,json}"
npm run build
npm test

# Backend
cd backend
dotnet restore
dotnet format
dotnet build --configuration Release
dotnet test
```

### Check for Vulnerabilities Locally
```bash
# Frontend
cd frontend
npm audit

# Backend
cd backend
dotnet list package --vulnerable --include-transitive
```

## 📝 PR Checklist

Before creating a PR, ensure:

- [ ] PR title follows conventional commits format
- [ ] PR has a clear description
- [ ] Code is properly formatted
- [ ] All tests pass locally
- [ ] No new security vulnerabilities introduced
- [ ] Build succeeds
- [ ] No sensitive data (secrets, credentials) in code

## 🛠️ Troubleshooting

### Common Issues

**"PR title must follow conventional commits"**
```
❌ Added new feature
✅ feat(backend): add user authentication

Format: type(scope): description
```

**"Prettier formatting check failed"**
```bash
cd frontend
npx prettier --write "src/**/*.{ts,html,css,scss,json}"
```

**".NET format check failed"**
```bash
cd backend
dotnet format
```

**"Build failed"**
- Check for TypeScript/compilation errors
- Ensure all dependencies are installed
- Review error logs in Actions tab

**"Security vulnerabilities found"**
- Update vulnerable packages
- Check Security tab for details
- Address HIGH/CRITICAL issues first

## 🎯 Workflow Badges

Add to your README.md:
```markdown
![CodeQL](https://github.com/alessandrocaetanob/smooth-operator/workflows/CodeQL%20Security%20Analysis/badge.svg)
![Frontend Quality](https://github.com/alessandrocaetanob/smooth-operator/workflows/Frontend%20Code%20Quality/badge.svg)
![Backend Quality](https://github.com/alessandrocaetanob/smooth-operator/workflows/Backend%20Code%20Quality/badge.svg)
```

## 📊 Viewing Results

- **Actions Tab**: View all workflow runs and logs
- **Security Tab**: View CodeQL and Trivy findings
- **Pull Requests**: See check status on each PR

## 🔐 Security Best Practices

1. **Never commit secrets** - Use environment variables or Azure Key Vault
2. **Update dependencies regularly** - Weekly/monthly reviews
3. **Address security alerts promptly** - Within 30 days for HIGH, 7 days for CRITICAL
4. **Review CodeQL findings** - Not all are vulnerabilities, use judgment
5. **Enable branch protection** - Require all checks to pass before merge

## 📚 Learn More

- Full documentation: `.github/workflows/README.md`
- Conventional Commits: https://conventionalcommits.org
- GitHub Actions: https://docs.github.com/actions
- CodeQL: https://codeql.github.com
