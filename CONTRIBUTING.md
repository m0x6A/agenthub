# Contributing to AgentBus

Thank you for your interest in contributing to AgentBus! This document provides guidelines and instructions for contributing.

## Development Workflow

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker](https://docs.docker.com/get-docker/)
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) (for infrastructure deployment)
- Git

### Setting Up Development Environment

1. Clone the repository:
   ```bash
   git clone https://github.com/m0x6A/agenthub.git
   cd agenthub
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Build the solution:
   ```bash
   dotnet build
   ```

4. Run tests:
   ```bash
   dotnet test
   ```

### Code Style

This project uses EditorConfig for consistent code style. The .editorconfig file in the repository root defines the rules:

- C# 13 / .NET 9
- Nullable reference types enabled
- File-scoped namespaces
- Primary constructors preferred
- Warning as errors (TreatWarningsAsErrors=true)

Run the formatter before committing:
```bash
dotnet format
```

### BDD/TDD Workflow

AgentBus follows Test-Driven Development with Behavior-Driven Development scenarios:

1. **Write BDD Scenarios** (Reqnroll/Gherkin)
   - Define feature in `.feature` file
   - Get stakeholder approval

2. **Write Unit Tests** (xUnit)
   - Write tests that FAIL initially (RED phase)
   - Use Shouldly for assertions, NSubstitute for mocks

3. **Implement Feature** (GREEN phase)
   - Write minimal code to make tests pass
   - Follow Vertical Slice Architecture

4. **Refactor** (BLUE phase)
   - Improve code while keeping tests green
   - Add telemetry and logging

### Architecture Principles

- **Modular Monolith**: Clear module boundaries with explicit interfaces
- **Vertical Slices**: Each feature is self-contained (endpoint, request, response, handler, validator)
- **Security by Identity**: Azure Managed Identities, no secrets in code
- **Observability First**: OpenTelemetry traces, structured logging, custom metrics

### Pull Request Process

1. Create a feature branch:
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. Make changes following the code style and architecture principles

3. Run tests and ensure they pass:
   ```bash
   dotnet test
   ```

4. Commit with clear, descriptive messages:
   ```bash
   git commit -m "Add feature: description"
   ```

5. Push to your fork and create a Pull Request

6. Ensure CI passes (build and tests)

7. Request review from maintainers

### Commit Message Guidelines

- Use present tense ("Add feature" not "Added feature")
- Use imperative mood ("Move cursor to..." not "Moves cursor to...")
- First line should be 50 characters or less
- Reference issues and pull requests liberally

### Reporting Bugs

When reporting bugs, please include:

- .NET version
- Operating system
- Steps to reproduce
- Expected behavior
- Actual behavior
- Relevant logs or error messages

### Feature Requests

Feature requests are welcome! Please include:

- Clear use case
- Expected behavior
- How it aligns with project goals
- Any relevant examples from other projects

## License

By contributing to AgentBus, you agree that your contributions will be licensed under the MIT License.

## Questions?

Feel free to open an issue for questions or discussions!
