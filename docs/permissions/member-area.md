# Permissions: /profile, /members/map, /hall-of-fame

Source: `MyProfileService, HallOfFameService`. Page doc: [react-member-area](../react-member-area.md).

| Who | What |
| --- | --- |
| Visitor | `401` on the API; `/profile` shows the "área reservada" card, the others redirect to sign in |
| Any signed-in member (Leitão included) | their own profile only; the members map; Hall of Fame |
| Leitão only (no other category) | cannot change the nickname |
| Any role | no extra rights here: the profile is always the caller's own |
