#!/bin/bash

# commit-and-document.sh
# Commits changes, updates documentation, but doesn't commit the docs themselves

set -e

echo "=== Checking for changes ==="
git status --short

echo ""
echo "=== Changed files ==="
CHANGED_FILES=$(git diff --name-only HEAD)
STAGED_FILES=$(git diff --cached --name-only)
ALL_CHANGES=$(echo -e "$CHANGED_FILES\n$STAGED_FILES" | sort -u | grep -v "^$")

if [ -z "$ALL_CHANGES" ]; then
    echo "No changes to commit"
    exit 0
fi

echo "$ALL_CHANGES"

echo ""
echo "=== Enter commit message ==="
read -p "Commit message: " COMMIT_MSG

if [ -z "$COMMIT_MSG" ]; then
    echo "Commit message cannot be empty"
    exit 1
fi

# Generate timestamp
TIMESTAMP=$(date "+%Y-%m-%d %H:%M:%S")

# Create changelog entry
CHANGELOG="## $TIMESTAMP - $COMMIT_MSG

Changed files:
$(echo "$ALL_CHANGES" | sed 's/^/- /')

"

echo ""
echo "=== Updating documentation files ==="

# Update CLAUDE.md with changelog (append at end before final newlines)
if [ -f "CLAUDE.md" ]; then
    # Remove trailing newlines, add changelog, add newlines back
    sed -i -e :a -e '/^\s*$/d;N;ba' CLAUDE.md
    echo "" >> CLAUDE.md
    echo "## Recent Changes" >> CLAUDE.md
    echo "" >> CLAUDE.md
    echo "$CHANGELOG" >> CLAUDE.md
    echo "CLAUDE.md updated"
fi

# Update agents.md with changelog
if [ -f "agents.md" ]; then
    sed -i -e :a -e '/^\s*$/d;N;ba' agents.md
    echo "" >> agents.md
    echo "## Recent Changes" >> agents.md
    echo "" >> agents.md
    echo "$CHANGELOG" >> agents.md
    echo "agents.md updated"
fi

# Update README.md changelog section if it exists
if [ -f "README.md" ]; then
    if grep -q "## Changelog" README.md; then
        # Insert after "## Changelog" line
        sed -i "/## Changelog/a\\
\\
$CHANGELOG" README.md
        echo "README.md updated"
    fi
fi

echo ""
echo "=== Staging files for commit (excluding CLAUDE.md and agents.md) ==="

# Stage all changes except CLAUDE.md and agents.md
git add .
git reset HEAD CLAUDE.md agents.md 2>/dev/null || true

echo ""
echo "=== Files to be committed ==="
git diff --cached --name-only

echo ""
read -p "Proceed with commit and push? (y/n): " CONFIRM

if [ "$CONFIRM" != "y" ]; then
    echo "Commit cancelled"
    git reset HEAD
    exit 0
fi

echo ""
echo "=== Committing ==="
git commit -m "$COMMIT_MSG"

echo ""
echo "=== Pushing to remote ==="
git push

echo ""
echo "✅ Done! Changes committed and pushed."
echo "📝 Documentation updated but not committed (CLAUDE.md, agents.md)"
