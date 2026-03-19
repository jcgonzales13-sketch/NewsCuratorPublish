#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${ROOT_DIR}/.env.local"

if [[ ! -f "${ENV_FILE}" ]]; then
  echo "Missing ${ENV_FILE}. Copy .env.local.example to .env.local and fill in your secrets."
  exit 1
fi

set -a
source "${ENV_FILE}"
set +a

cd "${ROOT_DIR}"
dotnet run --project src/AiNewsCurator.Api --launch-profile AiNewsCurator.Api
