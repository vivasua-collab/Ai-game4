#!/usr/bin/env bash
# =============================================================================
# DEPRECATED — используйте cold_start.sh
# =============================================================================
# Этот файл оставлен для обратной совместимости.
# Новая процедура холодного старта: cold_start.sh
# Дизайн: docs/docs_v2/09_workflow/COLD_START.md
# =============================================================================

echo "[recover_sandbox.sh] DEPRECATED — перенаправление на cold_start.sh"
echo ""

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec bash "$SCRIPT_DIR/cold_start.sh" "$@"
