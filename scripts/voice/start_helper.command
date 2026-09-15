#!/bin/sh
set -eu
cd "$(dirname "$0")/../.."
printf '%s\n' 'Bara Kitchen Voice Helper' 'Enter your own OpenAI key in the game. No key file is needed.'
if ! command -v python3 >/dev/null 2>&1; then
  printf '%s\n' 'Install Python 3.9 or newer, then run this helper again.'
  exit 1
fi
if [ ! -x .local/voice-venv/bin/python ]; then
  python3 -m venv .local/voice-venv
fi
if ! .local/voice-venv/bin/python -c 'import aiohttp' >/dev/null 2>&1; then
  .local/voice-venv/bin/pip install -r scripts/voice/requirements.txt
fi
exec .local/voice-venv/bin/python scripts/voice/live_chef_server.py --player-keys-only
