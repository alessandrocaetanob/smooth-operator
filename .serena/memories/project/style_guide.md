# Style and Conventions

## Architectural Patterns
- **Clean Architecture**: Separation of concerns into Domain, Application, Infrastructure, and Api layers.
- **CQRS**: Command Query Responsibility Segregation using MediatR.
- **Options Pattern**: Configuration handled via strongly-typed options.

## Frontend Conventions
- **Angular Standalone Components**: Modern Angular 21 patterns.
- **Signals**: Using Signals and Signal Stores for state management.
- **Design System**: "Operator Glass" — dark, glassmorphic UI using Tailwind CSS 4 variables.
- **Tokens**: Use CSS custom properties (tokens) instead of hardcoded values.
- **Animations**: Purposeful animations using Angular triggers and Tailwind keyframes.

## Naming & Code Quality
- **Naming**: Follow standard C# (PascalCase for classes/methods) and TypeScript (camelCase for methods/variables, PascalCase for classes) conventions.
- **Linting**: ESLint for frontend, `dotnet format` for backend.
- **Coverage**: Targets 80% for backend and 70% for frontend.
