# Suggested Commands

## Running the Application
- **Full Stack (Docker)**: `docker-compose up --build`
- **Frontend (Dev Server)**: `cd frontend; npm install; npm start`
- **Backend (Local)**: `cd backend/src/SmoothOperator.Api; dotnet run`
- **Docs**: `docker-compose up docs`

## Build and Compilation
- **Backend Build**: `dotnet build smooth-operator.sln`
- **Backend Build (Windows Fix)**: `dotnet build smooth-operator.sln /p:DisableFastUpToDateCheck=true`
- **Frontend Build**: `cd frontend; npm run build`

## System Utilities (Windows PowerShell)
- **List Files**: `ls`, `dir`, or `Get-ChildItem`
- **Search Content**: `grep` (if available) or `Select-String`
- **Find Files**: `find` (Windows version differs from Unix) or `Get-ChildItem -Recurse -Filter`
- **Git**: `git status`, `git log -n 5`, `git diff --staged`

## Environment Setup
- **Dependencies**: `npm install` in `frontend` or `docs`.
- **Database Migrations**: Handled by the backend on startup in Development mode.
