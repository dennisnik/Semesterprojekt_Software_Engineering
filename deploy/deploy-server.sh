#!/usr/bin/env bash
# Deploys the committed state of the current branch to the course server.
#
#   SSH_TARGET=tinf25d-two-and-a-half-men@212.227.31.76 ./deploy/deploy-server.sh
#
# - Transfers the source with `git archive` (only committed files → never secrets like .env or data/*.yaml).
# - Builds the image ON the server (x86_64) and (re)starts the stack with the server overlay.
# - The server-side deploy/.env (secrets) is created once by hand from deploy/.env.example and is never overwritten.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

TARGET="${SSH_TARGET:?set SSH_TARGET=user@host}"
REMOTE_DIR="${REMOTE_DIR:-tn-watch}"
REV="$(git rev-parse --short HEAD)"

[ -z "$(git status --porcelain)" ] || { echo "Working tree not clean – commit first (only committed files are deployed)." >&2; exit 1; }

echo "Deploying $REV to $TARGET:~/$REMOTE_DIR"
git archive --format=tar HEAD | ssh "$TARGET" "mkdir -p ~/$REMOTE_DIR && tar -x -C ~/$REMOTE_DIR && echo $REV > ~/$REMOTE_DIR/DEPLOYED_REVISION"
ssh "$TARGET" "cd ~/$REMOTE_DIR/deploy && test -f .env || { echo 'Missing ~/$REMOTE_DIR/deploy/.env (copy .env.example and fill in)'; exit 1; }
  docker compose -f docker-compose.yml -f docker-compose.server.yml up -d --build --remove-orphans
  docker compose -f docker-compose.yml -f docker-compose.server.yml ps --format 'table {{.Name}}\t{{.Status}}'"
