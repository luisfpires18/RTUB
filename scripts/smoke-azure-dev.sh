#!/bin/bash

# Kept at this path so refresh-dev-database.yml (unit 029) runs exactly what it was proven with.
# Everything lives in smoke-azure.sh; this only pins the app to rtub-dev.
APP=rtub-dev exec bash "$(dirname "${BASH_SOURCE[0]}")/smoke-azure.sh" "$@"
