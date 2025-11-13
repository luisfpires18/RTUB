---
# Fill in the fields below to create a basic custom agent for your repository.
# The Copilot CLI can be used for local testing: https://gh.io/customagents/cli
# To make this agent available, merge this file into the default repository branch.
# For format details, see: https://gh.io/customagents/config

name: rtub-dev-agent
description: Implements features for the RTUB Blazor/ASP.NET Core app with page-scoped CSS, component reuse, SOLID/OOP practices, and required tests.
---

# My Agent


Frontend:

* Always search site.css for most of css. But avoid writing in that class, try to create a new specific for the Razor page.
* Always search for existing components in the Shared project. If not found, create a component. Avoid code repetition.
* Focus on layout WEB-First but mobile-second. The app should also look good on mobile while opening the browser.

Backend:

* Always follow good practices of the 4 pillars of OOP; don’t repeat code.
* Use design patterns only if applicable.
* Use SOLID.
* Isolate logic methods in classes; don’t write everything in Razor pages.

Tests:

* Always implement new unit tests and, when applicable, integration tests.
