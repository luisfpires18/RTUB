#!/bin/bash

# Resolves which SQLite file Azure DEV actually opens, from rtub-dev's own
# ConnectionStrings__SqliteConnection, and turns it into a Kudu VFS path.
#
# Why this exists: refresh-dev-database.yml used to hardcode site/data/rtub-dev.db.
# rtub-dev had since been re-pointed at a fresh database file, so the workflow backed up,
# replaced and cleaned the sidecars of an empty 4 KB file the app never opened. The only
# source of truth for which file the app opens is the app's own setting, so that is what
# this reads - never a literal.
#
#   SQLITE_CONNECTION_STRING='<setting value>' bash scripts/resolve-dev-db-path.sh
#       prints the VFS path, e.g. site/data/<file>.db
#   bash scripts/resolve-dev-db-path.sh --self-test
#
# Exactly one shape is accepted; everything else fails closed:
#
#   Data Source=/home/site/data/<file>.db
#
#   - keyword 'Data Source', case-insensitive, as Microsoft.Data.Sqlite matches it;
#     the 'DataSource' and 'Filename' aliases are refused
#   - no second keyword (Mode, Cache, Password, ...) - one trailing ';' is tolerated
#   - the path must sit directly in /home/site/data/: no subdirectory, no '..'
#   - <file> is [A-Za-z0-9][A-Za-z0-9._-]* ending in .db
#
# That last allow-list is also what makes the output safe to append to $GITHUB_ENV:
# no newline, quote, space or '=' can get through to smuggle in a second variable.
#
# The connection string is read from the environment, never from an argument, and is never
# printed - not on success and not in an error message. Only the validated path is.
#
# Exit codes: 0 resolved, 1 rejected, 2 wrong invocation.

set -u

readonly DATA_DIR="/home/site/data/"

trim() {
    local v=$1
    v="${v#"${v%%[![:space:]]*}"}"
    v="${v%"${v##*[![:space:]]}"}"
    printf '%s' "$v"
}

# resolve <raw setting value>
# Sets RESOLVED_VFS_PATH on success. On failure sets REJECT_REASON and returns 1.
# REJECT_REASON is always a fixed sentence: no part of the input is ever echoed into it.
resolve() {
    RESOLVED_VFS_PATH=""
    REJECT_REASON=""

    local s key value file

    # Surrounding whitespace only - which includes the trailing CR that az emits on
    # Windows. A CR *inside* the value is left alone, and the allow-list rejects it.
    s=$(trim "$1")

    if [ -z "$s" ]; then
        REJECT_REASON="ConnectionStrings__SqliteConnection is missing or empty"
        return 1
    fi

    s=$(trim "${s%;}")

    # A second keyword is refused rather than interpreted. Mode=Memory would mean there is
    # no file at all; Password would mean the unencrypted sanitized copy cannot be opened.
    if [[ $s == *";"* ]]; then
        REJECT_REASON="the connection string carries more than one keyword; only 'Data Source' is accepted"
        return 1
    fi

    if [[ $s != *"="* ]]; then
        REJECT_REASON="the setting is not a SQLite connection string"
        return 1
    fi

    key=$(trim "${s%%=*}")
    value=$(trim "${s#*=}")

    case "$key" in
        [Dd][Aa][Tt][Aa]" "[Ss][Oo][Uu][Rr][Cc][Ee]) ;;
        *)
            REJECT_REASON="the keyword is not 'Data Source'"
            return 1 ;;
    esac

    # Case-sensitive on purpose: /Home/site/data is a different directory on Linux.
    if [[ $value != "$DATA_DIR"* ]]; then
        REJECT_REASON="Data Source is not under ${DATA_DIR}"
        return 1
    fi

    file=${value#"$DATA_DIR"}

    if [[ $file == *"/"* || $file == *".."* ]]; then
        REJECT_REASON="Data Source has a subdirectory or traversal component"
        return 1
    fi

    if [[ $file != *.db ]]; then
        REJECT_REASON="Data Source does not name a .db file"
        return 1
    fi

    if [[ ! $file =~ ^[A-Za-z0-9][A-Za-z0-9._-]*\.db$ ]]; then
        REJECT_REASON="Data Source file name has unsupported characters"
        return 1
    fi

    RESOLVED_VFS_PATH="site/data/${file}"
}

