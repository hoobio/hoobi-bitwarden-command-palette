# GitHub Copilot Instructions

## Git Commit Message Format

All commit messages MUST follow the [Conventional Commits](https://www.conventionalcommits.org/) specification for release-please compatibility.

### Required Format

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

### Commit Types

Use these standard types (release-please compatible):

- **feat**: A new feature (triggers MINOR version bump)
- **fix**: A bug fix (triggers PATCH version bump)
- **chore**: Maintenance tasks, dependency updates, tooling changes (no version bump)
- **docs**: Documentation only changes (no version bump)
- **style**: Code style changes (formatting, no functional changes, no version bump)
- **refactor**: Code refactoring without feature/fix changes (no version bump)
- **perf**: Performance improvements (triggers PATCH version bump)
- **test**: Adding or updating tests (no version bump)
- **build**: Build system or external dependency changes (no version bump)
- **ci**: CI/CD pipeline changes (no version bump)
- **revert**: Revert a previous commit

### Breaking Changes

For breaking changes (triggers MAJOR version bump):
- Add `!` after type: `feat!: breaking change`
- Or include `BREAKING CHANGE:` in footer

### Examples

Good:
```
feat: add Dev suffix to Debug builds for side-by-side installation
fix: clear vault cache immediately before locking
chore: add WACK testing as separate job
docs: update README with installation steps
ci: configure release-please to update Package.appxmanifest
```

Bad (DON'T USE):
```
🐛 fix bug
Fixed the lock issue
WIP
Update
```

### Scope (Optional)

Can specify affected area: `feat(auth): add OAuth support`

### Issue Reference

Include work item in description: `fix: resolve login timeout (AB#123)`

## Notes

- First line limited to 72 characters
- Description uses imperative mood ("add" not "adds" or "added")
- No period at end of description
- Emoji are NOT allowed (incompatible with release-please)
