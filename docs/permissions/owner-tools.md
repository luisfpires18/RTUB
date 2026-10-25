# Permissions: Owner tools

Source: `OwnerEndpoints`. Page doc: [react-owner-operations](../react-owner-operations.md).

| Who | What |
| --- | --- |
| Visitor | `401` |
| Everyone else, Admin included | `403` |
| Owner | `/emails`, `/notifications`, `/users`, `/owner/tracing`, `/owner/db` |

The whole `/api/owner` group is gated before any handler runs. `/emails` and `/notifications` used to be Admin pages.
