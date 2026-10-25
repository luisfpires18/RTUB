# Permissions: /documentation

Source: `DocumentationAuthorization` and `DocumentationService`. Page doc: [react-documentation](../react-documentation.md).

## Matrix

"Atas CV" = Veterano, Tunossauro or Magister, whatever the role. **Owner alone sees every folder.**

| Who | Open page | Normal folders | Atas AG | Atas CV | Upload new file | Replace same name | Folders, delete |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Visitor | `401` | - | - | - | - | - | - |
| Owner | yes | yes | yes | yes | yes | yes | yes |
| Admin | yes | yes | yes | only Vet/Tuno/Magister | yes | yes | yes |
| Mod | yes | yes | yes | only Vet/Tuno/Magister | yes | yes | yes |
| Member | yes | yes | yes | only Vet/Tuno/Magister | yes | no | no |
| Caloiro, Tuno | yes | yes | yes | no | yes | no | no |
| Veterano, Tunossauro, Magister | yes | yes | yes | yes | yes | no | no |
| Leitão | `403` | no | no | no | no | no | no |

- "Folders, delete" = create or delete a folder, delete a document.
- A member uploading a name that already exists is refused; Owner, Admin and Mod replace it.
- Role and type combine: an Admin who is a Veterano sees Atas CV and manages; a Leitão with any role is blocked
  (only Owner is exempt).
- A folder the caller cannot see is `404`, never `403`.