if [ "${1:-}" = "--self-test" ]; then
    rc=0

    # OK lines go to stdout, FAIL lines to stderr, so a caller can discard the noise
    # with > /dev/null and still see every failure.
    accepts() { # expected-vfs-path label value
        if resolve "$3" && [ "$RESOLVED_VFS_PATH" = "$1" ]; then
            echo "  OK    accepts: $2"
        else
            echo "  FAIL  should accept: $2 (got '${RESOLVED_VFS_PATH}', ${REJECT_REASON:-no reason})" >&2
            rc=1
        fi
    }

    rejects() { # label value
        if resolve "$2"; then
            echo "  FAIL  should reject: $1 (resolved to '${RESOLVED_VFS_PATH}')" >&2
            rc=1
        else
            echo "  OK    rejects: $1"
        fi
    }

    echo "resolve-dev-db-path self-test"

    accepts site/data/rtub-dev-v3.db "the live rtub-dev value"     'Data Source=/home/site/data/rtub-dev-v3.db'
    accepts site/data/rtub-dev.db    "a different file name"        'Data Source=/home/site/data/rtub-dev.db'
    accepts site/data/rtub-dev-v3.db "one trailing semicolon"       'Data Source=/home/site/data/rtub-dev-v3.db;'
    accepts site/data/rtub-dev-v3.db "CRLF from az on Windows"      $'Data Source=/home/site/data/rtub-dev-v3.db\r'
    accepts site/data/rtub-dev-v3.db "keyword case and spacing"     '  data source = /home/site/data/rtub-dev-v3.db  '

    # missing / not SQLite
    rejects "unset or empty"                     ''
    rejects "whitespace only"                    $'  \r'
    rejects "az printing None for null"          'None'
    rejects "a bare path, no keyword"            '/home/site/data/rtub-dev.db'
    rejects "SQL Server connection string"       'Server=tcp:example.database.windows.net;Database=rtub'
    rejects "in-memory database"                 'Data Source=:memory:'
    rejects "Mode=Memory alone"                  'Mode=Memory'
    rejects "DataSource alias"                   'DataSource=/home/site/data/rtub-dev.db'
    rejects "Filename alias"                     'Filename=/home/site/data/rtub-dev.db'
    rejects "second keyword"                     'Data Source=/home/site/data/rtub-dev.db;Mode=ReadWriteCreate'
    rejects "two trailing semicolons"            'Data Source=/home/site/data/rtub-dev.db;;'

    # not under /home/site/data/
    rejects "relative path"                      'Data Source=rtub-dev.db'
    rejects "wwwroot"                            'Data Source=/home/site/wwwroot/app.db'
    rejects "look-alike directory"               'Data Source=/home/site/database/rtub-dev.db'
    rejects "different case"                     'Data Source=/Home/site/data/rtub-dev.db'
    rejects "file: URI"                          'Data Source=file:/home/site/data/rtub-dev.db'
    rejects "quoted value"                       'Data Source="/home/site/data/rtub-dev.db"'

    # traversal / suspicious components
    rejects "parent traversal"                   'Data Source=/home/site/data/../wwwroot/app.db'
    rejects "subdirectory"                       'Data Source=/home/site/data/old/rtub-dev.db'
    rejects "double slash"                       'Data Source=/home/site/data//rtub-dev.db'
    rejects "dot segment"                        'Data Source=/home/site/data/./rtub-dev.db'
    rejects "double dot in name"                 'Data Source=/home/site/data/rtub..db'
    rejects "hidden file"                        'Data Source=/home/site/data/.rtub-dev.db'
    rejects "percent-encoded name"               'Data Source=/home/site/data/%2e%2e.db'
    rejects "space in name"                      'Data Source=/home/site/data/rtub dev.db'
    rejects "embedded CR"                        $'Data Source=/home/site/data/rtub\rdev.db'
    rejects "newline: \$GITHUB_ENV injection"    $'Data Source=/home/site/data/rtub.db\nAZURE_CLIENT_ID=x.db'

    # not a .db file
    rejects "the directory itself"               'Data Source=/home/site/data/'
    rejects "bare .db"                           'Data Source=/home/site/data/.db'
    rejects "wrong extension"                    'Data Source=/home/site/data/rtub-dev.sqlite'
    rejects "a WAL sidecar"                      'Data Source=/home/site/data/rtub-dev.db-wal'
    rejects "a rollback copy"                    'Data Source=/home/site/data/rtub-dev.db.rollback'

    # No rejection reason may echo any part of the input: the setting could carry a secret.
    resolve 'Data Source=/home/site/data/rtub-dev.db;Password=hunter2-secret' || true
    case "$REJECT_REASON" in
        *hunter2*|*Password*|*rtub-dev*)
            echo "  FAIL  a rejection reason echoed part of the input" >&2
            rc=1 ;;
        *)
            echo "  OK    rejection reasons never echo the input" ;;
    esac

    if [ "$rc" -eq 0 ]; then
        echo "resolve-dev-db-path self-test PASSED"
    else
        echo "resolve-dev-db-path self-test FAILED" >&2
    fi
    exit "$rc"
fi

if [ $# -ne 0 ]; then
    echo "usage: SQLITE_CONNECTION_STRING=<value> $0" >&2
    echo "       $0 --self-test" >&2
    exit 2
fi

if ! resolve "${SQLITE_CONNECTION_STRING:-}"; then
    echo "::error::Refusing to resolve the Azure DEV database: ${REJECT_REASON}." >&2
    exit 1
fi

printf '%s\n' "$RESOLVED_VFS_PATH"
